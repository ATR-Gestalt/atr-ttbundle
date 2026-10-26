using System.Text;

using StlTechRelGen.Model;

namespace StlTechRelGen;

/// <summary>
/// 树渲染配置。对应 TOML 的 [tree] 段。
/// </summary>
internal sealed class TreeConfig {
	public bool Enabled { get; set; } = true;

	/// <summary>down = 只画下游；up = 只画前置链；both = 上下游都画。</summary>
	/// <summary>down / up / both；默认 both（上下游各成一段，展示完整科技树）。</summary>
	public string Direction { get; set; } = "both";

	public string Title { get; set; } = "科技树:";

	/// <summary>both 方向时的前置段标题。</summary>
	public string UpTitle { get; set; } = "[前置]";

	/// <summary>both 方向时的解锁段标题。</summary>
	public string DownTitle { get; set; } = "[解锁]";

	/// <summary>ascii（|--）或 unicode（├─ └─）。</summary>
	public string IndentStyle { get; set; } = "ascii";

	public int IndentWidth { get; set; } = 3;

	public int MaxLines { get; set; } = 100;

	public string TruncateText { get; set; } = "…还有未显示的解锁项";

	/// <summary>{tech} = 概念引用，{tier} = 层级数字，{icon} = 领域图标。</summary>
	public string NodeTemplate { get; set; } = "{tech} (T{tier})";

	/// <summary>按领域给节点上色（物理蓝/社会绿/工程橙）。</summary>
	public bool ColorByArea { get; set; } = true;
}

/// <summary>
/// 把「科技 + 它的上下游关系」渲染成缩进文本树。
///
/// 采用与科技树 MOD（codexvn/TechnologyTree）一致的规则：
/// 每个父节点下都完整展开（expand_all），不做重复折叠——实测原版数据下
/// 重复在 594 个科技里只出现在 6 个、最多多 2 行，折叠得不偿失。
///
/// 所有科技名一律输出 <c>['technology:xxx']</c> 概念引用，由游戏负责显示
/// 本地化名字与图标，因此本渲染器不接触翻译文本。
/// </summary>
internal static class TreeRenderer {
	/// <summary>渲染一棵树；无内容时返回空字符串。</summary>
	public static string Render(
		string rootId,
		Tech root,
		TechTable table,
		TreeConfig cfg
	) {
		if (!cfg.Enabled) {
			return string.Empty;
		}

		// 显式关闭：direction = none 表示选「不画树」，与段落开关等效
		string direction = cfg.Direction?.Trim().ToLowerInvariant() ?? "down";
		if (direction is "none" or "off" or "no" or "false") {
			return string.Empty;
		}

		bool up = direction is "up" or "both";
		bool down = direction is "down" or "both";

		List<string> upLines = [];
		bool upTruncated = false;
		if (up) {
			RenderUp(root, table, cfg, upLines, 1, ref upTruncated);
		}

		List<string> downLines = [];
		bool downTruncated = false;
		if (down) {
			RenderDown(rootId, root, table, cfg, downLines, 1, ref downTruncated);
		}

		if (upLines.Count == 0 && downLines.Count == 0) {
			return string.Empty;
		}

		StringBuilder sb = new();
		if (up && down) {
			// both：拆成「前置段」「解锁段」两棵树，各自带分段标题
			if (upLines.Count > 0) {
				sb.Append(cfg.UpTitle);
				foreach (string line in upLines) {
					sb.Append('\n').Append(line);
				}
			}

			if (downLines.Count > 0) {
				if (sb.Length > 0) {
					sb.Append("\n\n");
				}

				sb.Append(cfg.DownTitle);
				foreach (string line in downLines) {
					sb.Append('\n').Append(line);
				}
			}
		} else {
			List<string> lines = up ? upLines : downLines;
			for (int i = 0; i < lines.Count; i++) {
				if (i > 0) {
					sb.Append('\n');
				}

				sb.Append(lines[i]);
			}
		}

		return sb.ToString();
	}

