using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// One run of a fix diff (T-040): kept text plain, removed text red and struck through, added text green (styles
/// <c>Border.diff.removed</c>/<c>.added</c>, colours in <see cref="Highlighting.DiffBrushes"/>).
/// </summary>
public sealed class DiffPartViewModel(DiffPart part)
{
    /// <summary>The run.</summary>
    public DiffPart Part { get; } = part ?? throw new ArgumentNullException(nameof(part));

    /// <summary>The text of the run; a removed or added space is shown as <c>·</c> so it can be seen.</summary>
    public string Text => Part.Kind == DiffKind.Same ? Part.Text : Part.Text.Replace(' ', '·');

    /// <summary>The run's kind.</summary>
    public DiffKind Kind => Part.Kind;

    /// <summary>Whether the run is removed text.</summary>
    public bool IsRemoved => Part.Kind == DiffKind.Removed;

    /// <summary>Whether the run is added text.</summary>
    public bool IsAdded => Part.Kind == DiffKind.Added;
}
