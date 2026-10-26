namespace StlTechRelGen.Utils;

/// <summary>
/// 让进度任务在「没有控制台」的场景下也能跑。
///
/// CLI 走 Spectre.Console 的进度条；GUI / 本地 Web UI 没有可渲染的控制台，
/// 传 <c>null</c> 进来即可，所有进度调用自动变成空操作。
/// </summary>
internal static class ProgressExt {
	/// <summary>安全地创建进度任务；ctx 为 null 时返回 null。</summary>
	public static ProgressTask? Task(ProgressContext? ctx, string description) =>
		ctx?.AddTask(description);
}
