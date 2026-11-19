namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.CHROMOSOME_ORDER</c> (§4.1 ¶3): in a rearrangement of two chromosomes the sex chromosome or the lowest-numbered one is listed first (<see cref="ChromosomeOrder"/>).</summary>
[KbRule("STRUCT.CHROMOSOME_ORDER")]
internal sealed class ChromosomeOrderCheck() : StructuralCheck(ChromosomeOrder.Analyze);
