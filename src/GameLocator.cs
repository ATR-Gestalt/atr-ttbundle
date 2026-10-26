namespace StlTechRelGen;

/// <summary>
/// 尝试自动定位群星安装目录。
///
/// 上游用的是 GameFinder 包，但那个包是 **GPL-3.0**，会波及整个程序的分发许可，
/// 所以这里换成零依赖的常见路径探测：找到就用，找不到就让用户手填
/// （界面与向导本来都提供输入框）。
/// </summary>
internal static class GameLocator {
	private const string GameFolder = "Stellaris";

	/// <summary>返回找到的游戏根目录；找不到返回 null。</summary>
	public static string? TryFind() {
		foreach (string candidate in Candidates()) {
			try {
				if (Directory.Exists(candidate)) {
					return candidate;
				}
			} catch {
				// 无权限访问的盘符/目录直接跳过
			}
		}

		return null;
	}

	private static IEnumerable<string> Candidates() {
		if (OperatingSystem.IsWindows()) {
			// 1. 常见安装位置
			yield return @"C:\Program Files (x86)\Steam\steamapps\common\" + GameFolder;
			yield return @"C:\Program Files\Steam\steamapps\common\" + GameFolder;

			// 2. 各盘符下的 Steam 库
			foreach (DriveInfo drive in DriveInfo.GetDrives()) {
				string root;
				try {
					root = drive.RootDirectory.FullName;
				} catch {
					continue;
				}

				yield return Path.Combine(root, "SteamLibrary", "steamapps", "common", GameFolder);
				yield return Path.Combine(root, "Steam", "steamapps", "common", GameFolder);
				yield return Path.Combine(root, "Games", "SteamLibrary", "steamapps", "common", GameFolder);
			}

			yield break;
		}

		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (OperatingSystem.IsMacOS()) {
			yield return Path.Combine(
				home, "Library/Application Support/Steam/steamapps/common", GameFolder
			);
			yield break;
		}

		// Linux
		yield return Path.Combine(home, ".steam/steam/steamapps/common", GameFolder);
		yield return Path.Combine(home, ".local/share/Steam/steamapps/common", GameFolder);
		yield return Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam/steamapps/common", GameFolder);
	}
}
