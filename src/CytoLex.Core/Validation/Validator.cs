using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Rules;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;
using CytoLex.Core.Translation;
using CytoLex.Core.Validation.Suggestions;

namespace CytoLex.Core.Validation;

/// <summary>
/// The validator (docs/design/core.md §2, §7, §9): splits the input into segments, infers the context, and runs each
/// requested edition's profile (parser + rule checks), returning one result per edition. Immutable and thread-safe.
/// </summary>
public sealed class Validator
{
    /// <summary>The ISCN editions the core knows of, implemented or not (an all-editions call reports each).</summary>
    public static readonly ImmutableArray<string> IscnEditions = ["2016", "2020", "2024"];

    private static readonly Lazy<Validator> _default = new(() => new Validator(KnowledgeBase.LoadEmbedded()));
    private static readonly ImmutableArray<SampleContext> _allContexts = [.. Enum.GetValues<SampleContext>()];

    private readonly FrozenDictionary<string, EditionProfile> _profiles;
    private readonly FrozenDictionary<string, EditionCoverage> _coverage;

    /// <summary>A validator over <paramref name="kb"/> with the rule checks of <c>CytoLex.Core</c>.</summary>
    /// <exception cref="InvalidOperationException">The startup check failed (a rule ID unknown to the KB, or an ambiguous binding).</exception>
    public Validator(IKnowledgeBase kb)
        : this(kb, RuleCatalog.Scan(typeof(Validator).Assembly))
    {
    }

