using System.IO.Compression;

using StlTechRelGen.Db;

namespace StlTechRelGen;

/// <summary>
/// 与界面无关的生成流程。CLI、本地 Web UI、将来的原生 GUI 都调用它。
///
/// 与 <see cref="Program"/> 的区别：不依赖 Spectre 的进度条与交互提示，
/// 进度任务传 null，日志通过回调交给调用方渲染。
/// </summary>
internal static class Generator {
	public sealed record Result(
		int LanguageCount,
		int TechCount,
		int RelationCount,
		string OutputDirectory,
		string[] Files
	);

	/// <summary>
	/// 解析生成目标：数据来源（原版 / 播放集全部 / 播放集指定 MOD）与输出目标（MOD / 目录）分开决定。
	/// 兼容旧配置：[playset].name 回退为播放集名，[playset].target 回退为目标。
	/// </summary>
	public static (string destPath, List<Mod> sourceMods) ResolveTarget(
		Config config,
		LauncherV2DbContext db
	) {
		string playsetName = string.IsNullOrWhiteSpace(config.Data.Playset)
			? config.Playset?.Name ?? ""
			: config.Data.Playset;

		bool playsetMode = string.Equals(
			config.Data.Mode, "playset", StringComparison.OrdinalIgnoreCase
		);

		// 数据来源
		List<Mod> dataMods = [];
		if (playsetMode) {
			if (string.IsNullOrWhiteSpace(playsetName)) {
				throw new InvalidOperationException(
					"播放集模式下必须指定播放集（[data].playset）"
				);
			}

			Playset playset = FindPlayset(db, playsetName);
			dataMods = [.. db.GetModsInPlayset(playset)];

			if (!string.IsNullOrWhiteSpace(config.Data.SourceMod)) {
				Mod? source = dataMods.FirstOrDefault(
					i => i.DisplayName == config.Data.SourceMod
				);
				if (source is null) {
					throw new InvalidOperationException(
						$"播放集「{playset.Name}」里没有名为「{config.Data.SourceMod}」的模组"
					);
				}

				dataMods = [source];
			}
		}

		// 输出目标：新字段优先，旧 [playset].target 回退
		string targetMod = config.Output.TargetMod;
		string targetDir = config.Output.TargetDir;
		if (string.IsNullOrWhiteSpace(targetMod)
			&& string.IsNullOrWhiteSpace(targetDir)
			&& !string.IsNullOrWhiteSpace(config.Playset?.Target)) {
			if (playsetMode) {
				targetMod = config.Playset.Target;
			} else {
				targetDir = config.Playset.Target;
			}
		}

		if (!string.IsNullOrWhiteSpace(targetMod)) {
			if (string.IsNullOrWhiteSpace(playsetName)) {
				throw new InvalidOperationException(
					"指定目标 MOD 时必须同时指定播放集（[data].playset）"
				);
			}

			Playset playset = FindPlayset(db, playsetName);
			List<Mod> mods = [.. db.GetModsInPlayset(playset)];
			Mod[] candidates = [.. mods.Where(i => i.DisplayName == targetMod)];
			if (candidates.Length == 0) {
				throw new InvalidOperationException(
					$"播放集「{playset.Name}」里没有名为「{targetMod}」的模组"
				);
			}

			if (candidates.Length > 1) {
				throw new InvalidOperationException(
					$"播放集里有 {candidates.Length} 个同名模组「{targetMod}」，无法确定目标"
				);
			}

			return (candidates[0].Path(), dataMods);
		}

		if (string.IsNullOrWhiteSpace(targetDir)) {
			throw new InvalidOperationException(
				"必须指定目标 MOD（[output].target_mod）或输出目录（[output].target_dir）"
			);
		}

		return (targetDir, dataMods);
	}

	private static Playset FindPlayset(LauncherV2DbContext db, string name) =>
		db.Playsets.SingleOrDefault(i => i.Name == name)
			?? throw new InvalidOperationException($"找不到播放集：{name}");

