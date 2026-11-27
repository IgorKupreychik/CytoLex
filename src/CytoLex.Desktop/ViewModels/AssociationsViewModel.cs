using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The Associations view (T-110): the fixed disclaimer, then one card per association rule the string matches
/// (<see cref="EditionResult.Associations"/>, T-109). Literature associations, never a diagnosis (T-104).
/// </summary>
public sealed class AssociationsViewModel
{
    /// <summary>The disclaimer shown at the top of the view, always (T-104/T-110 wording).</summary>
    public const string DisclaimerText =
        "Conditions associated with these abnormalities in the cited literature. Not a diagnosis or an interpretation of this result.";

    /// <summary>The view of <paramref name="result"/>'s associations; spans mapped to the editor by <paramref name="toEditor"/>.</summary>
    public AssociationsViewModel(EditionResult result, Func<TextSpan, TextSpan>? toEditor = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        Items = [.. result.Associations.Select(m => new AssociationItemViewModel(m, toEditor))];
        Note = result.Validity.IsValid
            ? null
            : $"The string is not valid in ISCN {result.Edition}; the matches are read from it as written.";
    }

    /// <summary>One card per matched rule, in the result's order.</summary>
    public IReadOnlyList<AssociationItemViewModel> Items { get; }

    /// <summary>Whether a rule matched.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>Shown when no rule matched.</summary>
    public static string EmptyText => "No association rule matches this string.";

    /// <summary>A note when the string is not valid in the edition; <c>null</c> otherwise.</summary>
    public string? Note { get; }

    /// <summary>Whether <see cref="Note"/> is shown.</summary>
    public bool HasNote => Note is not null;

    /// <summary>The tab header: <c>Associations</c> or <c>Associations (2)</c>.</summary>
    public string Header => Items.Count == 0 ? "Associations" : $"Associations ({Items.Count})";

    /// <summary>All evidence rows over the cards.</summary>
    public IEnumerable<AssociationEvidenceViewModel> Evidence => Items.SelectMany(i => i.Evidence);

    /// <summary>Highlights the evidence rows of a hovered span (the editor, a card, a locus); <c>null</c> clears them.</summary>
    public void HighlightSpan(TextSpan? span)
    {
        foreach (var row in Evidence)
        {
            row.IsHighlighted = Overlaps(row.Span, span);
        }
    }

    /// <summary>Marks the evidence rows of a selected span (a clicked row, card, marker or diagnostic).</summary>
    public void SelectSpan(TextSpan span)
    {
        foreach (var row in Evidence)
        {
            row.IsSelected = Overlaps(row.Span, span);
        }
    }

    // The row holds the span or lies inside it.
    private static bool Overlaps(TextSpan row, TextSpan? span) =>
        span is { Length: > 0 } s
        && ((s.Start >= row.Start && s.End <= row.End) || (row.Start >= s.Start && row.End <= s.End));
}
