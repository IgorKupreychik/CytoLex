namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.DETAILED_SYMBOLS</c> (§4.3.2.1): arm ends in a band composition are pter/qter (<see cref="Composition"/>).</summary>
[KbRule("STRUCT.DETAILED_SYMBOLS")]
internal sealed class DetailedSymbolsCheck() : StructuralCheck(Composition.Analyze);
