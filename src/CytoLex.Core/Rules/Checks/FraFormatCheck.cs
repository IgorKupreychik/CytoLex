namespace CytoLex.Core.Rules.Checks;

/// <summary><c>FRA.FORMAT</c> (§9.2.7): <c>fra(chromosome)(band)</c>, the band in its own parentheses (<see cref="SymbolForms"/>).</summary>
[KbRule("FRA.FORMAT")]
internal sealed class FraFormatCheck() : StructuralCheck(SymbolForms.Analyze);
