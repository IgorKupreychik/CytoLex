using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
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

/// <summary>
/// A proposed fix of a diagnostic (docs/design/core.md §10, T-035). Level, kind and wording come from the KB rule's
/// <c>fix</c>; the edits come from the check or the parser. A level-3 fix is an explanation: no edits, the KB statement,
/// hint and citations in <see cref="Description"/>.
/// </summary>
/// <param name="Level">How deterministic the fix is.</param>
/// <param name="Kind">What it does, if it edits.</param>
/// <param name="Edits">Non-overlapping edits against the original input; empty for an explanation.</param>
/// <param name="Description">From the KB fix hint (level 1), or the explanation with its rendered citations (level 3).</param>
/// <param name="ForContext">The context this alternative is for, when the diagnostic is conditional.</param>
/// <param name="Preview">The whole corrected string (level 1); <c>null</c> for an explanation.</param>
/// <param name="Citations">The KB citations of the rule the fix belongs to.</param>
public sealed record Fix(
    FixLevel Level,
    FixKind? Kind,
    ImmutableArray<TextEdit> Edits,
    string Description,
    SampleContext? ForContext,
    string? Preview,
    ImmutableArray<Citation> Citations)
{
    /// <summary>Whether the fix edits the text (level 1) rather than only explaining.</summary>
    public bool IsEdit => Level == FixLevel.Deterministic && !Edits.IsDefaultOrEmpty;

    /// <summary>
    /// Whether the fix is a "did you mean" option (level 2, T-041): an edit the user may apply by hand. Never part of
    /// fix all or autocorrection, which take <see cref="IsEdit"/> fixes only.
    /// </summary>
    public bool IsSuggestion => Level == FixLevel.Suggestion && !Edits.IsDefaultOrEmpty;

    /// <summary>Whether the fix carries edits and a preview (level 1 or a level-2 suggestion).</summary>
    public bool HasEdits => !Edits.IsDefaultOrEmpty && Preview is not null;
}

/// <summary>
/// A "did you mean" option for a finding (level 2, T-041): one replace edit against the whole input and the wording
/// shown for it (<c>Did you mean 'del' (Deletion)?</c>).
/// </summary>
/// <param name="Edit">The replace edit.</param>
/// <param name="Description">The wording of the option.</param>
public sealed record SuggestionProposal(TextEdit Edit, string Description);

/// <summary>
/// What a check or the parser proposes for one finding: only the edits (and, for a context-dependent finding, the
/// context they are for). <see cref="DiagnosticFactory"/> turns it into a <see cref="Fix"/> with the KB level, kind and hint.
/// </summary>
/// <param name="Edits">Non-overlapping edits against the whole input.</param>
/// <param name="ForContext">The context the edits are for; <c>null</c> = the rule's single context when conditional, else any.</param>
public sealed record FixProposal(ImmutableArray<TextEdit> Edits, SampleContext? ForContext = null)
{
    /// <summary>One edit.</summary>
    public static FixProposal Of(TextEdit edit, SampleContext? forContext = null) => new([edit], forContext);

    /// <summary>Replace <paramref name="span"/> with <paramref name="text"/>.</summary>
    public static FixProposal Replace(TextSpan span, string text) => Of(new TextEdit(span, text));

    /// <summary>Delete <paramref name="span"/>.</summary>
    public static FixProposal Delete(TextSpan span) => Of(new TextEdit(span, string.Empty));

    /// <summary>Insert <paramref name="text"/> at <paramref name="offset"/>.</summary>
    public static FixProposal Insert(int offset, string text) => Of(new TextEdit(new TextSpan(offset, 0), text));
}
