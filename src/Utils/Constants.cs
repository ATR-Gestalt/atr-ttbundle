namespace StlTechRelGen.Utils;

internal static class Constants {
	public static string ConfigPath { get; } =
		Path.ChangeExtension(Environment.ProcessPath!, "toml");

	// https://stellaris.paradoxwikis.com/Modding#Mod_folder_location
	// 注意：.NET 不会展开 "~"，必须用 UserProfile 拼绝对路径，否则 Linux/macOS 上取不到文件
	public static string? DefaultDocumentPath {
		get {
			string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (OperatingSystem.IsWindows()) {
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"Paradox Interactive\Stellaris\"
				);
			} else if (OperatingSystem.IsMacOS()) {
				return Path.Combine(
					home, "Documents/Paradox Interactive/Stellaris"
				);
			} else if (OperatingSystem.IsLinux()) {
				return Path.Combine(
					home, ".local/share/Paradox Interactive/Stellaris"
				);
			} else {
				return null;
			}
		}
	}

	// Hardcoded in CWTools to identify vanilla dir
	// https://github.com/cwtools/cwtools/blob/b377453dee803f9258be92cfc49896d09039702d/CWTools/Game/Stellaris/STLGame.fs#L551
	public static string GameDirName { get; } = "stellaris";


	public static class L10n {
		public const string Repeatable = "$techrel_repeatable$";
		public const string Requires = "$techrel_requires$";
		public const string Unlocks = "$techrel_unlocks$";
		public const string OneOf = "$techrel_one_of$";

		public const char Times = '×';
		public const string Bullet = "$BULLET_POINT$";
		public const string Mod = "[Mod] ";
		public const string LParen = " (";
		public const char RParen = ')';
		public const char Sep = ' ';
		public const string Dangerous = "$TECH_IS_DANGEROUS$";
		public const string Rare = "$TECH_IS_RARE$";
	}
}
