using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CytoLex.Core.Semantics;
using CytoLex.Core.Validation;
using CytoLex.Desktop.Highlighting;
using CytoLex.Desktop.Rules;

namespace CytoLex.Desktop.ViewModels;

/// <summary>An entry of the rule browser's section filter.</summary>
/// <param name="Section">The section, e.g. <c>9.2.17</c>; <c>null</c> = all sections.</param>
/// <param name="Label">The text shown.</param>
public sealed record SectionChoice(string? Section, string Label)
{
    /// <summary>"All sections".</summary>
    public static SectionChoice All { get; } = new(null, "All sections");

    /// <inheritdoc/>
    public override string ToString() => Label;
}

/// <summary>
/// The rule browser (T-039): full-text search over the rules of one edition in <c>build/kb.sqlite</c> (<see cref="RuleStore"/>),
/// a section filter, the hit list and the selected rule's detail. <see cref="OpenRule"/> is the diagnostic → rule link.
/// Vocab items (symbols, banding codes) are listed after the rules with their own detail (T-059), so the translation's
/// KB links (<see cref="OpenKbRef"/>) have a target.
/// A missing or stale database only disables this tab (<see cref="ErrorText"/>).
/// </summary>
public partial class RuleBrowserViewModel : ObservableObject, IDisposable
{
    private readonly RuleStore? _store;
    private readonly Action<string, string>? _checkExample;
    private bool _suspendRefresh;

    /// <summary>The browser over the database next to the app (<see cref="RuleStore.DefaultPath"/>).</summary>
    public RuleBrowserViewModel()
        : this(RuleStore.DefaultPath, null, null)
    {
    }

    /// <summary>The browser over the database at <paramref name="databasePath"/>.</summary>
    /// <param name="databasePath">The <c>kb.sqlite</c> file.</param>
    /// <param name="defaultEdition">The edition shown first if the database has it (else the newest one).</param>
    /// <param name="checkExample">Opens an example (text, edition) in the single-string check.</param>
    public RuleBrowserViewModel(string databasePath, string? defaultEdition, Action<string, string>? checkExample)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        _checkExample = checkExample;
        try
        {
            _store = RuleStore.Open(databasePath);
        }
        catch (RuleStoreException ex)
        {
            ErrorText = ex.Message;
            Editions = [];
            return;
        }

