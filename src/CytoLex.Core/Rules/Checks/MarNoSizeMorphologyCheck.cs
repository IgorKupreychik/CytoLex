namespace CytoLex.Core.Rules.Checks;

/// <summary><c>MAR.NO_SIZE_MORPHOLOGY</c> (§9.2.12): no size or shape term before <c>mar</c> (<see cref="SymbolForms"/>; unlisted terms are the parser's).</summary>
[KbRule("MAR.NO_SIZE_MORPHOLOGY")]
internal sealed class MarNoSizeMorphologyCheck() : StructuralCheck(SymbolForms.Analyze);
