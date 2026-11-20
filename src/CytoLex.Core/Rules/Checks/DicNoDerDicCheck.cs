namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DIC.NO_DER_DIC</c> (§9.2.4): <c>der</c> or <c>dic</c>, never <c>der dic</c> (<see cref="SymbolForms"/>).</summary>
[KbRule("DIC.NO_DER_DIC")]
internal sealed class DicNoDerDicCheck() : StructuralCheck(SymbolForms.Analyze);
