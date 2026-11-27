using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>
/// Matches the association rules (T-108) against one edition result's abnormalities and loci (T-109). A rule's tree is
/// evaluated over the whole string (every segment and cell line): <c>all</c> needs every part, <c>any</c> one,
/// <c>not</c> that its part matches nothing. The tree is evaluated per sample context: the string's effective context,
/// or both contexts when it is unknown (like the translation's readings); the match lists the contexts it holds in.
/// Thread-safe.
/// </summary>
internal sealed class AssociationMatcher(AssociationRules rules, BandSet? bands, Func<string, AbnormalityEntry?> exactPattern)
{
    private static readonly ImmutableArray<SampleContext> _bothContexts = [SampleContext.Constitutional, SampleContext.Acquired];

    private readonly BandNesting _nesting = new(bands);

    /// <summary>The matches, in rule order; empty without rules.</summary>
    public ImmutableArray<AssociationMatch> Match(ContextInfo context, ImmutableArray<AbnormalityEntry> entries, ImmutableArray<LocusEntry> loci)
    {
        if (rules.Rules.IsEmpty || entries.IsEmpty)
        {
            return [];
        }

        var readings = context.Effective is { } effective ? [effective] : _bothContexts;
        var matches = ImmutableArray.CreateBuilder<AssociationMatch>();
        foreach (var rule in rules.Rules)
        {
            var contexts = new List<SampleContext>();
            var evidence = new List<AssociationEvidence>();
            foreach (var reading in readings)
            {
                var scope = new MatchScope(entries, loci, _nesting, reading, rule.Context);
                if (Evaluate(rule.Match, scope) is { } found)
                {
                    contexts.Add(reading);
                    evidence.AddRange(found);
                }
            }

            if (contexts.Count == 0)
            {
                continue;
            }

            matches.Add(new AssociationMatch(
                rule.Id,
                rule.Condition,
                rule.Context,
                rule.Strength,
                rule.Source,
                rule.Citation,
                rule.Defining,
                !rule.Reviewed,
                [.. contexts],
                context.Effective is not null,
                [.. evidence.Distinct().OrderBy(e => e.Span.Start).ThenBy(e => e.Span.Length).ThenBy(e => e.LeafText, StringComparer.Ordinal)]));
        }

        return matches.ToImmutable();
    }

    /// <summary>The evidence when <paramref name="node"/> holds (empty for a <c>not</c>), else <c>null</c>.</summary>
    private List<AssociationEvidence>? Evaluate(MatchNode node, MatchScope scope)
    {
        switch (node)
        {
            case AllNode all:
                var every = new List<AssociationEvidence>();
                foreach (var part in all.Parts)
                {
                    if (Evaluate(part, scope) is not { } found)
                    {
                        return null;
                    }

                    every.AddRange(found);
                }

                return every;
            case AnyNode any:
                var some = any.Parts.Select(p => Evaluate(p, scope)).OfType<List<AssociationEvidence>>().ToList();
                return some.Count == 0 ? null : [.. some.SelectMany(f => f)];
            case NotNode not:
                return Evaluate(not.Part, scope) is null ? [] : null;
            default:
                var leaf = Leaf(node, scope).ToList();
                return leaf.Count == 0 ? null : leaf;
        }
    }

    private IEnumerable<AssociationEvidence> Leaf(MatchNode node, MatchScope scope) => node switch
    {
        ExactLeaf exact => ExactMatcher.Match(exact, exactPattern(exact.Form), scope),
        FusionLeaf fusion => FusionMatcher.Match(fusion, scope),
        CopyLeaf copy => CopyMatcher.Match(copy, scope),
        _ => [],
    };
}
