namespace CytoLex.Core.Rules.Checks;

/// <summary><c>REC.NOT_ACQUIRED</c> (§9.2.3): <c>rec</c> is not used for acquired abnormalities; conditional by the KB context (<see cref="Derivatives"/>).</summary>
[KbRule("REC.NOT_ACQUIRED")]
internal sealed class RecNotAcquiredCheck() : StructuralCheck(Derivatives.Analyze);
