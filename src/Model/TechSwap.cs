namespace StlTechRelGen.Model;

internal sealed class TechSwap(
	bool vanilla,
	TechArea area,
	List<string> categories
) {
	public bool Vanilla { get; private init; } = vanilla;
	public TechArea Area { get; private init; } = area;
	public List<string> Categories { get; private init; } = categories;

	// Each property falls back to the parent Tech's value when the swap
	// block omits it, via UnwrapOr(). This mirrors Stellaris's own
	// inheritance rule: an unset swap property inherits from the owning
	// technology.
	public static TechSwap Parse(
		CWNode node, CWComparer cwComparer, Tech parent
	) => new(
		cwComparer.IsVanilla(node.Position),
		node.Tag("area")
			.Map(x => Enum.Parse<TechArea>(x.ToRawString(), true))
			.UnwrapOr(parent.Area),
		node.Child("category")
			.Map(x => x.LeafValues.Select(i => i.ValueText).ToList())
			.UnwrapOr(parent.Categories)
	);
}
