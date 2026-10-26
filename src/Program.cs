using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Spectre.Console.Cli;

using StlTechRelGen.Db;

namespace StlTechRelGen;

public static class Program {
	public static async Task Main(string[] args) {
		// --ui / -u：启动本地 Web 界面（浏览器打开），而不是命令行流程
		if (args.Length > 0 && (args[0] is "--ui" or "-u" or "ui")) {
			Initialize();
			string url = UiServer.Start();
			Console.WriteLine($"界面已启动：{url}");
			Console.WriteLine("浏览器没自动打开就手动访问上面的地址；关闭本窗口即停止服务。");
			UiServer.OpenBrowser(url);

			// 一直运行，直到用户关闭窗口
			await Task.Delay(Timeout.Infinite);
			return;
		}

		try {
			Initialize();
			Config config = ResolveConfig(args);
			await Execute(config);
		} catch (Exception e) {
			AnsiConsole.WriteException(e);
		}
	}

	private static void Initialize() {
		// Add support for codepage 1252, used by CWTools
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		// Initialize SQLitePCL using the packages:
		// SQLitePCLRaw.config.e_sqlite3 (glue) + SourceGear.sqlite3 (binary).
		SQLitePCL.Batteries_V2.Init();
	}

	private static Config ResolveConfig(string[] args) {
		if (args.Length == 0) {
			Config config = Config.Load() ?? Inquiries.RunSetupWizard();
			config.Save(); // Roundtrip to format, or save newly created
			return config;
		}

		// Spectre.Console.Cli normally runs a command to completion inside
		// app.Run(). We hijack Execute() to capture parsed arguments into
		// a static property, then return control to the caller.
		// If Arguments is null, a built-in option (-h/-v) ran instead;
		// for those options we call Environment.Exit and never return.
		CommandApp<CliArgs> app = new();
		app.Configure(config =>
			config.UseAssemblyInformationalVersion()
		);
		app.Run(args);

		if (CliArgs.Arguments is not CliArgs.Settings arguments) {
			// Main command was not invoked, e.g. -h or -v was used
			Environment.Exit(0);
			throw new UnreachableException();
		} else {
			Config config = arguments.ToConfig();
			config.Normalize();
			return config;
		}
	}

	private static async Task Execute(Config config) {
		config.Validate();

		if (config.OverrideLanguage != null) {
			LoadMessages(config.OverrideLanguage);
		}

		if (config.SuppressesCWToolsErrors) {
			// CWTools emits two error lines about missing rule files.
			// Suppress them by replacing the F# logError function with a
			// no-op. FuncConvert.FromAction bridges C# Action to FSharpFunc.
			CWTools.Utilities.Utils.logError = FuncConvert.FromAction((string _) => { });
		}

		Inquiries.WaitForLauncherClose(config);

		(string destPath, Mod[] mods) = await GetTarget(config);
		DirectoryInfo l10nDir = new(Path.Combine(destPath, "localisation"));

		(int countL10nFiles, int countTechs, int countRelations) = await AnsiConsole
			.Progress()
			.UsePreset()
			.StartAsync(async (ctx) => {
				GameData gameData = LoadGameData(ctx, config, mods);
				// 加载完成后再清空输出目录：目标 MOD 可作为数据来源时也能读到自己的本地化
				PrepareOutputDir(l10nDir);
				L10nBuilder l10nBuilder = BuildL10n(ctx, gameData, config);

				Parallel.Invoke(
					() => WriteL10nFragments(ctx, l10nDir, config),
					() => l10nBuilder.WriteFilesWithProgress(
						ctx,
						l10nDir.CreateSubdirectory("replace").FullName
					)
				);

				return (
					l10nBuilder.Generated.Keys.Count,
					gameData.TechTable.Techs.Count,
					// 2 * because each Unlocks entry represents a
					// bidirectional edge: "tech_a requires tech_b"
					// produces both a "requires" line and an "unlocks"
					// line in the output.
					2 * gameData.TechTable.Techs.Values
						.Sum(i => i.Unlocks.Count)
				);
			});

		Log(Messages.Prompt.SavedL10n, countL10nFiles, Markup.Escape(l10nDir.FullName));
		Log(Messages.Prompt.GenerationSummary, countRelations, countTechs);
		if (!config.Yesmen) {
			Inquiries.Pause();
		}

	}


