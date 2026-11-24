using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// One change a cell line inherits through <c>idem</c>/<c>sl</c>/<c>sdl</c> (T-070), shown greyed under the reference row;
/// clicking it selects the change where it is written.
/// </summary>
public sealed class InheritedItemViewModel : ISpanItem
{
    /// <param name="change">The inherited change.</param>
    /// <param name="source">The entry where the change is written.</param>
    /// <param name="sourceLineNumber">The number shown for its cell line.</param>
    /// <param name="removedBy">The text of the own item that removes it; <c>null</c> when kept.</param>
    /// <param name="toEditor">Maps spans to the editor text (T-097).</param>
    public InheritedItemViewModel(InheritedChange change, AbnormalityEntry source, int sourceLineNumber, string? removedBy, Func<TextSpan, TextSpan> toEditor)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(toEditor);

        Change = change;
        Span = toEditor(source.Span);
        Text = change.Text;
        ShortForm = source.ShortForm;
        Tag = $"from line {sourceLineNumber}";
        IsRemoved = change.RemovedBy is not null;
        RemovedTip = IsRemoved ? $"Removed by {removedBy} (signs are relative to the clone referred to, ISCN §11.1.4)" : null;
    }

    /// <summary>The change shown.</summary>
    public InheritedChange Change { get; }

    /// <summary>The source span (where the change is written), in the editor text.</summary>
    public TextSpan Span { get; }

    /// <summary>As written in the full form (<c>t(9;22)(q34;q11.2)×2</c>), or the source text when removed.</summary>
    public string Text { get; }

    /// <summary>The source's short form (<c>t(9;22)</c>).</summary>
    public string ShortForm { get; }

    /// <summary>Whether the short form differs from <see cref="Text"/>.</summary>
    public bool ShowShortForm => !string.Equals(ShortForm, Text, StringComparison.Ordinal);

    /// <summary><c>from line N</c>.</summary>
    public string Tag { get; }

    /// <summary>Removed by an own item of the line (<c>–inv(6)</c>); shown struck through.</summary>
    public bool IsRemoved { get; }

    /// <summary>Kept (not removed).</summary>
    public bool IsKept => !IsRemoved;

    /// <summary>Why it is removed; <c>null</c> when kept.</summary>
    public string? RemovedTip { get; }
}
