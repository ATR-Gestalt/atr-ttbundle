namespace StlTechRelGen.Model;

internal abstract class TechRequirement {
	public abstract IEnumerable<string> Requirements();
}

internal sealed class TechRequirementSingle(string id) : TechRequirement {
	public string Id { get; private init; } = id;

	public override IEnumerable<string> Requirements() => [Id];
}

// OR clauses in prereq, seen commonly in bioship related techs
internal sealed class TechRequirementAlternatives(
	IEnumerable<string> alternatives
) : TechRequirement {
	public string[] Alternatives { get; private init; } = [.. alternatives];

	public override IEnumerable<string> Requirements() => Alternatives;
}