	private static void PrepareOutputDir(DirectoryInfo outputDir) {
		try {
			if (outputDir.Exists) {
				outputDir.Delete(true);
			}

			outputDir.Create();
		} catch {
			LogError(
				Messages.Error.FailedToAccessOutputDir,
				Markup.Escape(outputDir.FullName)
			);
			throw;
		}
	}

	private static GameData LoadGameData(
		ProgressContext ctx,
		Config config,
		Mod[] sourceMods
	) => GameData.LoadWithProgress(
		ctx, config, sourceMods.Select(i => (i.DisplayName!, i.Path()))
	);

	private static L10nBuilder BuildL10n(
		ProgressContext? ctx,
		GameData gameData,
		Config config
	) {
		L10nBuilder l10nBuilder = new(gameData, config);
		gameData.TechTable.Techs
			.DriveProgressTask(ProgressExt.Task(ctx, Messages.Progress.GeneratingL10n))
			.ForEach(i => l10nBuilder.BuildTech(i.Key, i.Value));
		return l10nBuilder;
	}

	private static void WriteL10nFragments(
		ProgressContext? ctx,
		DirectoryInfo outputDirectory,
		Config config
	) {
		const string fragmentPrefix = $"{nameof(StlTechRelGen)}.Resources.fragments.";
		Assembly assembly = Assembly.GetExecutingAssembly();

		assembly
			.GetManifestResourceNames()
			.Where(i => i.StartsWith(fragmentPrefix, StringComparison.Ordinal))
			// 只输出配置里选定的语言，避免生成本用不到的文件
			.Where(i => config.Output.Languages.Count == 0
				|| config.Output.Languages.Any(
					l => i.EndsWith($"_{l}.yml", StringComparison.OrdinalIgnoreCase)
				)
			)
			.DriveProgressTask(ProgressExt.Task(ctx, Messages.Progress.WritingL10nFragments))
			.Select(i => i[fragmentPrefix.Length..])
			.ForEach(i => {
				// 片段文件名跟着 [output].file_prefix 走，并保留 _l_ 语言标记：
				// !techrel_l_english.yml → !<prefix>_l_english.yml
				string fileName = i.Replace(
					"techrel_l_", $"{config.Output.FilePrefix}_l_",
					StringComparison.Ordinal
				);

				using Stream stream = assembly
					.GetManifestResourceStream(fragmentPrefix + i)!;
				using FileStream file = File.Open(
					Path.Combine(outputDirectory.FullName, fileName),
					GlobalInstances.FileWriteOptions
				);
				stream.CopyTo(file);
			});
	}

	private static async Task<(string destPath, Mod[] sourceMods)> GetTarget(
		Config config
	) {
		using LauncherV2DbContext db = await Inquiries.RunWithProgressAsync(
			Messages.Progress.ConnectingLauncherDb,
			() => new LauncherV2DbContext(config.Game)
		);

		bool hasSavedTarget = config.Playset is not null
			|| !string.IsNullOrWhiteSpace(config.Data.Playset)
			|| !string.IsNullOrWhiteSpace(config.Output.TargetMod)
			|| !string.IsNullOrWhiteSpace(config.Output.TargetDir);
		if (hasSavedTarget && (config.Yesmen || Inquiries.ConfirmUseSavedSelection(config))) {
			try {
				(string destPath, List<Mod> mods) = Generator.ResolveTarget(config, db);
				return (destPath, [.. mods]);
			} catch (Exception) when (!config.Yesmen) {
				// 已保存的配置无效时，回退到交互式选择
			}
		}

		return Inquiries.SelectOutputTarget(config, db);
	}
}
