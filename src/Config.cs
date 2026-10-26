using Tomlyn;
using Tomlyn.Serialization;

namespace StlTechRelGen;

internal sealed partial class Config {
	// 已不再执行更新检查（原先指向上游仓库，对本工具无意义）。
	// 保留该类型仅为兼容旧版 atr-ttbundle.toml 里的 [update] 段。
	public sealed class UpdateConfig {
		/// <summary>兼容旧版 toml 的 check_update 键；工具已不执行更新检查。</summary>
		public bool CheckUpdate { get; set; } = true;
	}

	public sealed class GameConfig {
		public required string GamePath { get; set; }

		public required string DocumentPath { get; set; }
	}

	public sealed class PlaysetConfig {
		// Use empty string to indicate vanilla
		public required string Name { get; set; }

		// When vanilla, this is output path
		public required string Target { get; set; }
	}

	/// <summary>输出设置（TOML 的 [output] 段）。</summary>
	public sealed class OutputConfig {
		/// <summary>文件名前缀。zz 前缀保证加载顺序靠后、覆盖优先级最高。</summary>
		public string FilePrefix { get; set; } = "zz_techtree";

		/// <summary>要生成的语言；留空表示全部 10 种。</summary>
		public List<string> Languages { get; set; } = [];

		/// <summary>是否输出辅助片段文件（!xxx_&lt;lang&gt;.yml 里的标题文案）。</summary>
		public bool WriteFragments { get; set; } = true;

		/// <summary>目标 MOD（播放集内展示名）；空 = 输出到 target_dir 目录。</summary>
		public string TargetMod { get; set; } = "";

		/// <summary>输出目录；target_mod 为空时使用。</summary>
		public string TargetDir { get; set; } = "";
	}

	/// <summary>数据范围与过滤（TOML 的 [data] 段）。</summary>
	public sealed class DataConfig {
		/// <summary>vanilla = 只解析原版；其余值视作使用播放集（由 [playset].name 决定）。</summary>
		public string Mode { get; set; } = "vanilla";

		public string Playset { get; set; } = "";

		/// <summary>数据来源 MOD（播放集内展示名）；空 = 播放集里全部 MOD。</summary>
		public string SourceMod { get; set; } = "";

		/// <summary>是否把原版科技也纳入生成；false = 只生成 MOD 科技。</summary>
		public bool IncludeVanilla { get; set; } = true;

		/// <summary>排除事件科技（按名称前缀启发式识别：tech_event_*）。</summary>
		public bool ExcludeEventTechs { get; set; }

		/// <summary>排除网关科技（gateway = xxx 的条目）。</summary>
		public bool ExcludeGatewayTechs { get; set; }

		/// <summary>排除起始科技（start_tech = yes）。</summary>
		public bool ExcludeStartTechs { get; set; }
	}

	/// <summary>段落顺序（TOML 的 [sections] 段）。标签行固定在首行，不参与排序。</summary>
	public sealed class SectionsConfig {
		/// <summary>可选值：requires / unlocks / tree。</summary>
		public List<string> Order { get; set; } = ["requires", "unlocks", "tree"];
	}

	/// <summary>标签行设置（TOML 的 [labels] 段）。</summary>
	public sealed class LabelsConfig {
		public bool Enabled { get; set; } = true;

		public bool AreaIcon { get; set; } = true;

		public bool AreaName { get; set; } = true;

		public bool Tier { get; set; } = true;

		/// <summary>tier = 0（起始科技）显示成什么；留空则不显示层级。</summary>
		public string TierZero { get; set; } = "T0";

		public bool Category { get; set; } = true;

		public bool Rare { get; set; } = true;

		public bool Dangerous { get; set; } = true;

		public bool Repeatable { get; set; } = true;

		public bool Cost { get; set; }

		public bool Color { get; set; } = true;

		/// <summary>各标签之间的分隔符。</summary>
		public string Delimiter { get; set; } = " ";
	}

	/// <summary>前置/解锁列表设置（TOML 的 [lists] 段）。</summary>
	public sealed class ListsConfig {
		public bool Enabled { get; set; } = true;

		/// <summary>留空则沿用游戏内建的多语言片段 $techrel_requires$。</summary>
		public string? RequiresTitle { get; set; }

		/// <summary>留空则沿用游戏内建的多语言片段 $techrel_unlocks$。</summary>
		public string? UnlocksTitle { get; set; }

		/// <summary>留空则沿用游戏内建的多语言片段 $techrel_one_of$。</summary>
		public string? OneOfTitle { get; set; }