	/// <summary>执行一次完整生成。</summary>
	public static async Task<Result> RunAsync(
		Config config,
		Action<string> log,
		CancellationToken ct = default
	) {
		ArgumentNullException.ThrowIfNull(config);

		config.Validate();

		if (config.SuppressesCWToolsErrors) {
			CWTools.Utilities.Utils.logError =
				FuncConvert.FromAction((string _) => { });
		}

		log("正在连接启动器数据库…");
		List<Mod> sourceMods;
		string destPath;
		using (LauncherV2DbContext db = new(config.Game)) {
			(destPath, sourceMods) = ResolveTarget(config, db);
		}

		log($"输出目录：{destPath}");

		DirectoryInfo l10nDir = new(Path.Combine(destPath, "localisation"));

		ct.ThrowIfCancellationRequested();
		log("正在加载游戏数据（CWTools，首次较慢）…");

		GameData gameData = await Task.Run(
			() => GameData.LoadWithProgress(
				null,
				config,
				sourceMods.Select(i => (i.DisplayName!, i.Path()))
			),
			ct
		);

		ct.ThrowIfCancellationRequested();
		log("正在准备输出目录（会清空目标 localisation/）…");
		PrepareOutputDir(l10nDir);

		ct.ThrowIfCancellationRequested();
		log($"已加载 {gameData.TechTable.Techs.Count} 个科技，开始生成文本…");

		L10nBuilder builder = new(gameData, config);
		foreach ((string id, Model.Tech tech) in gameData.TechTable.Techs) {
			ct.ThrowIfCancellationRequested();
			builder.BuildTech(id, tech);
		}

		log("正在写入本地化文件…");
		DirectoryInfo replaceDir = l10nDir.CreateSubdirectory("replace");
		builder.WriteFilesWithProgress(null, replaceDir.FullName);

		if (config.Output.WriteFragments) {
			WriteFragments(l10nDir, config);
		}

		string[] files = [.. Directory
			.EnumerateFiles(l10nDir.FullName, "*.yml", SearchOption.AllDirectories)
			.Select(Path.GetFileName)
			.OfType<string>()
			.OrderBy(i => i, StringComparer.Ordinal)];

		return new Result(
			builder.Generated.Keys.Count,
			gameData.TechTable.Techs.Count,
			2 * gameData.TechTable.Techs.Values.Sum(i => i.Unlocks.Count),
			l10nDir.FullName,
			files
		);
	}

	private static void PrepareOutputDir(DirectoryInfo outputDir) {
		if (outputDir.Exists) {
			outputDir.Delete(true);
		}

		outputDir.Create();
	}

	private static void WriteFragments(DirectoryInfo outputDirectory, Config config) {
		const string fragmentPrefix = $"{nameof(StlTechRelGen)}.Resources.fragments.";
		System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

		foreach (string resource in assembly.GetManifestResourceNames()) {
			if (!resource.StartsWith(fragmentPrefix, StringComparison.Ordinal)) {
				continue;
			}

			// 只输出配置里选定的语言
			if (config.Output.Languages.Count > 0
				&& !config.Output.Languages.Any(
					l => resource.EndsWith($"_{l}.yml", StringComparison.OrdinalIgnoreCase)
				)
			) {
				continue;
			}

			string name = resource[fragmentPrefix.Length..].Replace(
				"techrel_l_", $"{config.Output.FilePrefix}_l_", StringComparison.Ordinal
			);

			using Stream stream = assembly.GetManifestResourceStream(resource)!;
			using FileStream file = File.Open(
				Path.Combine(outputDirectory.FullName, name),
				Utils.GlobalInstances.FileWriteOptions
			);
			stream.CopyTo(file);
		}
	}

	/// <summary>读取启动器里的播放集列表（供界面下拉框用）。</summary>
	/// <param name="docOverride">界面当前输入的文档目录；为空时用配置文件里的值。</param>
	public static List<string> ListPlaysets(Config config, string? docOverride = null) {
		using LauncherV2DbContext db = new(ResolveGame(config, docOverride));
		// 先取回数据库再排序：带 StringComparer 的 OrderBy 无法被 EF Core 翻译成 SQL
		return [.. db.Playsets
			.Select(i => i.Name)
			.ToList()
			.OrderBy(i => i, StringComparer.Ordinal)];
	}

	/// <summary>为播放集列出模组展示名（供目标模组下拉框用）。</summary>
	public static List<string> ListModsInPlayset(
		Config config,
		string playsetName,
		string? docOverride = null
	) {
		using LauncherV2DbContext db = new(ResolveGame(config, docOverride));
		Playset? playset = db.Playsets.SingleOrDefault(i => i.Name == playsetName);
		return playset is null
			? []
			: [.. db.GetModsInPlayset(playset)
				.Select(i => i.DisplayName)
				.ToList()
				.Where(i => !string.IsNullOrEmpty(i))
				.Select(i => i!)
				.OrderBy(i => i, StringComparer.Ordinal)];
	}

	/// <summary>用界面传入的文档目录覆盖配置值，否则「刚填的路径」对接口不生效。</summary>
	private static Config.GameConfig ResolveGame(Config config, string? docOverride) {
		if (string.IsNullOrWhiteSpace(docOverride)) {
			return config.Game;
		}

		return new() {
			GamePath = config.Game.GamePath,
			DocumentPath = docOverride,
		};
	}
}