    internal Validator(IKnowledgeBase kb, RuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(kb);
        ArgumentNullException.ThrowIfNull(catalog);
        catalog.Verify(kb);

        var profiles = new Dictionary<string, EditionProfile>(StringComparer.Ordinal);
        foreach (var edition in kb.Editions)
        {
            if (kb.ForEdition(edition) is { } ekb && EditionProfiles.ImplementedTypes.TryGetValue(edition, out var types))
            {
                profiles[edition] = new EditionProfile(edition, ekb, new Parser(ekb.Vocab), types, catalog.ChecksFor(ekb), new Binder(ekb));
            }
        }

        _profiles = profiles.ToFrozenDictionary(StringComparer.Ordinal);
        _coverage = _profiles.Values.ToFrozenDictionary(
            p => p.Edition,
            p => new EditionCoverage(
                p.Edition,
                [.. p.Checks.Select(c => c.Rule.Id)],
                [.. Parser.RuleIds.Where(id => p.Kb.Rule(id) is not null).Order(StringComparer.Ordinal)]),
            StringComparer.Ordinal);
        KnownEditions = [.. kb.Editions.Union(IscnEditions, StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        ImplementedEditions = [.. _profiles.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>The validator over the KB embedded in this assembly (created once).</summary>
    public static Validator Default => _default.Value;

    /// <summary>Every known edition, oldest first: the KB's plus <see cref="IscnEditions"/>.</summary>
    public ImmutableArray<string> KnownEditions { get; }

    /// <summary>The editions with a profile and KB data, oldest first.</summary>
    public ImmutableArray<string> ImplementedEditions { get; }

    /// <summary>The edition used when none is chosen: the newest implemented one.</summary>
    public string DefaultEdition => ImplementedEditions.IsEmpty ? KnownEditions[^1] : ImplementedEditions[^1];

    /// <summary>JSON options for serialising results (enums as names), for export (T-038) and the web service (T-044).</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Which rules <paramref name="edition"/> implements; <c>null</c> for an edition without a profile.</summary>
    public EditionCoverage? Coverage(string edition) => _coverage.GetValueOrDefault(edition);

    /// <summary>Validates <paramref name="input"/> against the editions in <paramref name="options"/>.</summary>
    public ValidationReport Validate(string input, ValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= ValidationOptions.Default;

        var segments = Segmenter.Split(input);
        var context = ContextInference.Infer(input, segments, options.ContextHint);
        var editions = options.Editions.IsAll ? KnownEditions : [options.Editions.Edition ?? DefaultEdition];

        var results = editions
            .Select(e => _profiles.TryGetValue(e, out var profile)
                ? RunEdition(profile, input, segments, context, options)
                : NotImplemented(e))
            .ToImmutableArray();
        return new ValidationReport(input, segments, context, results);
    }

    /// <summary>One edition; with <see cref="ValidationOptions.AutoCorrect"/>, autocorrected to a fixed point (T-096).</summary>
    private static EditionResult RunEdition(EditionProfile profile, string input, ImmutableArray<Segment> segments, ContextInfo context, ValidationOptions options)
    {
        var first = Run(profile, input, segments, context, options);
        if (!options.AutoCorrect)
        {
            return first;
        }

        return AutoCorrector.Correct(profile.Kb, input, first, text =>
        {
            var corrected = Segmenter.Split(text);
            return Run(profile, text, corrected, ContextInference.Infer(text, corrected, options.ContextHint), options);
        });
    }

    private static EditionResult Run(EditionProfile profile, string input, ImmutableArray<Segment> segments, ContextInfo context, ValidationOptions options)
    {
        var isFragment = options.IsFragment;
        var diagnostics = new List<Diagnostic>();
        var models = new List<(Segment Segment, SegmentModel? Model)>();
        var segmentResults = ImmutableArray.CreateBuilder<SegmentResult>();
        var abnormalities = new AbnormalityList(input);
        SymbolSuggester? suggester = null;
        foreach (var segment in segments)
        {
            if (!profile.ImplementedTypes.Contains(segment.Type))
            {
                segmentResults.Add(new SegmentResult(segment, EditionStatus.NotImplemented, $"{segment.Type.ToKbName()}: not implemented in the pilot", []));
                models.Add((segment, null));
                continue;
            }

            var tree = profile.Parser.Parse(input, segment, isFragment);
            SegmentModel model = profile.Binder.BindSegment(tree);
            models.Add((segment, model));
            if (model is KaryotypeModel karyotype)
            {
                abnormalities.Add(segment.Index, karyotype);
            }
            else if (model is ArrModel arr)
            {
                abnormalities.Add(segment.Index, arr); // T-072
            }
            else if (model is FishModel fish)
            {
                abnormalities.Add(segment.Index, fish); // T-092
            }

            foreach (var d in tree.Diagnostics)
            {
                if (DiagnosticFactory.FromSyntax(profile.Kb, input, d, context.Effective) is not { } diagnostic)
                {
                    continue;
                }

                if (d.RuleId == Parser.ListedOnly && profile.Kb.Rule(d.RuleId) is { } listedOnly)
                {
                    // "Did you mean" options for an unlisted word (level 2, T-041).
                    suggester ??= new SymbolSuggester(profile.Kb, profile.Parser);
                    diagnostic = DiagnosticFactory.WithSuggestions(diagnostic, listedOnly, input, suggester.Suggest(input, segment, isFragment, d.Span));
                }

                diagnostics.Add(diagnostic);
            }

            foreach (var (rule, check) in profile.Checks.Where(c => c.Rule.AppliesToType(segment.Type)))
            {
                var ctx = new RuleContext(input, segment, tree, model, context, rule, profile.Kb, segments, profile.ImplementedTypes);
                check.Check(ctx);
                diagnostics.AddRange(ctx.Findings.Select(f => DiagnosticFactory.FromFinding(rule, input, f, DiagnosticOrigin.Rule, context.Effective)).OfType<Diagnostic>());
            }

            segmentResults.Add(new SegmentResult(segment, EditionStatus.Implemented, null, OpenConstructs(tree, model)));
        }

        if (context.IsContradictory && !context.Markers.IsEmpty)
        {
            diagnostics.Add(DiagnosticFactory.ContextConflict(context));
        }

        ImmutableArray<Diagnostic> sorted =
        [
            .. DiagnosticFactory.WithRuleWideFixes(diagnostics, input)
                .OrderBy(d => d.Span.Start)
                .ThenByDescending(d => d.Severity)
                .ThenBy(d => d.RuleId, StringComparer.Ordinal),
        ];
        var segs = segmentResults.ToImmutable();
        var complete = segs.All(s => s.Status == EditionStatus.Implemented && s.Constructs.IsEmpty);
        var (cellLines, entries) = abnormalities.Build(sorted);
        var validity = ValidityOf(sorted, context.Effective, complete);
        var translation = options.IncludeTranslation
            ? TranslationBuilder.Build(profile.Kb, input, models, sorted, context, validity.IsValid, options.IncludeCoordinates)
            : null;
        return new EditionResult(profile.Edition, EditionStatus.Implemented, null, validity, CountsOf(sorted), sorted, segs, cellLines, entries, translation)
        {
            Loci = LocusList.Build(input, profile.Kb.Bands, cellLines, entries), // T-101
        };
    }

    /// <summary>
    /// The parser's not-implemented constructs without the underlining the binder read as a chromosome identifier
    /// (homologue distinction, T-042): only underlining elsewhere leaves the segment unvalidated.
    /// </summary>
    private static ImmutableArray<NotImplementedConstruct> OpenConstructs(SyntaxTree tree, SegmentModel model)
    {
        if (model.UnderlinedChromosomes.IsEmpty)
        {
            return tree.Constructs;
        }

        var bound = model.UnderlinedChromosomes.ToHashSet();
        return [.. tree.Constructs.Where(c => c.Kind != ConstructKind.Underlining || !bound.Contains(c.Span))];
    }

    /// <summary>Validity per §9: known context → no errors; unknown → the contexts without (firm or conditional) errors.</summary>
    private static Validity ValidityOf(ImmutableArray<Diagnostic> diagnostics, SampleContext? effective, bool complete)
    {
        if (effective is { } known)
        {
            var valid = !diagnostics.Any(d => d.Severity == Severity.Error);
            return new Validity(valid, valid ? [known] : [], complete);
        }

        var firmError = diagnostics.Any(d => d.Condition is null && d.Severity == Severity.Error);
        ImmutableArray<SampleContext> contexts = firmError
            ? []
            : [.. _allContexts.Where(c => !diagnostics.Any(d => d.Condition?.In(c) == Severity.Error))];
        return new Validity(!contexts.IsEmpty, contexts, complete);
    }

    private static SeverityCounts CountsOf(ImmutableArray<Diagnostic> d) => new(
        d.Count(x => x.Severity == Severity.Error),
        d.Count(x => x.Severity == Severity.Warning),
        d.Count(x => x.Severity == Severity.Info),
        d.Count(x => x.IsConditional));

    private EditionResult NotImplemented(string edition) => new(
        edition,
        EditionStatus.NotImplemented,
        KnownEditions.Contains(edition) ? $"ISCN {edition}: not implemented" : $"ISCN {edition}: unknown edition",
        new Validity(false, [], false),
        SeverityCounts.None,
        [],
        [],
        [],
        []);
}
