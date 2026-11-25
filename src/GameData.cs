using System.Collections.Concurrent;
using System.IO.Compression;

using CWTools.Games;
using CWTools.Validation;

using Microsoft.FSharp.Collections;

using StlTechRelGen.Model;

using static CWTools.Games.Files;


namespace StlTechRelGen;

internal sealed class GameData {
	// Read only these 3 common/ subdirectories; everything else is excluded
	// by ignoreGlobs below. This avoids CWTools parsing irrelevant files.
	private static readonly IReadOnlyCollection<string> includedCommonSubdirs = [
		"governments", // auth_suffix is used in localizations
		"scripted_variables", // may be used everywhere
		"technology", // obviously
	];

	private WorkspaceDirectory GameDir { get; init; }
	private WorkspaceDirectoryInput[] ModDirs { get; init; }

	private STLGameObject Game { get; init; }
	private EntitySet<SECData> Entities { get; init; }

	public CWComparer Comparer { get; private init; }

	public ReadOnlyDictionary<string, CWValue> ScriptedVariables {
		get; private set;
	} = null!;
	public ReadOnlyCollection<string> AuthSuffixes { get; private set; } = null!;
	public TechTable TechTable { get; private set; } = null!;
	public ReadOnlyDictionary<CWLang, ReadOnlyDictionary<string, string>>
		Localizations = null!;

