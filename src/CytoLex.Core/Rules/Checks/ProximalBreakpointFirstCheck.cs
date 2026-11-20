namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.PROXIMAL_BREAKPOINT_FIRST</c> (§4.3.1.1): with both breaks in one arm the proximal breakpoint comes first (<see cref="BreakpointOrder"/>).</summary>
[KbRule("STRUCT.PROXIMAL_BREAKPOINT_FIRST")]
internal sealed class ProximalBreakpointFirstCheck() : StructuralCheck(BreakpointOrder.Analyze);
