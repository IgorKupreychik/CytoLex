namespace CytoLex.Core.Rules.Checks;

/// <summary><c>REC.FORMAT</c> (§4.5): no comma between <c>rec(n)</c> and its aberrations (<see cref="Derivatives"/>).</summary>
[KbRule("REC.FORMAT")]
internal sealed class RecFormatCheck() : StructuralCheck(Derivatives.Analyze);
