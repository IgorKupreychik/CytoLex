namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.MULTI_CHROMOSOME_ORDER</c> (§4.3.1.2, §9.2.17.1): a translocation of three or more chromosomes starts with the sex chromosome or the lowest-numbered one (<see cref="ChromosomeOrder"/>).</summary>
[KbRule("STRUCT.MULTI_CHROMOSOME_ORDER")]
internal sealed class MultiChromosomeOrderCheck() : StructuralCheck(ChromosomeOrder.Analyze);