        Editions = _store.Editions;
        _suspendRefresh = true;
        SelectedEdition = defaultEdition is not null && Editions.Contains(defaultEdition) ? defaultEdition : Editions.LastOrDefault();
        _suspendRefresh = false;
        Refresh();
    }

    /// <summary>Why the browser is unavailable (database missing or of another format); <c>null</c> when it works.</summary>
    public string? ErrorText { get; }

    /// <summary>Whether <see cref="ErrorText"/> is set.</summary>
    public bool HasError => ErrorText is not null;

    /// <summary>Whether the browser works.</summary>
    public bool IsAvailable => ErrorText is null;

    /// <summary>The editions in the database.</summary>
    public ImmutableArray<string> Editions { get; }

    /// <summary>The edition browsed; changing it reloads the sections and hits.</summary>
    [ObservableProperty]
    public partial string? SelectedEdition { get; set; }

    /// <summary>The section filter entries ("All sections" first).</summary>
    public ObservableCollection<SectionChoice> Sections { get; } = [];

    /// <summary>The section filter.</summary>
    [ObservableProperty]
    public partial SectionChoice? SelectedSection { get; set; }

    /// <summary>The search text (IDs, words of titles, statements, examples and quotes, example strings like <c>t(9;22)</c>).</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>The rules found.</summary>
    public ObservableCollection<RuleHitViewModel> Hits { get; } = [];

    /// <summary>The rule selected in the list; its detail is <see cref="Detail"/>.</summary>
    [ObservableProperty]
    public partial RuleHitViewModel? SelectedHit { get; set; }

    /// <summary>The detail of the selected rule.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial RuleDetailViewModel? Detail { get; set; }

    /// <summary>Whether a rule is shown.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary>The detail of the selected vocab item (T-059).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVocabDetail))]
    public partial VocabDetailViewModel? VocabDetail { get; set; }

    /// <summary>Whether a vocab item is shown.</summary>
    public bool HasVocabDetail => VocabDetail is not null;

    /// <summary><c>177 rule(s)</c>, <c>3 rule(s) match</c>, or a note on a rule that could not be opened.</summary>
    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    /// <summary>
    /// Shows rule <paramref name="id"/> of <paramref name="edition"/> (the diagnostic → rule link): switches the edition,
    /// clears the search and section filter if they hide the rule, and selects it.
    /// </summary>
    /// <returns>Whether the rule exists in the database.</returns>
    public bool OpenRule(string edition, string id)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(id);
        if (_store is null)
        {
            return false;
        }

        if (!Editions.Contains(edition) || _store.Rule(edition, id) is null)
        {
            ResultText = $"{id} is not a rule of the ISCN {edition} KB.";
            return false;
        }

        SelectedEdition = edition; // refreshes when it changes
        if (!Find(id))
        {
            _suspendRefresh = true;
            Query = string.Empty;
            SelectedSection = SectionChoice.All;
            _suspendRefresh = false;
            Refresh();
        }

        SelectedHit = Hits.FirstOrDefault(h => h.Kind == KbEntryKind.Rule && h.Id == id);
        return SelectedHit is not null;
    }

    /// <summary>
    /// Shows the vocab item <paramref name="label"/> of <paramref name="vocabulary"/> in <paramref name="edition"/> (a translation
    /// link, T-059), like <see cref="OpenRule"/>.
    /// </summary>
    /// <returns>Whether the item exists in the database.</returns>
    public bool OpenVocab(string edition, string vocabulary, string label)
    {
        ArgumentNullException.ThrowIfNull(edition);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(label);
        if (_store is null)
        {
            return false;
        }

        var id = $"{vocabulary}/{label}";
        if (!Editions.Contains(edition) || _store.Vocab(edition, vocabulary, label) is null)
        {
            ResultText = $"{id} is not a vocab item of the ISCN {edition} KB.";
            return false;
        }

        SelectedEdition = edition;
        if (!Hits.Any(h => h.Kind == KbEntryKind.Vocab && h.Id == id))
        {
            _suspendRefresh = true;
            Query = string.Empty;
            SelectedSection = SectionChoice.All;
            _suspendRefresh = false;
            Refresh();
        }

        SelectedHit = Hits.FirstOrDefault(h => h.Kind == KbEntryKind.Vocab && h.Id == id);
        return SelectedHit is not null;
    }

    /// <summary>Opens the KB entry of a translation link (T-059): a rule or a vocab item. Bands have no browser entry (tooltip only).</summary>
    /// <returns>Whether the entry was found.</returns>
    public bool OpenKbRef(string edition, KbRef kbRef)
    {
        ArgumentNullException.ThrowIfNull(edition);
        switch (kbRef.Kind)
        {
            case KbRefKind.Rule:
                return OpenRule(edition, kbRef.Id);
            case KbRefKind.Vocab:
                var slash = kbRef.Id.IndexOf('/', StringComparison.Ordinal);
                return slash > 0 && OpenVocab(edition, kbRef.Id[..slash], kbRef.Id[(slash + 1)..]);
            case KbRefKind.Band:
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store?.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnSelectedEditionChanged(string? value)
    {
        if (_store is null || value is null)
        {
            return;
        }

        var keep = SelectedSection?.Section;
        var suspended = _suspendRefresh;
        _suspendRefresh = true;
        Sections.Clear();
        Sections.Add(SectionChoice.All);
        foreach (var section in _store.Sections(value))
        {
            Sections.Add(new SectionChoice(section, "§" + section));
        }

        SelectedSection = Sections.FirstOrDefault(s => s.Section == keep) ?? SectionChoice.All;
        _suspendRefresh = suspended;
        Refresh();
    }

    partial void OnSelectedSectionChanged(SectionChoice? value) => Refresh();

    partial void OnQueryChanged(string value) => Refresh();

    partial void OnSelectedHitChanged(RuleHitViewModel? value)
    {
        var isVocab = value?.Kind == KbEntryKind.Vocab;
        var detail = value is null || _store is null || isVocab ? null : _store.Rule(value.Edition, value.Id);
        Detail = detail is null ? null : new RuleDetailViewModel(detail, OpenRelated, _checkExample);
        var vocab = value is null || _store is null || !isVocab ? null : VocabOf(value);
        VocabDetail = vocab is null ? null : new VocabDetailViewModel(vocab);
    }

    private bool Find(string id) => Hits.Any(h => h.Kind == KbEntryKind.Rule && h.Id == id);

    private VocabDetail? VocabOf(RuleHitViewModel hit)
    {
        var slash = hit.Id.IndexOf('/', StringComparison.Ordinal);
        return slash > 0 ? _store!.Vocab(hit.Edition, hit.Id[..slash], hit.Id[(slash + 1)..]) : null;
    }

    private void OpenRelated(string id)
    {
        if (SelectedEdition is { } edition)
        {
            OpenRule(edition, id);
        }
    }

    private void Refresh()
    {
        if (_suspendRefresh || _store is null || SelectedEdition is not { } edition)
        {
            return;
        }

        var selected = SelectedHit is { } current ? (current.Kind, current.Id) : default((KbEntryKind, string)?);
        var hits = _store.Search(edition, Query, SelectedSection?.Section);
        Hits.Clear();
        foreach (var hit in hits)
        {
            Hits.Add(new RuleHitViewModel(hit));
        }

        var hasQuery = RuleStore.ToFtsQuery(Query) is not null;
        var rules = hits.Count(h => h.Kind == KbEntryKind.Rule);
        var vocab = hits.Length - rules;
        var counts = $"{rules} rule(s)" + (vocab > 0 ? $", {vocab} vocab item(s)" : string.Empty);
        ResultText = hasQuery ? counts + " match" : counts;
        SelectedHit = Hits.FirstOrDefault(h => (h.Kind, h.Id) == selected) ?? (hasQuery ? Hits.FirstOrDefault() : null);
    }

    internal static Severity SeverityOf(string severity) => severity switch
    {
        "error" => Severity.Error,
        "warning" => Severity.Warning,
        _ => Severity.Info,
    };
}

/// <summary>One row of the rule browser's hit list.</summary>
public sealed class RuleHitViewModel(RuleHit hit)
{
    /// <summary>The hit.</summary>
    public RuleHit Hit { get; } = hit;

    /// <summary>The edition.</summary>
    public string Edition => Hit.Edition;

    /// <summary>The rule ID, or <c>&lt;vocabulary&gt;/&lt;label&gt;</c> of a vocab item.</summary>
    public string Id => Hit.Id;

    /// <summary>Rule or vocab item.</summary>
    public KbEntryKind Kind => Hit.Kind;

    /// <summary>The title.</summary>
    public string Title => Hit.Title;

    /// <summary>
    /// <c>§9.2.17 · error</c>, plus <c>· quote</c> when only a source quote matched; <c>§3 · vocab · abbreviation</c> for a vocab item.
    /// </summary>
    public string Meta => Hit.Kind == KbEntryKind.Vocab
        ? $"§{Hit.Section} · vocab · {Hit.Severity}"
        : $"§{Hit.Section} · {Hit.Severity}" + (Hit.Match == RuleMatch.Quote ? " · matched in a quote" : string.Empty);

    /// <summary>The matching text.</summary>
    public string? Snippet => Hit.Snippet;

    /// <summary>Whether <see cref="Snippet"/> is set.</summary>
    public bool HasSnippet => Hit.Snippet is not null;

    /// <summary>The severity colour; a neutral colour for a vocab item.</summary>
    public IBrush SeverityBrush { get; } = hit.Kind == KbEntryKind.Vocab
        ? VocabDetailViewModel.KindBrush
        : SeverityBrushes.For(RuleBrowserViewModel.SeverityOf(hit.Severity));
}

/// <summary>The detail of one vocab item in the rule browser (T-059).</summary>
public sealed class VocabDetailViewModel
{
    public VocabDetailViewModel(VocabDetail item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Item = item;
        Header = $"ISCN {item.Edition} §{item.Section} · vocab/{item.File}";
        var tokens = item.Tokens.IsDefaultOrEmpty ? "none (not written in a notation string)" : string.Join("  ", item.Tokens);
        TokensText = "Tokens: " + tokens
            + (item.PairOpen is { } open && item.PairClose is { } close ? $" · pair {open} … {close}" : string.Empty)
            + (item.EquivalentForms ? " · typed forms of the printed symbol" : string.Empty);
        Meanings = [.. item.Meanings.Select(m => m.Sections.IsDefaultOrEmpty ? m.Text : $"{m.Text} (§{string.Join(", §", m.Sections)})")];
    }

    /// <summary>The chip colour of vocab items (neutral, not a severity).</summary>
    public static IBrush KindBrush { get; } = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(0x03, 0x83, 0x87));

    /// <summary>The item.</summary>
    public VocabDetail Item { get; }

    /// <summary>The KB ID shown, <c>vocab:symbols/del</c>.</summary>
    public string Id => "vocab:" + Item.Id;

    /// <summary>The label as printed.</summary>
    public string Label => Item.Label;

    /// <summary>The title (first meaning).</summary>
    public string Title => Item.Title;

    /// <summary>The entry kind (<c>abbreviation</c>, <c>code</c>, …).</summary>
    public string Kind => Item.Kind;

    /// <summary><c>ISCN 2016 §3 · vocab/ch03/03-symbols.yaml</c>.</summary>
    public string Header { get; }

    /// <summary>The tokens (and pair / typed forms).</summary>
    public string TokensText { get; }

    /// <summary>Each meaning with the sections that use it.</summary>
    public ImmutableArray<string> Meanings { get; }

    /// <summary>The note, if any.</summary>
    public string? Note => Item.Note;

    /// <summary>Whether <see cref="Note"/> is set.</summary>
    public bool HasNote => Item.Note is { Length: > 0 };

    /// <summary>The rendered book citation of the printed row.</summary>
    public string Citation => Item.Citation;
}

