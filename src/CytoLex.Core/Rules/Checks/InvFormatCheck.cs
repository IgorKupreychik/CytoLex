namespace CytoLex.Core.Rules.Checks;

/// <summary><c>INV.FORMAT</c> (§9.2.10): the two breakpoints of an inversion form one group (<see cref="BreakpointGroups"/>); only the grouping is checked.</summary>
[KbRule("INV.FORMAT")]
internal sealed class InvFormatCheck() : StructuralCheck(BreakpointGroups.Analyze);
