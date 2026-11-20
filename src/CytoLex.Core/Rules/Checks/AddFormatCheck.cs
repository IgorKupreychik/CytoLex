namespace CytoLex.Core.Rules.Checks;

/// <summary><c>ADD.FORMAT</c> (§9.2.1): unknown material attached at one band (<c>t(1;?)(p36;?)</c>) is recommended as <c>add</c> (<see cref="SymbolForms"/>).</summary>
[KbRule("ADD.FORMAT")]
internal sealed class AddFormatCheck() : StructuralCheck(SymbolForms.Analyze);
