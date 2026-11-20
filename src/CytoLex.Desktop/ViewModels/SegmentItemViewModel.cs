using CytoLex.Core.Segmentation;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>One detected segment: its notation type, its text and whether it was validated.</summary>
/// <param name="Type">The KB notation type name (<c>karyotype</c>, <c>nuc_ish</c>, …).</param>
/// <param name="Text">The segment text.</param>
/// <param name="Note">Why it was not validated, if it was not (e.g. <c>ish: not implemented in the pilot</c>).</param>
public sealed record SegmentItemViewModel(string Type, string Text, string? Note)
{
    /// <summary>Whether <see cref="Note"/> is set.</summary>
    public bool HasNote => Note is not null;

    /// <summary>The row for <paramref name="segment"/> and, if validated in an edition, its result there.</summary>
    public static SegmentItemViewModel From(string input, Segment segment, SegmentResult? result)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(segment);

        string? note = result switch
        {
            { Status: EditionStatus.NotImplemented } r => r.Reason ?? "not implemented",
            { Constructs.IsDefaultOrEmpty: false } r => "not validated: " + string.Join("; ", r.Constructs.Select(c => c.Reason)),
            _ => null,
        };
        return new SegmentItemViewModel(segment.Type.ToKbName(), segment.Span.Of(input).Trim(), note);
    }
}
