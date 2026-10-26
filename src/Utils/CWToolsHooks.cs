namespace StlTechRelGen.Utils;

/// <summary>
/// 原本用 Harmony 给 CWTools 的 updateFiles / updateAllLocalisationSources /
/// updateProcessedLocalisation 三个方法挂 postfix，用来推进 Spectre 进度条。
///
/// 但 HarmonyX 依赖的 MonoMod 在 self-contained 单文件发布下找不到 clrjit，
/// 启动时会抛 <c>PlatformNotSupportedException: Could not locate clrjit library</c>，
/// 整个程序直接崩掉。
///
/// 本工具的首要目标是「玩家拿到 exe 双击就能用、不用装 .NET 运行时」，
/// 所以这里放弃细粒度进度回调，改为空实现：功能完全不受影响，
/// 只是加载阶段进度条不再逐格推进。
/// </summary>
internal static class CWToolsHooks {
	/// <summary>保留字段以兼容调用方；没有 patch 就不会被触发。</summary>
	internal static Action? OnProgress;

	/// <summary>空实现：不再对 CWTools 做任何方法级别的 patch。</summary>
	internal static void Init() {
		// 故意留空
	}
}
