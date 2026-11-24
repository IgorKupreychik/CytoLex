namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.DETAILED_MATCHES_SYMBOL</c> (§4.3, §4.3.2, T-086): a detailed composition is the one its symbol defines (<see cref="DetailedSymbolMatch"/>).</summary>
[KbRule(DetailedSymbolMatch.Rule)]
internal sealed class DetailedMatchesSymbolCheck() : StructuralCheck(DetailedSymbolMatch.Analyze);
