using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The result of one edition (T-037): verdict, counts, segment notes, diagnostics, abnormalities and editor highlights.
/// One chip of the edition strip (T-098); the view tabs (Diagnostics, Abnormalities, Translation) show the selected panel.
/// A single-edition check has one panel, "All editions" one per known edition.
/// </summary>
public partial class EditionPanelViewModel : ObservableObject
{
    public EditionPanelViewModel(ValidationReport report, EditionResult result, ApplyFixHandler? applyFix = null)
        : this(report, result, applyFix, CoordinateBuild.Grch38, null)
    {
    }

    /// <summary>A panel with the translation view (T-059).</summary>
    /// <param name="report">The report.</param>
    /// <param name="result">The edition's result.</param>
    /// <param name="applyFix">Applies a fix to the input.</param>
    /// <param name="build">The genome build whose coordinates are shown.</param>
    /// <param name="openKbEntry">Opens a translation link (edition, KB entry) in the rule browser.</param>
    public EditionPanelViewModel(ValidationReport report, EditionResult result, ApplyFixHandler? applyFix, CoordinateBuild build, Action<string, KbRef>? openKbEntry)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(result);

        // With autocorrection (T-097) the result's spans refer to the corrected string; the editor shows the typed text.
        var input = report.Input;
        var text = EditionText.Of(report, result);
        Result = result;
        Edition = result.Edition;
        IsNotImplemented = result.Status == EditionStatus.NotImplemented;
        IsValid = !IsNotImplemented && result.Validity.IsValid;
        IsAutoCorrected = result.Verdict == EditionVerdict.ValidAfterAutoCorrection;
        ValidityText = VerdictOf(result, report.Context);
        ChipText = ChipOf(Edition, IsNotImplemented, IsValid, result.Counts.Errors, IsAutoCorrected);
        CountsText = IsNotImplemented ? string.Empty : CountsOf(result.Counts);
        ChipTip = CountsText.Length == 0 ? ValidityText : $"{ValidityText} · {CountsText}";

        Segments = [.. report.Segments.Select(s => SegmentItemViewModel.From(input, s, result.Segments.FirstOrDefault(r => r.Segment.Index == s.Index)))];
        Diagnostics = [.. result.Diagnostics.Select((d, i) => new DiagnosticItemViewModel(text, d, i, result.Edition, applyFix))];
        FixAll = FixAllViewModel.From(text, result, report.Context.Effective, applyFix);
        CellLineGroups = CellLineGroupViewModel.From(result, text.ToEditor);
        var withLoci = result.Loci.Select(l => l.Abnormality).ToHashSet();
        foreach (var card in CellLineGroups.SelectMany(g => g.Items))
        {
            card.IsOnMap = withLoci.Contains(card.Entry.Index);
        }

        Map = IsNotImplemented ? null : new MapViewModel(result, MapBands.For(result.Edition), build, text.ToEditor);
        Highlights = IsNotImplemented ? [] : HighlightBuilder.Build(text, result, report.Context.Effective);
        if (text.IsCorrected)
        {
            Corrected = text.Text;
            Corrections = [.. result.AutoCorrection!.Corrections.Select(c => new CorrectionItemViewModel(
                c,
                openKbEntry is null ? null : id => openKbEntry(result.Edition, KbRef.Rule(id))))];
        }

