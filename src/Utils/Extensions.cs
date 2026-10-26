using Microsoft.FSharp.Collections;

namespace StlTechRelGen.Utils;

internal static class Extensions {
	extension(Db.Mod self) {
		public string Path() => self.DirPath ?? self.ArchivePath
			?? throw new InvalidDataException(
				$"Mod {self.DisplayName} has no path"
			);
	}

	extension(Db.LauncherV2DbContext self) {
		public IOrderedQueryable<Db.Mod> GetModsInPlayset(Db.Playset playset) {
			IQueryable<Db.PlaysetsMod> playsetMods = self.PlaysetsMods
				.Where(i => (i.Enabled ?? false) && i.PlaysetId == playset.Id);
			return self.Mods
				.Where(i => i.Status == "ready_to_play"
					&& playsetMods.Any(j => j.ModId == i.Id)
				)
				.OrderBy(i =>
					playsetMods.Single(j => j.ModId == i.Id).Position
				);
		}
	}

	extension(string self) {
		// Find the longest common prefix between two path strings.
		// Uses Span<char> for zero-allocation per-character comparison;
		// this is called heavily by CwComparer for mod-index resolution.
		public string CommonPrefix(
			string other,
			StringComparison comparison = default
		) {
			if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(other)) {
				return string.Empty;
			}

			int commonLength = Math.Min(self.Length, other.Length);
			ReadOnlySpan<char> selfSpan = self.AsSpan();
			ReadOnlySpan<char> otherSpan = other.AsSpan();
			for (int i = 0; i < commonLength; i++) {
				if (!MemoryExtensions.Equals(
					selfSpan[i..(i + 1)],
					otherSpan[i..(i + 1)],
					comparison
				)) {
					return self[..i];
				}
			}

			return self[..commonLength];
		}
	}

	extension(Stream self) {
		public string ReadToString() {
			using StreamReader reader = new(self, true);
			return reader.ReadToEnd();
		}
	}

	extension(CompositeFormat self) {
		public string Format<T>(T arg) =>
			string.Format(CultureInfo.InvariantCulture, self, arg);
		public string Format<T1, T2>(T1 arg1, T2 arg2) =>
			string.Format(CultureInfo.InvariantCulture, self, arg1, arg2);
		public string Format<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3) =>
			string.Format(CultureInfo.InvariantCulture, self, arg1, arg2, arg3);

		public string FormatLocal<T>(T arg) =>
			string.Format(CultureInfo.CurrentCulture, self, arg);
		public string FormatLocal<T1, T2>(T1 arg1, T2 arg2) =>
			string.Format(CultureInfo.CurrentCulture, self, arg1, arg2);
		public string FormatLocal<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3) =>
			string.Format(CultureInfo.CurrentCulture, self, arg1, arg2, arg3);
	}

	extension<T>(IEnumerable<T> self) {
		public void ForEach(Action<T> action) {
			foreach (T item in self) {
				action(item);
			}
		}

		public FSharpList<T> ToFSharpList() =>
			SeqModule.ToList(self);

		public List<T> ToSortedList(IComparer<T> comparer) {
			List<T> list = [.. self];
			list.Sort(comparer);
			return list;
		}

		public List<T> ToSortedList(Comparison<T> comparison) {
			List<T> list = [.. self];
			list.Sort(comparison);
			return list;
		}

		public Dictionary<TKey, TValue> ToDictionaryOverwriting<TKey, TValue>(
			Func<T, TKey> keySelector,
			Func<T, TValue> valueSelector
		) where TKey : notnull {
			Dictionary<TKey, TValue> dict = [];

			IEnumerator<T> enumerator = self.GetEnumerator();
			while (enumerator.MoveNext()) {
				dict[keySelector(enumerator.Current)] =
					valueSelector(enumerator.Current);
			}

			return dict;
		}

		public IEnumerable<T> DriveProgressTask(ProgressTask? task) {
			List<T> list = [.. self];
			if (task is not null) {
				task.IsIndeterminate(false)
					.Value(0)
					.MaxValue(list.Count)
					.StartTask();
			}

			foreach (T item in list) {
				yield return item;
				task?.Increment(1);
			}

			task?.StopTask();
		}
	}

	extension(IEnumerable<string> self) {
		public string Join(char separator) {
			StringBuilder sb = new();
			sb.AppendJoin(separator, self);
			return sb.ToString();
		}
	}

	extension<T>(FSharpOption<T> self) {
		public FSharpOption<TTo> Map<TTo>(Func<T, TTo> mapper) =>
			OptionModule.IsSome(self)
			? FSharpOption<TTo>.Some(mapper.Invoke(self.Value))
			: FSharpOption<TTo>.None;

		public T UnwrapOr(T fallback) =>
			OptionModule.DefaultValue(fallback, self);
	}

	extension(CWLang self) {
		// CWTools represents game languages as F# discriminated unions:
		//   CWLang.STL(STLLang.English) -> "english"
		// The outer switch discriminates STL vs. HOI4/EU4 (never expected
		// in this project); the inner switch maps STL's ~10 language
		// variants to Stellaris localization directory names.
		public string Name() => self is CWLang.STL lang
			? lang.Item switch {
				STLLang.English => "english",
				STLLang.French => "french",
				STLLang.German => "german",
				STLLang.Spanish => "spanish",
				STLLang.Russian => "russian",
				STLLang.Polish => "polish",
				STLLang.Braz_Por => "braz_por",
				STLLang.Chinese => "simp_chinese",
				STLLang.Japanese => "japanese",
				STLLang.Korean => "korean",
				_ => throw new NotSupportedException()
			}
			: throw new NotSupportedException();
	}

	extension<T>(SelectionPrompt<T> self) where T : notnull {
		public SelectionPrompt<T> UsePreset() => self
			.WrapAround()
			.MoreChoicesText(Messages.Prompt.MoreChoicesText)
			.SearchPlaceholderText(Messages.Prompt.SearchPlaceholderText);
	}

	extension(Progress self) {
		public Progress UsePreset() => self
			.AutoClear(false)
			.HideCompleted(false)
			.Columns(
			new SpinnerColumn(Spinner.Known.Default),
			new PercentageColumn().Style(new(foreground: Color.Blue)),
			new TaskDescriptionColumn(),
			new ProgressBarColumn(),
			new ShortElapsedTimeColumn()
		);
	}
}
