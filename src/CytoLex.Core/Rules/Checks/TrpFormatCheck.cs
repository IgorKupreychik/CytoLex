namespace CytoLex.Core.Rules.Checks;

/// <summary><c>TRP.FORMAT</c> (§9.2.19): the breakpoints of a triplication form one group (<see cref="BreakpointGroups"/>); only the grouping is checked.</summary>
[KbRule("TRP.FORMAT")]
internal sealed class TrpFormatCheck() : StructuralCheck(BreakpointGroups.Analyze);
