using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Context;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The result of one edition (T-037): verdict, counts, segment notes, diagnostics, abnormalities and editor highlights.
/// One tab of the results area; a single-edition check has one panel, "All editions" one per known edition.
/// </summary>
public partial class EditionPanelViewModel : ObservableObject
{
    public EditionPanelViewModel(ValidationReport report, EditionResult result)
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
        Diagnostics = [.. result.Diagnostics.Select((d, i) => new DiagnosticItemViewModel(input, d, i))];
        CellLineGroups = CellLineGroupViewModel.From(result);
        Highlights = IsNotImplemented ? [] : HighlightBuilder.Build(input, result, report.Context.Effective);
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
