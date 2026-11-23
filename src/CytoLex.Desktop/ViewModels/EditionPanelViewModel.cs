using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The result of one edition (T-037): verdict, counts, segment notes, diagnostics, abnormalities and editor highlights.
/// One tab of the results area; a single-edition check has one panel, "All editions" one per known edition.
/// </summary>
public partial class EditionPanelViewModel : ObservableObject, IResultTab
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

        var input = report.Input;
        Result = result;
        Edition = result.Edition;
        IsNotImplemented = result.Status == EditionStatus.NotImplemented;
        IsValid = !IsNotImplemented && result.Validity.IsValid;
        ValidityText = VerdictOf(result, report.Context);
        Header = IsNotImplemented ? $"ISCN {Edition} · not implemented"
            : IsValid ? $"ISCN {Edition} ✓ valid"
            : $"ISCN {Edition} ✗ not valid";
        CountsText = IsNotImplemented ? string.Empty : CountsOf(result.Counts);

        Segments = [.. report.Segments.Select(s => SegmentItemViewModel.From(input, s, result.Segments.FirstOrDefault(r => r.Segment.Index == s.Index)))];
        Diagnostics = [.. result.Diagnostics.Select((d, i) => new DiagnosticItemViewModel(input, d, i, result.Edition, applyFix))];
        FixAll = FixAllViewModel.From(input, result, report.Context.Effective, applyFix);
        CellLineGroups = CellLineGroupViewModel.From(result);
        Highlights = IsNotImplemented ? [] : HighlightBuilder.Build(input, result, report.Context.Effective);
        Translation = result.Translation is { } translation
            ? new TranslationViewModel(
                translation,
                result.Diagnostics,
                build,
                openKbEntry is null ? null : r => openKbEntry(result.Edition, r),
                SelectDiagnostic)
            : null;
    }

    /// <summary>The literal translation (T-059); <c>null</c> when the edition has none (not implemented, or not requested).</summary>
    public TranslationViewModel? Translation { get; }

    /// <summary>Whether there is a translation to show.</summary>
    public bool HasTranslation => Translation is not null;

    /// <summary>The right-hand tab: <see cref="AbnormalitiesTab"/> (default) or <see cref="TranslationTab"/>.</summary>
    [ObservableProperty]
    public partial int SelectedSideTab { get; set; }

    /// <summary>The translation tab of the right-hand column.</summary>
    public const int TranslationTab = 1;

    /// <summary>The abnormalities tab of the right-hand column.</summary>
    public const int AbnormalitiesTab = 0;

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

    /// <summary>The tab header: <c>ISCN 2016 ✓ valid</c>, <c>ISCN 2016 ✗ not valid</c> or <c>ISCN 2020 · not implemented</c>.</summary>
    public string Header { get; }

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
        var text = !v.IsValid ? $"Not valid in ISCN {result.Edition}"
            : context.Effective is null && v.ValidContexts.Length < all
                ? $"Valid in ISCN {result.Edition} as {string.Join(" or ", v.ValidContexts.Select(DiagnosticItemViewModel.Label))} only"
                : $"Valid in ISCN {result.Edition}";
        return v.IsComplete ? text : text + " (as far as implemented)";
    }

    private static string CountsOf(SeverityCounts c)
    {
        var text = $"{c.Errors} error(s), {c.Warnings} warning(s), {c.Infos} info(s)";
        return c.Conditional > 0 ? text + $", {c.Conditional} context-dependent" : text;
    }
}