	/// <summary>向下展开：列出它能解锁的科技。</summary>
	private static void RenderDown(
		string techId,
		Tech tech,
		TechTable table,
		TreeConfig cfg,
		List<string> lines,
		int depth,
		ref bool truncated
	) {
		// 按 tier、再按 id 排序，保证不同机器上输出稳定、玩家阅读顺
		IEnumerable<string> children = tech.Unlocks
			.OrderBy(id => table.Techs.TryGetValue(id, out Tech? t) ? t.Tier : int.MaxValue)
			.ThenBy(id => id, StringComparer.Ordinal);

		foreach (string childId in children) {
			if (truncated) {
				return;
			}

			if (TryEmit(childId, table, cfg, lines, depth, ref truncated)) {
				if (table.Techs.TryGetValue(childId, out Tech? child)) {
					RenderDown(childId, child, table, cfg, lines, depth + 1, ref truncated);
				}
			}
		}
	}

	/// <summary>向上展开：列出它的前置科技。</summary>
	private static void RenderUp(
		Tech tech,
		TechTable table,
		TreeConfig cfg,
		List<string> lines,
		int depth,
		ref bool truncated
	) {
		foreach (TechRequirement requirement in tech.Requires) {
			foreach (string parentId in requirement.Requirements()) {
				if (truncated) {
					return;
				}

				if (TryEmit(parentId, table, cfg, lines, depth, ref truncated)) {
					if (table.Techs.TryGetValue(parentId, out Tech? parent)) {
						RenderUp(parent, table, cfg, lines, depth + 1, ref truncated);
					}
				}
			}
		}
	}

	/// <summary>输出一行；返回是否还能继续向下展开（到上限就停）。</summary>
	private static bool TryEmit(
		string techId,
		TechTable table,
		TreeConfig cfg,
		List<string> lines,
		int depth,
		ref bool truncated
	) {
		if (truncated) {
			return false;
		}

		if (lines.Count >= cfg.MaxLines) {
			// 只写一次截断提示，之后整棵树停止展开
			lines.Add(Indent(cfg, depth - 1) + cfg.TruncateText);
			truncated = true;
			return false;
		}

		if (!table.Techs.TryGetValue(techId, out Tech? tech)) {
			// 前置指向了不存在的科技（被别的 MOD 覆盖或拼写问题），跳过
			return false;
		}

		string node = cfg.NodeTemplate
			.Replace("{tech}", Concept(techId), StringComparison.Ordinal)
			.Replace(
				"{tier}",
				tech.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture),
				StringComparison.Ordinal
			)
			.Replace("{icon}", AreaIcon(tech), StringComparison.Ordinal);

		if (cfg.ColorByArea) {
			node = AreaColor(tech) + node + "§!";
		}

		lines.Add(Indent(cfg, depth) + node);
		return lines.Count < cfg.MaxLines;
	}

	/// <summary>
	/// 按层级生成缩进前缀，格式与科技树 MOD 对齐：
	/// depth=1 → <c>   |-- </c>；depth=2 → <c>   |   |-- </c>。
	/// </summary>
	private static string Indent(TreeConfig cfg, int depth) {
		if (depth <= 0) {
			return new string(' ', cfg.IndentWidth);
		}

		bool unicode = string.Equals(cfg.IndentStyle, "unicode", StringComparison.OrdinalIgnoreCase);

		StringBuilder sb = new();
		for (int i = 0; i < depth; i++) {
			sb.Append(unicode ? "│  " : "   |");
		}

		sb.Append(unicode ? "├─ " : "-- ");
		return sb.ToString();
	}

	private static string Concept(string techId) => $"['technology:{techId}']";

	private static string AreaIcon(Tech tech) => tech.Area switch {
		TechArea.Physics => "£physics£ ",
		TechArea.Society => "£society£ ",
		TechArea.Engineering => "£engineering£ ",
		_ => string.Empty,
	};

	private static string AreaColor(Tech tech) => tech.Area switch {
		TechArea.Physics => "§B",
		TechArea.Society => "§G",
		TechArea.Engineering => "§O",
		_ => "§Y",
	};
}
