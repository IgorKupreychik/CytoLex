namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.COMPOSITION_ORDER</c> (§4.3.2.2): a band composition runs from the short-arm end to the long-arm end (<see cref="Composition"/>).</summary>
[KbRule("STRUCT.COMPOSITION_ORDER")]
internal sealed class CompositionOrderCheck() : StructuralCheck(Composition.Analyze);
