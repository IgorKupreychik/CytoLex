namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ACE.NOT_IN_KARYOTYPE</c> (§9.2.12): acentric fragments are not written in a karyotype (<see cref="SymbolForms"/>).</summary>
[KbRule("ACE.NOT_IN_KARYOTYPE")]
internal sealed class AceNotInKaryotypeCheck() : StructuralCheck(SymbolForms.Analyze);
