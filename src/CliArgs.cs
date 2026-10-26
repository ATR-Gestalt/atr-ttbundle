using System.ComponentModel;

using Spectre.Console.Cli;

namespace StlTechRelGen;

internal sealed class CliArgs : Command<CliArgs.Settings> {
	internal sealed class Settings : CommandSettings {
		[CommandOption("-g|--game-path")]
		[Description("Path to the game")]
		public string? GamePath { get; init; }

		[CommandOption("-d|--document-path")]
		[Description("Path to the document")]
		public string? DocumentPath { get; init; }

		[CommandOption("-p|--playset", isRequired: true)]
		[Description("Playset to use")]
		public required string Playset { get; init; }

		[CommandOption("-m|--target-mod", isRequired: true)]
		[Description("Target mod to write localization to")]
		public required string TargetMod { get; init; }

		[CommandOption("-s|--source-mod")]
		[Description("Playset mode only: parse only this MOD (display name)")]
		public string? SourceMod { get; init; }

		[CommandOption("--mod-only")]
		[Description("Do not generate localization for vanilla techs (MOD techs only)")]
		public bool ModOnly { get; init; }

		[CommandOption("--no-check-update")]
		[Description("Don't check for updates")]
		public bool NoCheckUpdate { get; init; } = false;

		public Config ToConfig() => new() {
			Yesmen = true,
			Game = new Config.GameConfig {
				GamePath = GamePath ?? GameLocator.TryFind()
					?? throw new NotSupportedException(
						"Game path is not provided and failed to be found"
						+ " automatically"
					),
				DocumentPath = DocumentPath ?? Constants.DefaultDocumentPath
					?? throw new NotSupportedException(
						"Document path is not provided and cannot be"
						+ " determined automatically for this OS"
					),
			},
			Data = new Config.DataConfig {
				Mode = string.IsNullOrEmpty(Playset) ? "vanilla" : "playset",
				Playset = Playset,
				SourceMod = SourceMod ?? "",
				IncludeVanilla = !ModOnly,
			},
			Output = new Config.OutputConfig {
				TargetMod = string.IsNullOrEmpty(Playset) ? "" : TargetMod,
				TargetDir = string.IsNullOrEmpty(Playset) ? TargetMod : "",
			},
			Update = new Config.UpdateConfig {
				CheckUpdate = !NoCheckUpdate,
			},
		};
	}

	internal static Settings? Arguments { get; private set; }

	// We only utilize argument parsing feature here
	protected override int Execute(
		CommandContext ctx,
		Settings arguments,
		CancellationToken ct
	) {
		Arguments = arguments;
		return 0;
	}
}
