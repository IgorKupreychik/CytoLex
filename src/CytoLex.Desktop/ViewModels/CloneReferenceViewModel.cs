using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The expandable <c>idem</c>/<c>sl</c>/<c>sdl</c> row of the abnormalities panel (T-070): what the reference stands for
/// (<c>sl = stemline → line 1</c>), the text of the line it refers to, and the changes inherited from it.
/// </summary>
public sealed partial class CloneReferenceViewModel : ObservableObject
{
    /// <param name="line">The cell line written with the reference.</param>
    /// <param name="result">The edition result (cell lines and entries).</param>
    /// <param name="lineNumber">The number shown for a cell line index (<c>line N</c>).</param>
    /// <param name="toEditor">Maps spans to the editor text (T-097).</param>
    public CloneReferenceViewModel(CellLineSummary line, EditionResult result, Func<int, int> lineNumber, Func<TextSpan, TextSpan> toEditor)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(lineNumber);
        ArgumentNullException.ThrowIfNull(toEditor);

        var reference = line.Reference ?? throw new ArgumentException("The cell line has no reference.", nameof(line));
        Label = LabelOf(reference, lineNumber);
        TargetText = reference.TargetLine is { } t ? result.CellLines[t].Text : null;
        Inherited =
        [
            .. line.Inherited.Select(i => new InheritedItemViewModel(
                i,
                result.Abnormalities[i.Entry],
                lineNumber(i.SourceCellLine),
                i.RemovedBy is { } by ? result.Abnormalities[by].Text : null,
                toEditor)),
        ];
    }

    /// <summary>Whether the inherited changes are shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>E.g. <c>sl = stemline → line 1</c>, <c>sdl1 = sideline 1 → line 2</c>, <c>idem×2 = first karyotype ×2 → line 1</c>.</summary>
    public string Label { get; }

    /// <summary>The line referred to, as written; <c>null</c> when the reference has no target.</summary>
    public string? TargetText { get; }

    /// <summary>Whether <see cref="TargetText"/> is set.</summary>
    public bool HasTarget => TargetText is not null;

    /// <summary>The inherited changes (removed ones last in their source order, marked).</summary>
    public ImmutableArray<InheritedItemViewModel> Inherited { get; }

    /// <summary>The expander header: <c>3 inherited changes</c>.</summary>
    public string InheritedHeader
    {
        get
        {
            var kept = Inherited.Count(i => !i.IsRemoved);
            return kept == 1 ? "1 inherited change" : $"{kept} inherited changes";
        }
    }

    /// <summary>The label of a reference (§11.1.4): the word, what it stands for, and the line it points to.</summary>
    internal static string LabelOf(CloneReferenceSummary reference, Func<int, int> lineNumber)
    {
        var number = reference.Number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var meaning = (reference.Symbol, reference.Number) switch
        {
            ("idem", _) => "first karyotype",
            ("sl", null or 1) => "stemline",
            ("sl", { } n) => $"stemline {n}",
            ("sdl", null) => "sideline",
            ("sdl", { } n) => $"sideline {n}",
            _ => reference.Symbol,
        };
        var times = reference.Multiplier > 1 ? $"×{reference.Multiplier}" : string.Empty;
        var target = reference.TargetLine is { } t ? $"line {lineNumber(t)}" : "no clone to refer to";
        return $"{reference.Symbol}{number}{times} = {meaning}{(times.Length > 0 ? " " + times : string.Empty)} → {target}";
    }
}
