using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.External;
using CytoLex.External.Comparison;

namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// The comparison of one batch row with the external checker (T-052): the Agreement and Triage cells, the side-by-side
/// detail (CytoLex | checker) and the triage editor of a disagreement. Empty until a comparison is shown.
/// </summary>
public partial class ComparisonRowViewModel : ObservableObject
{
    private readonly ComparisonViewModel _owner;

    /// <summary>Creates the (empty) comparison of a row.</summary>
    public ComparisonRowViewModel(ComparisonViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>The triage label box entries.</summary>
    public static IReadOnlyList<TriageLabelChoice> Labels => TriageLabelChoice.All;

    /// <summary>The compared string; <c>null</c> before a comparison.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItem))]
    [NotifyCanExecuteChangedFor(nameof(ApplyTriageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveTriageCommand))]
    public partial ComparisonItem? Item { get; private set; }

    /// <summary>Whether the row was compared.</summary>
    public bool HasItem => Item is not null;

    /// <summary>The Agreement cell: <c>= ✓</c>, <c>= ✗</c>, <c>≠ CytoLex ✗</c>, <c>≠ CytoGPS ✗</c>, <c>n/s</c> or <c>–</c>; empty before a comparison.</summary>
    [ObservableProperty]
    public partial string AgreementText { get; private set; } = string.Empty;

    /// <summary>The Agreement cell's tooltip: what it means and why a string was not compared.</summary>
    [ObservableProperty]
    public partial string? AgreementTip { get; private set; }

    /// <summary>Whether CytoLex and the checker disagree.</summary>
    [ObservableProperty]
    public partial bool IsDisagreement { get; private set; }

    /// <summary>Whether both agree.</summary>
    [ObservableProperty]
    public partial bool IsAgreement { get; private set; }

    /// <summary>A disagreement without a confirmed triage entry.</summary>
    [ObservableProperty]
    public partial bool IsUntriaged { get; private set; }

    /// <summary>The Triage cell: the label (<c>?</c> when not confirmed), <c>suggested: …</c>, <c>not triaged</c> or empty.</summary>
    [ObservableProperty]
    public partial string TriageText { get; private set; } = string.Empty;

    /// <summary>CytoLex's verdict, e.g. <c>CytoLex ISCN 2016: ✗ not valid</c> or <c>CytoLex ISCN 2016: ✓ valid after autocorrection</c>.</summary>
    [ObservableProperty]
    public partial string CytoLexText { get; private set; } = string.Empty;

    /// <summary>CytoLex's error and warning/info rule IDs, one line each.</summary>
    [ObservableProperty]
    public partial string CytoLexRules { get; private set; } = string.Empty;

    /// <summary>The checker's verdict and status, e.g. <c>CytoGPS: ✓ valid (Success)</c>.</summary>
    [ObservableProperty]
    public partial string CheckerText { get; private set; } = string.Empty;

    /// <summary>The checker's messages and the sent and repaired strings, one per line.</summary>
    [ObservableProperty]
    public partial string CheckerDetails { get; private set; } = string.Empty;

    /// <summary>The label picked in the triage editor.</summary>
    [ObservableProperty]
    public partial TriageLabelChoice? EditLabel { get; set; }

    /// <summary>Whether the edited entry is confirmed by a person.</summary>
    [ObservableProperty]
    public partial bool EditConfirmed { get; set; }

    /// <summary>The note of the edited entry.</summary>
    [ObservableProperty]
    public partial string EditNote { get; set; } = string.Empty;

    /// <summary>Whether the string has a triage entry.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveTriageCommand))]
    public partial bool HasTriage { get; private set; }

    /// <summary>Shows <paramref name="item"/> (<c>null</c>: not compared) and resets the triage editor to its entry.</summary>
    public void Show(ComparisonItem? item)
    {
        if (ReferenceEquals(item, Item))
        {
            return;
        }

        var keepEdit = item is not null && Item is not null && item.Triage == Item.Triage && item.SuggestedLabel == Item.SuggestedLabel;
        Item = item;
        var checker = _owner.CheckerName;
        IsDisagreement = item?.IsDisagreement ?? false;
        IsAgreement = item?.Agreement is Agreement.BothValid or Agreement.BothInvalid;
        IsUntriaged = IsDisagreement && item!.Triage is not { Confirmed: true };
        HasTriage = item?.Triage is not null;
        (AgreementText, AgreementTip) = item?.Agreement switch
        {
            null => (string.Empty, null),
            Agreement.BothValid => ("= ✓", $"Both valid: CytoLex and {checker} accept it."),
            Agreement.BothInvalid => ("= ✗", $"Both not valid: CytoLex and {checker} reject it."),
            Agreement.OnlyCytoLexInvalid => ("≠ CytoLex ✗", $"Disagree: CytoLex rejects it, {checker} accepts it."),
            Agreement.OnlyCheckerInvalid => ($"≠ {checker} ✗", $"Disagree: {checker} rejects it, CytoLex accepts it."),
            Agreement.NotSupported => ("n/s", $"Not supported by {checker}: {item.Reason}"),
            _ => ("–", $"Not compared: {item.Reason}"),
        };
        TriageText = item switch
        {
            { Triage: { } t } => TriageFile.LabelName(t.Label) + (t.Confirmed ? string.Empty : " ?"),
            { SuggestedLabel: { } s } => "suggested: " + TriageFile.LabelName(s),
            { IsDisagreement: true } => "not triaged",
            _ => string.Empty,
        };

        if (item is null)
        {
            CytoLexText = CytoLexRules = CheckerText = CheckerDetails = string.Empty;
            return;
        }

        var edition = _owner.Edition;
        CytoLexText = $"CytoLex ISCN {edition}: "
            + (item.CytoLexValid switch
            {
                true when item.CytoLexAutoCorrected => "✓ valid after autocorrection",
                true => "✓ valid",
                false => "✗ not valid",
                null => "not validated",
            })
            + (item.CytoLexValid is not null && !item.CytoLexComplete ? " · validated only in part" : string.Empty);
        CytoLexRules = string.Join('\n', new[]
        {
            item.CytoLexCorrected is { } corrected ? "Corrected: " + corrected : null,
            item.CytoLexErrors.IsEmpty ? null : "Errors: " + string.Join(", ", item.CytoLexErrors),
            item.CytoLexNotes.IsEmpty ? null : "Warnings/infos: " + string.Join(", ", item.CytoLexNotes),
        }.OfType<string>());

        var ext = item.External;
        CheckerText = $"{checker}: "
            + (ext.Verdict switch
            {
                ExternalVerdict.Valid => "✓ valid",
                ExternalVerdict.Invalid => "✗ not valid",
                ExternalVerdict.NotConfigured => "not configured",
                _ => "failed",
            })
            + (string.IsNullOrEmpty(ext.ToolStatus) ? string.Empty : $" ({ext.ToolStatus})");
        var details = ext.Messages.Select(m => $"{m.Kind}: {m.Text}").ToList();
        if (!string.Equals(ext.SentInput, ext.Input, StringComparison.Ordinal))
        {
            details.Add("Sent: " + ext.SentInput);
        }

        if (!string.IsNullOrEmpty(ext.RevisedInput) && !string.Equals(ext.RevisedInput, ext.SentInput, StringComparison.Ordinal))
        {
            details.Add("Repaired: " + ext.RevisedInput);
        }

        CheckerDetails = string.Join('\n', details);
        if (!keepEdit)
        {
            ResetEdit();
        }
    }

    private void ResetEdit()
    {
        var entry = Item?.Triage;
        EditLabel = entry is not null ? TriageLabelChoice.Of(entry.Label)
            : Item?.SuggestedLabel is { } suggested ? TriageLabelChoice.Of(suggested)
            : null;
        EditConfirmed = entry?.Confirmed ?? false;
        EditNote = entry?.Note ?? string.Empty;
    }

    private bool CanApplyTriage() => Item is { IsDisagreement: true };

    /// <summary>Writes the edited label, confirmation and note into the triage file (kept: <c>expected</c>, <c>kb_rule</c>).</summary>
    [RelayCommand(CanExecute = nameof(CanApplyTriage))]
    private void ApplyTriage()
    {
        if (Item is not { } item || EditLabel is not { } label)
        {
            return;
        }

        var old = item.Triage;
        var note = string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim();
        _owner.SetTriageEntry(new TriageEntry(item.Item.Input, label.Label, EditConfirmed, note, old?.Expected, old?.KbRule));
    }

    /// <summary>Removes the string's triage entry.</summary>
    [RelayCommand(CanExecute = nameof(HasTriage))]
    private void RemoveTriage()
    {
        if (Item is { } item)
        {
            _owner.RemoveTriageEntry(item.Item.Input);
        }
    }
}
