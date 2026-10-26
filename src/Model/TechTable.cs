namespace StlTechRelGen.Model;

internal sealed class TechTable {
	public ReadOnlyDictionary<string, Tech> Techs { get; private init; }

	public ReadOnlySet<string> AllTechIds { get; }

	public TechTable(ProgressContext? ctx, Dictionary<string, Tech> dict) {
		ProgressTask? task = ProgressExt.Task(ctx, Messages.Progress.BuildingTechRelationship)?
			.MaxValue(dict.Count);
		// Build the reverse edge table: for every "tech_foo requires tech_bar",
		// add tech_foo to tech_bar.Unlocks. This constructs the bidirectional
		// prerequisite-unlock graph from the unidirectional prerequisite
		// data, so L10nBuilder can look up unlocks directly from each tech.
		foreach ((string id, Tech tech) in dict) {
			foreach (TechRequirement requirement in tech.Requires) {
				foreach (string reqId in requirement.Requirements()) {
					if (dict.TryGetValue(reqId, out Tech? reqTech)) {
						reqTech.Unlocks.Add(id);
					} else {
						LogWarning(Messages.Data.TechUnknownPrereq, id, reqId);
					}
				}
			}

			task?.Increment(1);
		}

		task?.StopTask();

		Techs = dict.AsReadOnly();
		// AllTechIds = every tech ID + every swap ID, flattened into one set.
		// Used by L10nBuilder's loc-reference resolver to verify that
		// $..._desc$ references point to known entities before resolving.
		AllTechIds = Techs
			.SelectMany(i => i.Value.Swaps.Keys.Prepend(i.Key))
			.ToHashSet().AsReadOnly();
	}
}