/// <summary>The detail of one rule in the rule browser.</summary>
public sealed class RuleDetailViewModel
{
    public RuleDetailViewModel(RuleDetail rule, Action<string>? openRelated, Action<string, string>? checkExample)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Rule = rule;
        SeverityBrush = SeverityBrushes.For(RuleBrowserViewModel.SeverityOf(rule.Severity));
        Header = $"ISCN {rule.Edition} §{rule.Section} · {rule.File}";
        Scope = "Applies to: " + string.Join(", ", rule.AppliesTo)
            + (rule.Contexts.IsDefaultOrEmpty ? " · every context" : " · " + string.Join(", ", rule.Contexts) + " only");
        Fix = rule.FixLevel is { } level
            ? $"Fix level {level}" + (rule.FixKind is { } kind ? $" ({kind})" : string.Empty) + (rule.FixHint is { } hint ? ": " + hint.Trim() : string.Empty)
            : null;
        var flags = new List<string>();
        if (rule.NeedsReview)
        {
            flags.Add("Book-ambiguous (needs review; findings capped at warning)");
        }

        if (!rule.ParaphraseChecked)
        {
            flags.Add("Paraphrase not yet checked");
        }

        Review = flags.Count == 0 && rule.ReviewNote is null
            ? null
            : string.Join(" · ", flags) + (rule.ReviewNote is { Length: > 0 } note ? (flags.Count > 0 ? ": " : string.Empty) + note.Trim() : string.Empty);
        Related = [.. rule.Related.Select(id => new RelatedRuleViewModel(id, openRelated))];
        ValidExamples = [.. rule.Examples.Where(e => e.IsValid).Select(e => new RuleExampleViewModel(rule.Edition, e, checkExample))];
        InvalidExamples = [.. rule.Examples.Where(e => !e.IsValid).Select(e => new RuleExampleViewModel(rule.Edition, e, checkExample))];
        Quotes = rule.Quotes;
    }

    /// <summary>The rule.</summary>
    public RuleDetail Rule { get; }

    /// <summary>The rule ID.</summary>
    public string Id => Rule.Id;

    /// <summary>The title.</summary>
    public string Title => Rule.Title;

    /// <summary>The KB severity.</summary>
    public string Severity => Rule.Severity;

    /// <summary>The severity colour.</summary>
    public IBrush SeverityBrush { get; }

    /// <summary><c>ISCN 2016 §9.2.17 · ch09/09-2-17.yaml</c>.</summary>
    public string Header { get; }

    /// <summary>The paraphrased statement.</summary>
    public string Statement => Rule.Statement;

    /// <summary>The rendered book citations.</summary>
    public ImmutableArray<string> Citations => Rule.Citations;

    /// <summary>The notation types and sample contexts the rule applies to.</summary>
    public string Scope { get; }

    /// <summary>The KB fix hint, if any.</summary>
    public string? Fix { get; }

    /// <summary>Whether <see cref="Fix"/> is set.</summary>
    public bool HasFix => Fix is not null;

    /// <summary>Whether the KB flags the rule's fix for autocorrection (<c>fix.auto</c>, T-097): the badge in the detail.</summary>
    public bool IsAutoCorrected => Rule.FixAuto;

    /// <summary>The review state (book-ambiguous, paraphrase not checked, note), if any.</summary>
    public string? Review { get; }

    /// <summary>Whether <see cref="Review"/> is set.</summary>
    public bool HasReview => Review is not null;

    /// <summary>The related rules (links).</summary>
    public ImmutableArray<RelatedRuleViewModel> Related { get; }

    /// <summary>Whether there are related rules.</summary>
    public bool HasRelated => !Related.IsEmpty;

    /// <summary>The valid KB examples.</summary>
    public ImmutableArray<RuleExampleViewModel> ValidExamples { get; }

    /// <summary>Whether there are valid examples.</summary>
    public bool HasValidExamples => !ValidExamples.IsEmpty;

    /// <summary>The invalid KB examples.</summary>
    public ImmutableArray<RuleExampleViewModel> InvalidExamples { get; }

    /// <summary>Whether there are invalid examples.</summary>
    public bool HasInvalidExamples => !InvalidExamples.IsEmpty;

    /// <summary>The verbatim source quotes; empty in a KB build without quote files.</summary>
    public ImmutableArray<RuleQuote> Quotes { get; }

    /// <summary>Whether there are quotes.</summary>
    public bool HasQuotes => !Quotes.IsEmpty;

    /// <summary>Shown instead of the quotes when there are none.</summary>
    public static string NoQuotesText => "No source quotes in this KB build.";
}

