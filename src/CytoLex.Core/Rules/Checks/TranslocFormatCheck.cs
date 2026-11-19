namespace CytoLex.Core.Rules.Checks;

/// <summary><c>TRANSLOC.FORMAT</c> (§9.2.17.1): one breakpoint group per chromosome of a translocation (<see cref="BreakpointGroups"/>); only the grouping is checked.</summary>
[KbRule("TRANSLOC.FORMAT")]
internal sealed class TranslocFormatCheck() : StructuralCheck(BreakpointGroups.Analyze);
