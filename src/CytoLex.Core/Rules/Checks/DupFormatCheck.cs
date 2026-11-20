namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DUP.FORMAT</c> (§9.2.5): the short system uses no arrow; the band order gives the orientation (<see cref="SymbolForms"/>).</summary>
[KbRule("DUP.FORMAT")]
internal sealed class DupFormatCheck() : StructuralCheck(SymbolForms.Analyze);
