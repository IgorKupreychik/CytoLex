namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.SHORT_ARM_BREAKPOINT_FIRST</c> (§4.3.1.1): with a break in each arm the short-arm breakpoint comes first (<see cref="BreakpointOrder"/>).</summary>
[KbRule("STRUCT.SHORT_ARM_BREAKPOINT_FIRST")]
internal sealed class ShortArmBreakpointFirstCheck() : StructuralCheck(BreakpointOrder.Analyze);
