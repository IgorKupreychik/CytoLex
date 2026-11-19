namespace CytoLex.Core.Rules.Checks;

/// <summary><c>QDP.FORMAT</c> (§9.2.14): the breakpoints of a quadruplication form one group (<see cref="BreakpointGroups"/>); only the grouping is checked.</summary>
[KbRule("QDP.FORMAT")]
internal sealed class QdpFormatCheck() : StructuralCheck(BreakpointGroups.Analyze);
