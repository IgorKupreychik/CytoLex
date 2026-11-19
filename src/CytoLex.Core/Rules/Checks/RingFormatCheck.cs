namespace CytoLex.Core.Rules.Checks;

/// <summary><c>RING.FORMAT</c> (§9.2.15): one breakpoint group per chromosome of a ring (<see cref="BreakpointGroups"/>); only the grouping is checked.</summary>
[KbRule("RING.FORMAT")]
internal sealed class RingFormatCheck() : StructuralCheck(BreakpointGroups.Analyze);
