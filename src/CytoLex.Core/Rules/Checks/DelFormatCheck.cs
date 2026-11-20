namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DEL.FORMAT</c> (§9.2.2): <c>del(chromosome)(breakpoints)</c>: no arm in the chromosome list (<c>del(5q)</c>) and no arrow in the short system (<see cref="SymbolForms"/>).</summary>
[KbRule("DEL.FORMAT")]
internal sealed class DelFormatCheck() : StructuralCheck(SymbolForms.Analyze);
