using Tomlyn;

namespace StlTechRelGen;

internal sealed class Lang {
	private const string DEFAULT_LANG = "en";

	internal static Lang Messages { get; private set; } = null!;

	static Lang() => LoadMessages(
		CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch {
			"en" => "en",
			"zh" => "zh",
			_ => DEFAULT_LANG
		}
	);

	internal static void LoadMessages(string? lang) =>
		Messages = TomlSerializer.Deserialize(
			Assembly.GetExecutingAssembly()
				.GetManifestResourceStream(
					$"{nameof(StlTechRelGen)}"
					+ $".Resources.lang.{lang}.toml"
				)!,
			TomlContext.Default.Lang
		)!;

	public required PromptMessages Prompt { get; init; }
	public required ProgressMessages Progress { get; init; }
	public required DataMessages Data { get; init; }
	public required ErrorMessages Error { get; init; }


	internal sealed class PromptMessages {
		public required string SearchPlaceholderText { get; init; }
		public required string MoreChoicesText { get; init; }

		public required string GenerateNewConfig { get; init; }

		public required string Welcome { get; init; }
		public required string ReadInstructions { get; init; }
		public required string Pause { get; init; }

		public required string AskGamePath { get; init; }
		public required string AskDocumentPath { get; init; }
		public required string ConfigCreated { get; init; }

		public required string CloseLauncher { get; init; }
		public required CompositeFormat ConfirmUseSavedVanilla { get; init; }
		public required CompositeFormat ConfirmUseSavedTarget { get; init; }
		public required string ChoosePlayset { get; init; }
		public required string CurrentPlaysetPrefix { get; init; }
		public required string AskOutputPath { get; init; }
		public required string ChooseTargetMod { get; init; }
		public required string TargetModCollisionHeader { get; init; }
		public required CompositeFormat TargetModCollisionItem { get; init; }
		public required CompositeFormat ConfirmSaveVanilla { get; init; }
		public required CompositeFormat ConfirmSaveTarget { get; init; }

		public required CompositeFormat SavedL10n { get; init; }
		public required CompositeFormat GenerationSummary { get; init; }
	}

	internal sealed class ProgressMessages {
		public required string ConnectingLauncherDb { get; init; }
		public required string LoadingGame { get; init; }
		public required string LoadingScriptedVariables { get; init; }
		public required string LoadingAuthoritySuffixes { get; init; }
		public required string LoadingTechnologies { get; init; }
		public required string BuildingTechRelationship { get; init; }
		public required string LoadingL10n { get; init; }
		public required string GeneratingL10n { get; init; }
		public required string WritingL10nFragments { get; init; }
		public required string WritingL10n { get; init; }
	}

	internal sealed class DataMessages {
		public required CompositeFormat TechNoCategory { get; init; }
		public required CompositeFormat L10nEntryNotFound { get; init; }
		public required CompositeFormat L10nEntryAndSwapNotFound { get; init; }
		public required CompositeFormat TechUnknownPrereq { get; init; }
		public required CompositeFormat TechUnnamedSwap { get; init; }
		public required CompositeFormat L10nCycleReference { get; init; }
	}

	internal sealed class ErrorMessages {
		public required string ConfigParseFailed { get; init; }
		public required string PlaysetNotFound { get; init; }
		public required string TargetModNotFound { get; init; }
		public required string MultipleModsSameName { get; init; }
		public required CompositeFormat FailedToAccessOutputDir { get; init; }
	}
}
