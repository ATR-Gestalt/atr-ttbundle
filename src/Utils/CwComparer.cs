using static CWTools.Games.Files;

namespace StlTechRelGen.Utils;

internal sealed class CWComparer(
	WorkspaceDirectory GameDir,
	WorkspaceDirectoryInput[] ModDirs
) : IComparer<CWNode>,
	IComparer<CWTools.Localisation.Entry>,
	IComparer<CWRange> {
	public string GamePath => GameDir.path;

	public string[] ModPaths { get; } = [.. ModDirs.Select(
		dir => dir switch {
			WorkspaceDirectoryInput.WD wd => wd.Item.path,
			WorkspaceDirectoryInput.ZD zd => zd.Item.path,
			_ => throw new ArgumentOutOfRangeException(nameof(ModDirs)),
		}
	)];

	public bool IsVanilla(CWRange range) =>
		range.FileName.StartsWith(GamePath, StringComparison.OrdinalIgnoreCase);

	// Find which mod root shares the longest path prefix with this file.
	// This is a greedy heuristic: the mod whose root path has the most
	// characters in common with the file path is assumed to own the file.
	public int ModIndex(CWRange range) => ModPaths.Index()
		.MaxBy((i) => range.FileName.CommonPrefix(
			i.Item, StringComparison.OrdinalIgnoreCase
		).Length)
		.Index;

	public int Compare(CWNode? x, CWNode? y) {
		if (x != null && y != null) {
			return Compare(x.Position, y.Position);
		} else if (x != null && y == null) {
			return 1;
		} else if (x == null && y != null) {
			return -1;
		}

		return 0;
	}

	public int Compare(
		CWTools.Localisation.Entry x,
		CWTools.Localisation.Entry y
	) =>
		Compare(x.position, y.position);

	// Three-tier sort order matching Stellaris mod override rules
	// (see https://stellaris.paradoxwikis.com/Modding#Overwriting_specific_elements):
	//   1. Vanilla files come before all mod files.
	//   2. Within vanilla, sort alphabetically by file name.
	//   3. Within mods, sort by relative file name first,
	//      then by mod load order (later mods override earlier ones).
	public int Compare(CWRange x, CWRange y) {
		// Determine whether each range belongs to the vanilla (base game) directory
		bool isVanillaX = IsVanilla(x);
		bool isVanillaY = IsVanilla(y);

		if (isVanillaX && isVanillaY) {
			// Both are vanilla: sort alphabetically by file name
			return string.Compare(
				x.FileName,
				y.FileName,
				StringComparison.OrdinalIgnoreCase
			);
		} else if (isVanillaX || isVanillaY) {
			// One is vanilla, one is from a mod: vanilla always comes first
			return isVanillaX.CompareTo(isVanillaY);
		}

		// Both are from mods: find the best-matching mod root for each,
		// compare their relative paths, and tie-break by mod index
		int modIndexX = ModIndex(x);
		int modIndexY = ModIndex(y);
		return string.Compare(
			x.FileName[ModPaths[modIndexX].Length..],
			y.FileName[ModPaths[modIndexY].Length..],
			StringComparison.OrdinalIgnoreCase
		) switch {
			0 => modIndexX.CompareTo(modIndexY),
			int i => i
		};
	}
}
