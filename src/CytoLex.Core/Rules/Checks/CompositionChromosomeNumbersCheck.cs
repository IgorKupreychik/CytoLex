namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.COMPOSITION_CHROMOSOME_NUMBERS</c> (§4.3.2.2): chromosome numbers in a band composition only when several chromosomes are involved, and then on every band (<see cref="Composition"/>).</summary>
[KbRule("STRUCT.COMPOSITION_CHROMOSOME_NUMBERS")]
internal sealed class CompositionChromosomeNumbersCheck() : StructuralCheck(Composition.Analyze);
