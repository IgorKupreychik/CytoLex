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
                ? Run(profile, input, segments, context, options.IsFragment)
                : NotImplemented(e))
            .ToImmutableArray();
        return new ValidationReport(input, segments, context, results);
    }

    private static EditionResult Run(EditionProfile profile, string input, ImmutableArray<Segment> segments, ContextInfo context, bool isFragment)
    {
        var diagnostics = new List<Diagnostic>();
        var segmentResults = ImmutableArray.CreateBuilder<SegmentResult>();
        foreach (var segment in segments)
        {
            if (!profile.ImplementedTypes.Contains(segment.Type))
            {
                segmentResults.Add(new SegmentResult(segment, EditionStatus.NotImplemented, $"{segment.Type.ToKbName()}: not implemented in the pilot", []));
                continue;
            }

            var tree = profile.Parser.Parse(input, segment, isFragment);
            var model = profile.Binder.Bind(tree);
            diagnostics.AddRange(tree.Diagnostics.Select(d => DiagnosticFactory.FromSyntax(profile.Kb, input, d, context.Effective)).OfType<Diagnostic>());

            foreach (var (rule, check) in profile.Checks.Where(c => c.Rule.AppliesToType(segment.Type)))
            {
                var ctx = new RuleContext(input, segment, tree, model, context, rule, profile.Kb);
                check.Check(ctx);
                diagnostics.AddRange(ctx.Findings.Select(f => DiagnosticFactory.FromFinding(rule, input, f, DiagnosticOrigin.Rule, context.Effective)).OfType<Diagnostic>());
            }

            segmentResults.Add(new SegmentResult(segment, EditionStatus.Implemented, null, tree.Constructs));
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
        return new EditionResult(profile.Edition, EditionStatus.Implemented, null, ValidityOf(sorted, context.Effective, complete), CountsOf(sorted), sorted, segs);
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
        []);
}