		public int MaxItems { get; set; } = 10;

		public string TruncateText { get; set; } = "…还有 {n} 项";

		/// <summary>列表项前是否带领域图标。</summary>
		public bool ShowAreaIcon { get; set; } = true;
	}

	// Automatically answers "yes" to confirmation prompts where possible,
	// reducing interactivity in automated / batch scenarios. Does not
	// bypass non-y/n prompts such as text input or list selection.
	[TomlIgnore(Condition = TomlIgnoreCondition.WhenWritingDefault)]
	public bool Yesmen { get; set; }

	public string? OverrideLanguage { get; set; }

	[TomlPropertyName("suppresses_cwtools_errors")]
	public bool SuppressesCWToolsErrors { get; set; } = true;

	public UpdateConfig Update { get; set; } = new();
	public required GameConfig Game { get; set; }
	public PlaysetConfig? Playset { get; set; }

	/// <summary>科技树段落配置（TOML 的 [tree] 段）。</summary>
	public TreeConfig Tree { get; set; } = new();

	/// <summary>输出设置（TOML 的 [output] 段）。</summary>
	public OutputConfig Output { get; set; } = new();

	/// <summary>数据范围（TOML 的 [data] 段）。</summary>
	public DataConfig Data { get; set; } = new();

	/// <summary>段落顺序（TOML 的 [sections] 段）。</summary>
	public SectionsConfig Sections { get; set; } = new();

	/// <summary>标签行（TOML 的 [labels] 段）。</summary>
	public LabelsConfig Labels { get; set; } = new();

	/// <summary>列表（TOML 的 [lists] 段）。</summary>
	public ListsConfig Lists { get; set; } = new();


	/// <summary>
	/// 把空值/非法值统一回退到默认，避免生成输出异常（空文件名前缀、树方向为空、
	/// 节点模板为空、列表标题/分隔符为空、条数与行数上限为 0 等）。
	/// 在读取 toml 与 UI 保存配置后调用。
	/// </summary>
	internal void Normalize() {
		// [output]
		if (string.IsNullOrWhiteSpace(Output.FilePrefix)) {
			Output.FilePrefix = "zz_techtree";
		}

		// [tree]
		if (string.IsNullOrWhiteSpace(Tree.Direction)) {
			Tree.Direction = "down";
		}

		if (string.IsNullOrWhiteSpace(Tree.Title)) {
			Tree.Title = "科技树:";
		}

		if (Tree.IndentWidth <= 0) {
			Tree.IndentWidth = 3;
		}

		if (Tree.MaxLines <= 0) {
			Tree.MaxLines = 100;
		}

		if (string.IsNullOrWhiteSpace(Tree.TruncateText)) {
			Tree.TruncateText = "…还有未显示的解锁项";
		}

		if (string.IsNullOrWhiteSpace(Tree.NodeTemplate)) {
			Tree.NodeTemplate = "{tech} (T{tier})";
		}

		// [labels]：分隔符为空会让标签粘连，回退成单个空格
		if (string.IsNullOrEmpty(Labels.Delimiter)) {
			Labels.Delimiter = " ";
		}

		// [lists]
		if (Lists.MaxItems <= 0) {
			Lists.MaxItems = 10;
		}

		if (string.IsNullOrWhiteSpace(Lists.TruncateText)) {
			Lists.TruncateText = "…还有 {n} 项";
		}

		// [data]
		if (string.IsNullOrWhiteSpace(Data.Mode)) {
			Data.Mode = "vanilla";
		}
	}

	internal static Config? Load() {
		try {
			using FileStream file = File.OpenRead(Constants.ConfigPath);
			Config config = TomlSerializer.Deserialize(file, TomlContext.Default.Config)!;
			config.Normalize();
			return config;
		} catch (FileNotFoundException) {
			return null;
		} catch (TomlException e) {
			LogError(Messages.Error.ConfigParseFailed);
			AnsiConsole.WriteException(e, ExceptionFormats.NoStackTrace);
			LogWarning(Messages.Prompt.GenerateNewConfig);
			AnsiConsole.WriteLine();
			return null;
		}
	}

	internal void Save() {
		using FileStream file = File.OpenWrite(Constants.ConfigPath);
		TomlSerializer.Serialize(file, this, TomlContext.Default.Options with {
			StringStylePreferences = new() {
				DefaultStyle = TomlStringStyle.Literal
			}
		});
	}
}