	private GameData(
		Config config,
		IEnumerable<(string name, string path)> mods,
		ProgressContext? ctx
	) {
		// There are 5 major steps:
		// 1. ResourceManager<>.updateFiles
		// 2. LocalisationManager<>.updateAllLocalisationSources #1
		// 3. LocalisationManager<>.updateProcessedLocalisation #1
		// 4. LocalisationManager<>.updateAllLocalisationSources #2
		// 5. LocalisationManager<>.updateProcessedLocalisation #2
		ProgressTask? task = ProgressExt.Task(ctx, Messages.Progress.LoadingGame)?
			.MaxValue(5);

		string commonDir = Path.Combine(config.Game.GamePath, "common");

		GameDir = new(
			config.Game.GamePath, Constants.GameDirName
		);
		ModDirs = [..mods.Select(i => {
			string path = Path.GetFullPath(i.path);
			if (Directory.Exists(path)) {
				return WorkspaceDirectoryInput.NewWD(
					new WorkspaceDirectory(path, i.name)
				);
			} else if (File.Exists(path) && Path.GetExtension(path) == ".zip") {
				string root = path.Replace('\\', '/');
				using ZipArchive archive = ZipFile.OpenRead(path);
				return WorkspaceDirectoryInput.NewZD(new(
					path,
					Path.GetFileName(path),
					// CWTools 2.39.0 起该字段由 FSharpList<Tuple<string,string>>
					// 改为 (string, string)[]（ValueTuple 数组）。
					[.. archive.Entries
						.Select(i => (
							$"{root}/{i.FullName}",
							i.Open().ReadToString()
						))
					]
				));
			}

			throw new InvalidDataException($"Invalid mod path: {i}");
		})];

		Comparer = new(GameDir, ModDirs);

		// CWTools 2.39.0 起 rootDirectories 由 FSharpList 改为数组
		WorkspaceDirectoryInput[] sourceDirs = [
			WorkspaceDirectoryInput.NewWD(GameDir),
			..ModDirs,
		];

		// Exclude subdirs of common
		string[] ignoreGlobs = [
			.. Directory
				.EnumerateDirectories(
					commonDir, "*", SearchOption.TopDirectoryOnly
				)
				.Select(i => Path.GetRelativePath(commonDir, i))
				// Except for these
				.Where(i => includedCommonSubdirs.All(
					j => !string.Equals(
						i, j, StringComparison.OrdinalIgnoreCase
					)
				))
				.Select(i => $"common/{i}/"),
			// Along with other hardcoded dirs that will be read by CWTools
			// https://github.com/cwtools/cwtools/blob/b377453dee803f9258be92cfc49896d09039702d/CWTools/Common/STLConstants.fs#L156-L170
			"events/", "flags/", "fonts/", "gfx/", "interface/",
			"map/", "music/", "prescripted_countries/", "sound/",
		];

		CWTools.Games.Stellaris.STLGame stellaris = new(
			new GameSetupSettings<STLLookup>(
				sourceDirs,
				EmbeddedSetupSettings.NewFromConfig([], []),
				new ValidationSettings(LangHelpers.allSTLLangs, true, false),
				null,
				null,
				// CWTools 2.39.0 起 excludeGlobPatterns 由 FSharpList<string>
				// 改为 FSharpOption<string[]>，需要显式包成 Some
				FSharpOption<string[]>.Some(ignoreGlobs),
				null,
				null,
				// 2.39.0 新增的两个参数：调试设置与模组过滤
				DebugSettings.Default,
				null
			)
		);

		// CWTools.STLGame does not expose its internal game object
		// publicly. Reflection is used to reach the private "game" field
		// so we can access .Resources.AllEntities for entity-set iteration.
		// The CWTools version is pinned; if upgrading CWTools, re-check
		// that the field name and type are still valid.
		Game = (STLGameObject) typeof(CWTools.Games.Stellaris.STLGame)
			.GetField("game", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(stellaris)!;
		Entities = new(Game.Resources.AllEntities.Invoke(null));


		task?.StopTask();
	}

	public static GameData LoadWithProgress(
		ProgressContext? ctx,
		Config config,
		IEnumerable<(string name, string path)> mods
	) {
		GameData gameData = new(config, mods, ctx);

		Parallel.Invoke(
			() => {
				gameData.LoadScriptedVariables(ctx);
				// Depends on ScriptedVariables
				gameData.LoadTechTable(ctx);
			},
			() => gameData.LoadAuthSuffixes(ctx),
			() => gameData.LoadLocalization(ctx)
		);

		return gameData;
	}

	private void LoadScriptedVariables(ProgressContext? ctx) =>
		ScriptedVariables = Entities
			.AllOfType(STLConstants.EntityType.ScriptedVariables)
			.DriveProgressTask(ProgressExt.Task(ctx, 
				Messages.Progress.LoadingScriptedVariables
			))
			.Select(i => i.Item1)
			.ToSortedList(Comparer)
			.SelectMany(i => i.Leaves)
			.ToDictionaryOverwriting(i => i.Key, i => i.Value)
			.AsReadOnly();

	private void LoadTechTable(ProgressContext? ctx) =>
		TechTable = new(ctx, Entities
			.AllOfType(STLConstants.EntityType.Technology)
			.DriveProgressTask(ProgressExt.Task(ctx, 
				Messages.Progress.LoadingTechnologies
			))
			.Select(i => i.Item1)
			.ToSortedList(Comparer)
			.SelectMany(i => i.Children)
			.ToDictionaryOverwriting(
				i => i.Key,
				i => Tech.Parse(i, Comparer, ScriptedVariables)
			)
		);

	private void LoadAuthSuffixes(ProgressContext? ctx) =>
		AuthSuffixes = Entities
			.AllOfType(STLConstants.EntityType.Authorities)
			.DriveProgressTask(ProgressExt.Task(ctx, 
				Messages.Progress.LoadingAuthoritySuffixes
			))
			.Select(i => i.Item1)
			.ToSortedList(Comparer)
			.SelectMany(i => i.Children)
			.Select(i => KeyValuePair.Create(
				i.Key,
				i.Tag("localization_postfix")
			))
			// The Aggregate implementing the override (last wins).
			// Prepend(string.Empty) ensures the suffix-less base entry is
			// always included as the first item in the result list.
			.Aggregate(
				new Dictionary<string, string>(),
				(dict, i) => {
					if (OptionModule.IsSome(i.Value)) {
						dict[i.Key] = '_' + i.Value.Value.ToRawString();
					}

					return dict;
				},
				(dict) => dict.Values.Prepend(string.Empty).ToList()
			)
			.AsReadOnly();

	private void LoadLocalization(ProgressContext? ctx) {
		ConcurrentDictionary<CWLang, ReadOnlyDictionary<string, string>> L10n = new();
		// CWTools 2.39.0 起 LocalisationEntries() 返回 IEnumerable 而非数组，
		// 没有 Length；先物化成数组，长度与遍历用同一份数据。
		var entries = Game.LocalisationManager.LocalisationEntries().ToArray();
		ProgressTask? task = ProgressExt.Task(ctx, Messages.Progress.LoadingL10n)?
			.MaxValue(entries.Length);

		Parallel.ForEach(entries, (x) => {
			L10n[x.Item1] = x.Item2
				.ToSortedList(
					(x, y) => Comparer.Compare(x.Item2, y.Item2)
				)
				.ToDictionaryOverwriting(
					i => i.Item1,
					i => {
						string value = i.Item2.desc;
						try {
							return GlobalInstances.Yaml.Deserializer.Deserialize<string>(value);
						} catch (YamlDotNet.Core.YamlException) {
							// Stellaris localization files often contain
							// unescaped double quotes inside double-quoted
							// values. PDX's internal parser tolerates this
							// phenomenon; YamlDotNet does not.
							// As a fallback, strip surrounding quotes naively.
							return value.StartsWith('"') && value.EndsWith('"')
								? value[1..^1]
								: value;
						}
					}
				)
				.AsReadOnly();

			task?.Increment(1);
		});

		task?.StopTask();
		Localizations = L10n.AsReadOnly();
	}
}
