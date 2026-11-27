using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// One evidence row of an association card (T-110): the abnormality, <c>con</c> group or locus that satisfies a leaf of
/// the rule. A click selects its span in the editor, hovering marks it.
/// </summary>
public sealed partial class AssociationEvidenceViewModel : ObservableObject, ISpanItem
{
    /// <summary>The row of <paramref name="evidence"/>; its span mapped to the editor by <paramref name="toEditor"/>.</summary>
    public AssociationEvidenceViewModel(AssociationEvidence evidence, Func<TextSpan, TextSpan>? toEditor = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Evidence = evidence;
        Span = (toEditor ?? (static s => s))(evidence.Span);
        var parts = new List<string>(2);
        if (evidence.Overlap is { } overlap)
        {
            parts.Add(overlap >= 1.0
                ? "covers the whole region"
                : string.Create(CultureInfo.InvariantCulture, $"covers {Math.Max(1, (int)Math.Round(overlap * 100))} % of the region"));
        }

        if (evidence.Reading is { } reading)
        {
            parts.Add(reading);
        }

        Detail = string.Join(" · ", parts);
    }

    /// <summary>The evidence.</summary>
    public AssociationEvidence Evidence { get; }

    /// <inheritdoc />
    public TextSpan Span { get; }

    /// <summary>What matched, as written.</summary>
    public string Text => Evidence.Text;

    /// <summary>The leaf it satisfies (<c>fusion 9q34+22q11.2</c>).</summary>
    public string LeafText => Evidence.LeafText;

    /// <summary>The overlap share and the copy-number reading, if any; empty otherwise.</summary>
    public string Detail { get; }

    /// <summary>Whether <see cref="Detail"/> is shown.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>Under the mouse, or its span is (in the editor, a card, a locus).</summary>
    [ObservableProperty]
    public partial bool IsHighlighted { get; set; }

    /// <summary>Its span is selected (a click here or elsewhere).</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
