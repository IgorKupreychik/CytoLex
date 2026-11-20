namespace CytoLex.Core.Rules.Checks;

/// <summary><c>DER.NO_PH_SYMBOL</c> (§9.2.3): <c>Ph</c> is for running text; the karyotype writes <c>der(22)t(9;22)(q34;q11.2)</c> (<see cref="SymbolForms"/>).</summary>
[KbRule("DER.NO_PH_SYMBOL")]
internal sealed class DerNoPhSymbolCheck() : StructuralCheck(SymbolForms.Analyze);
