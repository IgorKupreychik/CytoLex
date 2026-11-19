namespace CytoLex.Core.Rules.Checks;

/// <summary><c>STRUCT.BREAKPOINT_GROUPS</c> (§4.3.1 ¶1): one breakpoint group per chromosome, for symbols without a format rule of their own (<see cref="BreakpointGroups"/>); breakpoints outside parentheses are the parser's.</summary>
[KbRule("STRUCT.BREAKPOINT_GROUPS")]
internal sealed class BreakpointGroupsCheck() : StructuralCheck(BreakpointGroups.Analyze);
