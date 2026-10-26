using StlTechRelGen.Model;

namespace StlTechRelGen;

internal sealed class L10nBuilder(GameData gameData, Config config) {
	public Dictionary<string, Dictionary<string, string>> Generated { get; } =
		LangHelpers.allSTLLangs
			.Where(l => config.Output.Languages.Count == 0
				|| config.Output.Languages.Contains(l.Name(), StringComparer.OrdinalIgnoreCase))
			.ToDictionary(
				i => i.Name(),
				_ => new Dictionary<string, string>()
			);

	/// <summary>按 [data] 段的过滤规则判断这条科技是否要生成内容。</summary>
	private bool ShouldSkip(string id, Tech tech) {
		// 事件科技没有统一字段可判定，这里按常见命名前缀启发式识别
		if (config.Data.ExcludeEventTechs
			&& id.StartsWith("tech_event_", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		if (config.Data.ExcludeGatewayTechs && tech.Gateway is not null) {
			return true;
		}

		if (config.Data.ExcludeStartTechs && tech.StartTech) {
			return true;
		}

		return false;
	}

	/// <summary>层级文本：tier = 0 且配置了 tier_zero 时用自定义文本。</summary>
	private string BuildTierText(Tech tech) {
		if (!config.Labels.Tier) {
			return string.Empty;
		}

		if (tech.Tier == 0 && !string.IsNullOrEmpty(config.Labels.TierZero)) {
			return config.Labels.TierZero;
		}

		return $"T{tech.Tier.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
	}

	/// <summary>标签行尾部的标记（稀有 / 危险 / 可重复 / 成本），带前导分隔符。</summary>
	private string BuildLabelFlags(Tech tech) {
		StringBuilder sb = new();
		string sep = config.Labels.Delimiter;

		if (config.Labels.Dangerous && tech.Dangerous) {
			sb.Append(sep).Append(Constants.L10n.Dangerous);
		}

		if (config.Labels.Rare && tech.Rare) {
			sb.Append(sep).Append(Constants.L10n.Rare);
		}

		if (config.Labels.Repeatable && tech.Levels != 1) {
			sb.Append(sep).Append(Constants.L10n.Repeatable);
			if (tech.Levels != -1) {
				sb.Append(Constants.L10n.Times)
					.Append(tech.Levels.ToString(System.Globalization.CultureInfo.InvariantCulture));
			}
		}

		if (config.Labels.Cost && tech.Cost > 0) {
			sb.Append(sep).Append('§').Append('Y')
				.Append(tech.Cost.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append("§!");
		}

		return sb.ToString();
	}

	/// <summary>「需要下列科技」段落。</summary>
	private string BuildRequiresSection(Tech tech) {
		if (!config.Lists.Enabled || tech.Requires.Count == 0) {
			return string.Empty;
		}

		StringBuilder sb = new();
		sb.Append(config.Lists.RequiresTitle ?? Constants.L10n.Requires);

		int written = 0;
		foreach (TechRequirement require in tech.Requires) {
			if (written >= config.Lists.MaxItems) {
				AppendListTruncation(sb, tech.Requires.Count - written);
				break;
			}

			if (require is TechRequirementSingle single) {
				if (WriteRelatedTech(sb, single.Id, 1)) {
					written++;
				}
			} else if (require is TechRequirementAlternatives alternatives) {
				sb.Append("\n$t$")
					.Append(Constants.L10n.Bullet)
					.Append(config.Lists.OneOfTitle ?? Constants.L10n.OneOf);

				foreach (string alternative in alternatives.Alternatives) {
					if (written >= config.Lists.MaxItems) {
						break;
					}

					if (WriteRelatedTech(sb, alternative, 2)) {
						written++;
					}
				}
			} else {
				throw new NotSupportedException();
			}
		}

		return sb.ToString();
	}

	/// <summary>「解锁下列科技」段落。</summary>
	private string BuildUnlocksSection(Tech tech) {
		if (!config.Lists.Enabled || tech.Unlocks.Count == 0) {
			return string.Empty;
		}

		StringBuilder sb = new();
		sb.Append(config.Lists.UnlocksTitle ?? Constants.L10n.Unlocks);

		int written = 0;
		foreach (string unlock in tech.Unlocks) {
			if (written >= config.Lists.MaxItems) {
				AppendListTruncation(sb, tech.Unlocks.Count - written);
				break;
			}

			if (WriteRelatedTech(sb, unlock, 1)) {
				written++;
			}
		}

		return sb.ToString();
	}

	/// <summary>列表超限提示。</summary>
	private void AppendListTruncation(StringBuilder sb, int remaining) {
		if (remaining <= 0) {
			return;
		}

		string text = config.Lists.TruncateText.Replace(
			"{n}",
			remaining.ToString(System.Globalization.CultureInfo.InvariantCulture),
			StringComparison.Ordinal
		);
		sb.Append("\n$t$").Append(Constants.L10n.Bullet).Append(text);
	}

	/// <summary>科技树段落。</summary>
	private string BuildTreeSection(string id, Tech tech) {
		if (!config.Tree.Enabled) {
			return string.Empty;
		}

		string tree = TreeRenderer.Render(id, tech, gameData.TechTable, config.Tree);
		return tree.Length > 0 ? config.Tree.Title + "\n" + tree : string.Empty;
	}

	public void BuildTech(string id, Tech tech) {
		// 仅解析 MOD：原版科技保留在 TechTable 里供树渲染引用，但不生成本地化
		if (!config.Data.IncludeVanilla && tech.Vanilla) {
			return;
		}

		if (ShouldSkip(id, tech)) {
			return;
		}

		// 段落正文：按 [sections].order 顺序拼接（标签行固定在首行）
		StringBuilder body = new();
		foreach (string section in config.Sections.Order) {
			string text = section.ToLowerInvariant() switch {
				"requires" => BuildRequiresSection(tech),
				"unlocks" => BuildUnlocksSection(tech),
				"tree" => BuildTreeSection(id, tech),
				_ => string.Empty,
			};

			if (text.Length > 0) {
				body.Append("\n\n").Append(text);
			}
		}

		// 标签行后半段：稀有/危险/可重复/成本，受 [labels] 控制
		string flags = config.Labels.Enabled ? BuildLabelFlags(tech) : string.Empty;
		string labelClose = config.Labels.Enabled
			? Constants.L10n.RParen + "§!"
			: string.Empty;

		string contentMain = flags + labelClose + body.ToString();
		// This includes metadata that's not affected by swaps + relations
		string descKey = $"{id}_desc";

		// Treat the base tech as a swap of itself so the base entry and
		// every real swap entry share one generation path. AsSwap()
		// copies only area, categories, and vanilla — the three
		// properties a technology_swap block may override.
		foreach ((string swapId, TechSwap swapTech) in
			tech.Swaps.Prepend(new(id, tech.AsSwap()))
		) {
			// Some metadata might be changed by swaps, generate
			// individual copies for each swap
			string contentSwap = config.Labels.Enabled
				? $"\n\n£{swapTech.Area.ToString().ToLowerInvariant()}£"
					+ $" §Y${swapTech.Area.ToString().ToUpperInvariant()}$ "
					+ $"{BuildTierText(tech)}{Constants.L10n.LParen}"
					+ swapTech.Categories
						.Where(_ => config.Labels.Category)
						.Select(i => $"${i.ToLowerInvariant()}$")
						.Join(Constants.L10n.Sep)
					+ contentMain
				: contentMain; // 标签行关闭时只输出正文

			foreach ((CWLang lang, ReadOnlyDictionary<string, string> loc) in
				gameData.Localizations
			) {
				if (!Generated.TryGetValue(lang.Name(), out Dictionary<string, string>? dict)) {
					// 该语言没在 [output].languages 里，跳过
					continue;
				}
				foreach (string suffix in gameData.AuthSuffixes) {
					string descKeySwap = $"{swapId}_desc";
					string descKeySwapFull = $"{descKeySwap}{suffix}";

					if (suffix.Length > 0 && !loc.ContainsKey(descKeySwapFull)) {
						// suffix.Length > 0 => Auth specific desc
						// (Since we always want basic desc)
						// ContainsKey false => This auth has no specific desc
						continue;
					}

					// Currently use Swap desc w/ suffix => Orig desc w/o suffix fallback
					// Not sure if this is correct and covers all cases
					string descKeyFinal = descKeySwapFull;
					if (!loc.TryGetValue(descKeySwapFull, out string? descOrig)) {
						descKeyFinal = descKey;
						if (!loc.TryGetValue(descKey, out descOrig)) {
							// Loc not found, use key directly
							// We don't skip directly in order to provide info
							// at best-effort
							descOrig = descKeySwapFull;

							if (descKey == descKeySwapFull) {
								LogWarning(Messages.Data.L10nEntryNotFound, descKey);
							} else {
								LogWarning(
									Messages.Data.L10nEntryAndSwapNotFound,
									descKey,
									descKeySwapFull
								);
							}
						}
					}

					// Repeatedly resolve $key$ references until stable. A
					// resolved description may itself introduce new references
					// (rare case).
					// keysResolved is used to detect circular references.
					HashSet<string> keysResolved = [];
					string resolvedText = descOrig;
					while (true) {
						(resolvedText, string[] keysFound) = ResolveLocReferences(
							resolvedText,
							loc,
							gameData.TechTable.AllTechIds,
							gameData.AuthSuffixes
						);

						// No loc keys found → all references resolved
						// transitively.
						// Also covers the case where descOrig had zero refs.
						if (keysFound.Length == 0) {
							break;
						}

						// A key resolved in an earlier iteration reappeared
						// after resolution → circular chain (A→B→A or A→A
						// self-ref). Stop to avoid infinite loop; remaining
						// refs stay as-is.
						if (keysFound.Intersect(keysResolved).Any()) {
							LogWarning(
								Messages.Data.L10nCycleReference,
								descKeyFinal
							);
							break;
						}

						keysResolved.UnionWith(keysFound);
					}

					string descFinal = resolvedText + contentSwap;

					// Skip if the final text is identical to the base
					// (non-swap) entry. This commonly happens when
					// authority-specific suffixes produce the same
					// resolved description — writing duplicates would
					// bloat output.
					if (!dict.TryGetValue(descKey, out string? baseDesc)
						|| baseDesc != descFinal
					) {
						dict[descKeySwapFull] = descFinal;
					}
				}
			}
		}
	}

	/// <summary>输出一条列表项；返回是否真的写了（未知名/被过滤的科技返回 false）。</summary>
	private bool WriteRelatedTech(StringBuilder sb, string relTechId, int indent) {
		if (!gameData.TechTable.Techs.TryGetValue(relTechId, out Tech? tech)) {
			// Warning is handled by TechTable, so we simply skip here.
			return false;
		}

		if (ShouldSkip(relTechId, tech)) {
			return false;
		}

		sb.Append("\n$");
		for (int i = 0; i < indent; i++) {
			sb.Append('t');
		}

		sb.Append('$');

		sb.Append(Constants.L10n.Bullet);
		if (!tech.Vanilla) {
			sb.Append(Constants.L10n.Mod);
		}

		if (config.Lists.ShowAreaIcon) {
			sb.Append('£')
				.Append(tech.Area.ToString().ToLowerInvariant())
				.Append('£');
		}

		sb.Append(" ['technology:")
			.Append(relTechId)
			.Append("']");

		return true;
	}

	// Scans text for $key$ references, separates them into resolvable and
	// cycled groups, resolves the resolvable ones, and reports both groups
	// so the call site can handle cycles (log warning, stop iteration).
	// Mutates previouslyResolved by adding newly resolved keys.
	private static (string text, string[] foundLocKeys) ResolveLocReferences(
		string text,
		ReadOnlyDictionary<string, string> loc,
		ReadOnlySet<string> knownTechIds,
		ReadOnlyCollection<string> authSuffixes
	) {
		// A tech description may reference another tech's description via loc keys.
		// Without resolving these references, the output incorrectly nests the
		// referenced tech's relation info inside the current tech's description:
		//   [referenced tech's orig desc] + [referenced tech's relation info]
		//   + [this tech's relation info]
		// We resolve by replacing loc key references with the referenced tech's
		// own original description (which doesn't include its relation info).
		// Note: this changes loc behavior in edge cases (nested references).
		// Unknown techs are left as-is; this is safe as long as generated loc
		// is kept up-to-date.
		string[] foundLocKeys = [.. GlobalInstances.RegexLocReference()
			.Matches(text)
			// 1. Only resolve references to techs we actually know about
			.Where(i => knownTechIds.Contains(i.Groups["id"].Value))
			// 2. Skip auth-suffixed references pointing to non-existing suffixes
			.Where(i => !i.Groups["suffix"].Success
				|| authSuffixes.Contains(i.Groups["suffix"].Value)
			)
			// 3. Extract the full loc key (e.g., "tech_foo_desc")
			.Select(i => i.Groups["key"].Value)
			// 4. Deduplicate — multiple references may resolve to the same key
			.Distinct()
			// 5. Skip loc keys we have no localization for (guard for missing data)
			.Where(loc.ContainsKey)
		];

		string resolved = foundLocKeys.Aggregate(
			text,
			(desc, key) => desc.Replace($"${key}$", loc[key])
		);

		return (resolved, foundLocKeys);
	}

	public void WriteFilesWithProgress(ProgressContext? ctx, string destPath) {
		ProgressTask? task = ProgressExt.Task(ctx, Messages.Progress.WritingL10n)?
			.MaxValue(Generated.Keys.Count);
		Parallel.ForEach(Generated.Keys, (lang) => {
			WriteLangFile(destPath, lang);
			task?.Increment(1);
		});
		task?.StopTask();
	}

	private void WriteLangFile(string destPath, string lang) {
		Dictionary<string, string> dict = Generated[lang];

		using StreamWriter writer = new(
			// 文件名必须保留 _l_ 标记：Paradox 靠它识别语言，缺了就加载不到
			Path.Combine(destPath, $"{config.Output.FilePrefix}_l_{lang}.yml"),
			GlobalInstances.Utf8Bom,
			GlobalInstances.FileWriteOptions
		);
		writer.AutoFlush = false;
		writer.NewLine = "\n";

		writer.WriteLine($"l_{lang}:");
		foreach ((string key, string value) in dict) {
			// We use partly StringBuilder and partly YamlSerializer here.
			// Since we want:
			//   1. Never quote keys
			//   2. Always quote values
			writer.Write(' ');
			writer.Write(key);
			writer.Write(": ");
			writer.Write(
				GlobalInstances.Yaml.Serializer.Serialize(value)
			); // Serializer produces \n
		}
	}
}