        Translation = result.Translation is { } translation
            ? new TranslationViewModel(
                translation,
                result.Diagnostics,
                build,
                openKbEntry is null ? null : r => openKbEntry(result.Edition, r),
                SelectDiagnostic,
                text.ToEditor)
            : null;
    }

    /// <summary>The corrected string (T-097); <c>null</c> when autocorrection was off or changed nothing.</summary>
    public string? Corrected { get; }

    /// <summary>Whether the "Corrected" line is shown: autocorrection changed the input.</summary>
    public bool HasCorrection => Corrected is not null;

    /// <summary>One note per applied autocorrection (rule, change, citation).</summary>
    public IReadOnlyList<CorrectionItemViewModel> Corrections { get; } = [];

    /// <summary>Whether the verdict is "valid after autocorrection" (T-097): valid, but not as written.</summary>
    public bool IsAutoCorrected { get; }

    /// <summary>Valid as written (the plain green chip).</summary>
    public bool IsValidAsWritten => IsValid && !IsAutoCorrected;

    /// <summary>The literal translation (T-059); <c>null</c> when the edition has none (not implemented, or not requested).</summary>
    public TranslationViewModel? Translation { get; }

    /// <summary>The Map view (T-101): ideograms with the loci of the string; <c>null</c> for a not-implemented edition.</summary>
    public MapViewModel? Map { get; }

    /// <summary>The Map tab header: <c>Map (3)</c>.</summary>
    public string MapHeader => Map?.Header ?? "Map";

    /// <summary>
    /// Marks what a selection of <paramref name="span"/> (a clicked marker, row, card or diagnostic) is about: the loci on the
    /// Map and the abnormality cards holding it or inside it (T-101).
    /// </summary>
    public void MarkSelection(TextSpan span)
    {
        Map?.SelectSpan(span);
        foreach (var card in CellLineGroups.SelectMany(g => g.Items))
        {
            card.IsMarked = span.Length > 0
                && ((span.Start >= card.Span.Start && span.End <= card.Span.End) || (card.Span.Start >= span.Start && card.Span.End <= span.End));
        }
    }

    /// <summary>Whether there is a translation to show.</summary>
    public bool HasTranslation => Translation is not null;

    /// <summary>Selects diagnostic <paramref name="index"/> (into <c>EditionResult.Diagnostics</c>) in the list: a translation part's diagnostic link.</summary>
    public void SelectDiagnostic(int index)
    {
        if (index >= 0 && index < Diagnostics.Count)
        {
            // Re-selecting the same item still selects its span again.
            if (ReferenceEquals(SelectedDiagnostic, Diagnostics[index]))
            {
                SelectedDiagnostic = null;
            }

            SelectedDiagnostic = Diagnostics[index];
        }
    }

    /// <summary>The edition's result.</summary>
    public EditionResult Result { get; }

    /// <summary>The edition, e.g. <c>2016</c>.</summary>
    public string Edition { get; }

    /// <summary>The edition chip (T-098): <c>2016 ✓ valid</c>, <c>2016 ✗ 2 errors</c> or <c>2020 n/a</c>.</summary>
    public string ChipText { get; }

    /// <summary>The chip's tooltip: the verdict and the counts.</summary>
    public string ChipTip { get; }

    /// <summary>The verdict, e.g. <c>Valid in ISCN 2016 as constitutional only</c> or <c>ISCN 2020: not implemented</c>.</summary>
    public string ValidityText { get; }

    /// <summary>Whether the input is valid in the edition (for the verdict colour).</summary>
    public bool IsValid { get; }

    /// <summary>Whether the edition is not implemented (no KB data or profile).</summary>
    public bool IsNotImplemented { get; }

    /// <summary>Whether the edition was validated (the opposite of <see cref="IsNotImplemented"/>).</summary>
    public bool IsImplemented => !IsNotImplemented;

    /// <summary>Whether the input is invalid in an implemented edition (red verdict).</summary>
    public bool IsInvalid => IsImplemented && !IsValid;

    /// <summary>Diagnostic counts; empty for a not-implemented edition.</summary>
    public string CountsText { get; }

    /// <summary>The detected segments with this edition's notes (e.g. <c>ish: not implemented in the pilot</c>).</summary>
    public IReadOnlyList<SegmentItemViewModel> Segments { get; }

    /// <summary>The diagnostics of the edition, in span order.</summary>
    public IReadOnlyList<DiagnosticItemViewModel> Diagnostics { get; }

    /// <summary>Whether there are diagnostics (else the list shows "No findings.").</summary>
    public bool HasDiagnostics => Diagnostics.Count > 0;

    /// <summary>The header of the diagnostics panel: <c>Diagnostics</c> or <c>Diagnostics (3)</c>.</summary>
    public string DiagnosticsHeader => Diagnostics.Count == 0 ? "Diagnostics" : $"Diagnostics ({Diagnostics.Count})";

    /// <summary>"Fix all" (T-040): one previewed round of every applicable level-1 fix; <c>null</c> when fewer than two apply.</summary>
    public FixAllViewModel? FixAll { get; }

    /// <summary>Whether <see cref="FixAll"/> is offered.</summary>
    public bool HasFixAll => FixAll is not null;

    /// <summary>The abnormalities of the karyotype segments, grouped by cell line (T-069).</summary>
    public IReadOnlyList<CellLineGroupViewModel> CellLineGroups { get; }

    /// <summary>Whether there are cell lines to show in the abnormalities panel.</summary>
    public bool HasCellLines => CellLineGroups.Count > 0;

    /// <summary>The header of the abnormalities view: <c>Abnormalities</c> or <c>Abnormalities (2)</c> (cards over every cell line).</summary>
    public string AbnormalitiesHeader => CellLineGroups.Sum(g => g.Items.Length) is var n and > 0 ? $"Abnormalities ({n})" : "Abnormalities";

    /// <summary>Shown in a view of a not-implemented edition, e.g. <c>ISCN 2020 is not implemented.</c></summary>
    public string NotImplementedText => $"ISCN {Edition} is not implemented.";

    /// <summary>The editor highlights of this edition's result.</summary>
    public ImmutableArray<Highlight> Highlights { get; }

    /// <summary>The diagnostic selected in the list.</summary>
    [ObservableProperty]
    public partial DiagnosticItemViewModel? SelectedDiagnostic { get; set; }

    internal static string VerdictOf(EditionResult result, ContextInfo context)
    {
        if (result.Status == EditionStatus.NotImplemented)
        {
            return result.Reason ?? $"ISCN {result.Edition}: not implemented";
        }

        var v = result.Validity;
        var all = Enum.GetValues<SampleContext>().Length;
        var valid = result.Verdict == EditionVerdict.ValidAfterAutoCorrection
            ? $"Valid in ISCN {result.Edition} after autocorrection"
            : $"Valid in ISCN {result.Edition}";
        var text = !v.IsValid ? $"Not valid in ISCN {result.Edition}"
            : context.Effective is null && v.ValidContexts.Length < all
                ? $"{valid}{(result.Verdict == EditionVerdict.ValidAfterAutoCorrection ? "," : string.Empty)} as {string.Join(" or ", v.ValidContexts.Select(DiagnosticItemViewModel.Label))} only"
                : valid;
        return v.IsComplete ? text : text + " (as far as implemented)";
    }

    internal static string ChipOf(string edition, bool notImplemented, bool valid, int errors, bool autoCorrected = false) =>
        notImplemented ? $"{edition} n/a"
        : valid && autoCorrected ? $"{edition} ✓ valid after autocorrection"
        : valid ? $"{edition} ✓ valid"
        : errors == 1 ? $"{edition} ✗ 1 error"
        : errors > 1 ? $"{edition} ✗ {errors} errors"
        : $"{edition} ✗ not valid";

    private static string CountsOf(SeverityCounts c)
    {
        var text = $"{c.Errors} error(s), {c.Warnings} warning(s), {c.Infos} info(s)";
        return c.Conditional > 0 ? text + $", {c.Conditional} context-dependent" : text;
    }
}
