namespace StlTechRelGen.Utils;

internal static class LogHelper {
	public static void Log(string message) =>
		AnsiConsole.MarkupLine(message);

	public static void Log(CompositeFormat format, params ReadOnlySpan<object?> args) =>
		AnsiConsole.MarkupLine(string.Format(CultureInfo.InvariantCulture, format, args));

	private static void LogWithColor(Color color, string message) =>
		AnsiConsole.MarkupLine($"[{color.ToMarkup()}]{message}[/]");

	private static void LogWithColor(Color color, CompositeFormat format, params ReadOnlySpan<object?> args) =>
		LogWithColor(color, string.Format(CultureInfo.InvariantCulture, format, args));

	public static void LogWarning(string message) =>
		LogWithColor(Color.Yellow, message);

	public static void LogWarning(CompositeFormat format, params ReadOnlySpan<object?> args) =>
		LogWithColor(Color.Yellow, format, args);

	public static void LogError(string message) =>
		LogWithColor(Color.Red, message);

	public static void LogError(CompositeFormat format, params ReadOnlySpan<object?> args) =>
		LogWithColor(Color.Red, format, args);
}