/// <summary>A related-rule link.</summary>
public sealed partial class RelatedRuleViewModel(string id, Action<string>? open)
{
    /// <summary>The related rule ID.</summary>
    public string Id { get; } = id;

    /// <summary>Opens the related rule in the browser.</summary>
    [RelayCommand]
    private void Open() => open?.Invoke(Id);
}

/// <summary>A KB example in the rule detail; "Check" opens it in the single-string check.</summary>
public sealed partial class RuleExampleViewModel(string edition, RuleExample example, Action<string, string>? check)
{
    /// <summary>The example.</summary>
    public RuleExample Example { get; } = example;

    /// <summary>The example string.</summary>
    public string Text => Example.Text;

    /// <summary>Why, corrected form, note and citation, as one line; <c>null</c> if none.</summary>
    public string? Info { get; } = Join(example);

    /// <summary>Whether <see cref="Info"/> is set.</summary>
    public bool HasInfo => Info is not null;

    /// <summary>Opens the example in the single-string check, in the rule's edition.</summary>
    [RelayCommand]
    private void Check() => check?.Invoke(Example.Text, edition);

    private static string? Join(RuleExample e)
    {
        var parts = new List<string>();
        if (e.Why is { Length: > 0 } why)
        {
            parts.Add(why.Trim());
        }

        if (e.Corrected is { Length: > 0 } corrected)
        {
            parts.Add("corrected: " + corrected);
        }

        if (e.Note is { Length: > 0 } note)
        {
            parts.Add(note.Trim());
        }

        if (e.Citation is { Length: > 0 } cite)
        {
            parts.Add(cite);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
