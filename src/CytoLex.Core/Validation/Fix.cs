using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Text;

namespace CytoLex.Core.Validation;

/// <summary>How deterministic a fix is (docs/design/core.md §10; decisions → Fix suggestions).</summary>
public enum FixLevel
{
    /// <summary>A deterministic text edit.</summary>
    Deterministic = 1,

    /// <summary>A "did you mean" suggestion (T-041).</summary>
    Suggestion = 2,

    /// <summary>An explanation only, no edit.</summary>
    Explanation = 3,
}

/// <summary>What a fix does to the text (the KB <c>fix.kind</c>).</summary>
public enum FixKind
{
    /// <summary>Replace the covered text.</summary>
    Replace,

    /// <summary>Insert text at a point.</summary>
    Insert,

    /// <summary>Delete the covered text.</summary>
    Delete,

    /// <summary>Reorder parts (expressed as a replace of the covered span).</summary>
    Reorder,
}

/// <summary>One edit against the original input: insert = zero-length span, delete = empty <paramref name="NewText"/>.</summary>
public sealed record TextEdit(TextSpan Span, string NewText);

/// <summary>A proposed fix of a diagnostic (§10). Built by T-035; the engine leaves <see cref="Diagnostic.Fixes"/> empty until then.</summary>
/// <param name="Level">How deterministic the fix is.</param>
/// <param name="Kind">What it does, if it edits.</param>
/// <param name="Edits">Non-overlapping edits against the original input; empty for an explanation.</param>
/// <param name="Description">From the KB fix hint.</param>
/// <param name="ForContext">The context this alternative is for, when the diagnostic is conditional.</param>
/// <param name="Preview">The whole corrected string.</param>
public sealed record Fix(
    FixLevel Level,
    FixKind? Kind,
    ImmutableArray<TextEdit> Edits,
    string Description,
    SampleContext? ForContext,
    string? Preview);
