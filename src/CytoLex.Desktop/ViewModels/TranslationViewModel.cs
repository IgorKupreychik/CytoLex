using System.Collections.Immutable;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The literal translation of one edition (T-059, over the T-058 <see cref="TranslationResult"/>): banner, draft note, reading
/// basis, summary and one card per part. Parts and their spans are linked both ways: <see cref="HighlightAt"/> marks the part at
/// an editor offset, and a hovered card marks its span in the editor (<see cref="MainWindowViewModel.HoverSpan"/>).
/// </summary>
public sealed class TranslationViewModel
{
    /// <summary>The note on draft wording (decided with the user, 2026-11-21: shown until the review T-082).</summary>
    public const string DraftNoteText = "Draft wording: the translation templates have not been reviewed yet.";

    public TranslationViewModel(
        TranslationResult translation,
        ImmutableArray<Diagnostic> diagnostics,
        CoordinateBuild build,
        Action<KbRef>? openKbRef,
        Action<int>? selectDiagnostic)
        : this(translation, diagnostics, build, openKbRef, selectDiagnostic, null)
    {
    }

    /// <summary>A translation whose part spans are mapped to the editor text by <paramref name="toEditor"/> (T-097: autocorrection).</summary>
    public TranslationViewModel(
        TranslationResult translation,
        ImmutableArray<Diagnostic> diagnostics,
        CoordinateBuild build,
        Action<KbRef>? openKbRef,
        Action<int>? selectDiagnostic,
        Func<TextSpan, TextSpan>? toEditor)
    {
        ArgumentNullException.ThrowIfNull(translation);
        Result = translation;
        Banner = translation.Banner;
        DraftNote = translation.IsDraft ? DraftNoteText : null;
        ReadingBasisText = translation.ReadingBasis == ReadingBasis.Unknown && HasAnyReadings(translation)
            ? "Context unknown: where the context changes the meaning, both readings are given."
            : null;
        Summary = new TranslationItemViewModel(translation.Summary, build, openKbRef);
        Items = [.. translation.Items.Select(i => new TranslationItemViewModel(i, diagnostics, build, openKbRef, selectDiagnostic, toEditor))];
    }

    /// <summary>The core result.</summary>
    public TranslationResult Result { get; }

    /// <summary><c>Not valid ISCN 2016; partial translation.</c> when the edition has errors; else <c>null</c>.</summary>
    public string? Banner { get; }

    /// <summary>Whether the banner is shown.</summary>
    public bool HasBanner => Banner is not null;

    /// <summary><see cref="DraftNoteText"/> when a phrase comes from an unreviewed template; else <c>null</c>.</summary>
    public string? DraftNote { get; }

    /// <summary>Whether the draft note is shown.</summary>
    public bool HasDraftNote => DraftNote is not null;

    /// <summary>A note that both readings are given (context unknown and some phrase depends on it); else <c>null</c>.</summary>
    public string? ReadingBasisText { get; }

    /// <summary>Whether the reading note is shown.</summary>
    public bool HasReadingBasisText => ReadingBasisText is not null;

    /// <summary>The summary line.</summary>
    public TranslationItemViewModel Summary { get; }

    /// <summary>The parts, in input order.</summary>
    public ImmutableArray<TranslationItemViewModel> Items { get; }

    /// <summary>
    /// Marks the part whose span contains <paramref name="offset"/> (the narrowest one when parts nest, e.g. a cell line header
    /// and its items) and unmarks the others.
    /// </summary>
    /// <returns>The marked part's span, or <c>null</c> when no part is at the offset.</returns>
    public TextSpan? HighlightAt(int offset)
    {
        var hit = Items
            .Where(i => i.Span.Start <= offset && offset < i.Span.End)
            .OrderBy(i => i.Span.Length)
            .FirstOrDefault();
        Highlight(hit);
        return hit?.Span;
    }

    /// <summary>Marks <paramref name="item"/> only (<c>null</c> = none).</summary>
    public void Highlight(TranslationItemViewModel? item)
    {
        foreach (var i in Items)
        {
            i.IsHighlighted = ReferenceEquals(i, item);
        }
    }

    /// <summary>Re-renders coordinates and band tooltips for another build (no re-validation: both builds are in the result).</summary>
    public void SetBuild(CoordinateBuild build)
    {
        Summary.SetBuild(build);
        foreach (var item in Items)
        {
            item.SetBuild(build);
        }
    }

    private static bool HasAnyReadings(TranslationResult t) => t.Summary.Readings is not null || t.Items.Any(i => i.Readings is not null);
}
