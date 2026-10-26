using Spectre.Console.Rendering;

namespace StlTechRelGen.Utils;

/// <summary>
/// A column showing the elapsed time of a task. Copied from Spectre.Console's
/// ElapsedTimeColumn with the hour display removed (this tool typically runs
/// for only a few minutes, so hours are unnecessary). Shows <c>**:**</c>
/// when elapsed time exceeds the two-digit minute display.
/// </summary>
public sealed class ShortElapsedTimeColumn : ProgressColumn {
	/// <inheritdoc/>
	protected override bool NoWrap => true;

	/// <summary>
	/// Gets or sets the style of the remaining time text.
	/// </summary>
	public Style Style { get; set; } = Color.Blue;

	/// <inheritdoc/>
	public override IRenderable Render(
		RenderOptions options,
		ProgressTask task,
		TimeSpan deltaTime
	) {
		TimeSpan? elapsed = task.ElapsedTime;
		if (elapsed == null) {
			return new Markup("--:--");
		}

		if (elapsed.Value.TotalMinutes > 99) {
			return new Markup("**:**");
		}

		return new Text($"{elapsed.Value:mm\\:ss}", Style);
	}

	/// <inheritdoc/>
	public override int? GetColumnWidth(RenderOptions options) => "**:**".Length;
}
