using System.Collections.Immutable;
using System.Reflection;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;

namespace CytoLex.Core.Rules;

/// <summary>Which rules an edition implements, and how (docs/design/core.md §7.2 coverage list; T-025 report).</summary>
/// <param name="Edition">The ISCN edition.</param>
/// <param name="CheckedRuleIds">KB rules with a <see cref="KbRuleAttribute"/> check for this edition, sorted.</param>
/// <param name="ParserRuleIds">KB rules the parser reports syntax errors for (a surface part of the rule only), sorted.</param>
public sealed record EditionCoverage(string Edition, ImmutableArray<string> CheckedRuleIds, ImmutableArray<string> ParserRuleIds)
{
    /// <summary>Whether <paramref name="ruleId"/> has a rule check in this edition.</summary>
    public bool IsChecked(string ruleId) => CheckedRuleIds.Contains(ruleId);

    /// <summary>Whether the parser reports (part of) <paramref name="ruleId"/>.</summary>
    public bool IsParserReported(string ruleId) => ParserRuleIds.Contains(ruleId);
}

/// <summary>
/// The rule checks of an assembly: every non-abstract <see cref="IRuleCheck"/> type with <see cref="KbRuleAttribute"/>s,
/// instantiated once (docs/design/core.md §7.2).
/// </summary>
internal sealed class RuleCatalog
{
    private readonly ImmutableArray<(KbRuleAttribute Binding, IRuleCheck Check)> _bindings;

    internal RuleCatalog(IEnumerable<(KbRuleAttribute Binding, IRuleCheck Check)> bindings) => _bindings = [.. bindings];

    /// <summary>Every bound rule ID, sorted.</summary>
    public IReadOnlyList<string> RuleIds => [.. _bindings.Select(b => b.Binding.RuleId).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>The checks of <paramref name="assembly"/>.</summary>
    /// <exception cref="InvalidOperationException">A <see cref="KbRuleAttribute"/> sits on a type that is not a concrete <see cref="IRuleCheck"/> with a parameterless constructor.</exception>
    public static RuleCatalog Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var bindings = new List<(KbRuleAttribute, IRuleCheck)>();
        foreach (var type in assembly.GetTypes())
        {
            var attributes = type.GetCustomAttributes<KbRuleAttribute>(inherit: false).ToList();
            if (attributes.Count == 0)
            {
                continue;
            }

            if (type.IsAbstract || !typeof(IRuleCheck).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"[KbRule] on {type.FullName}: rule checks must be concrete IRuleCheck classes with a parameterless constructor.");
            }

            var check = (IRuleCheck)Activator.CreateInstance(type)!;
            bindings.AddRange(attributes.Select(a => (a, check)));
        }

        return new RuleCatalog(bindings);
    }

    /// <summary>
    /// Startup check (§7.2): every bound rule ID and every rule the parser reports exists in at least one edition of
    /// <paramref name="kb"/>, and no edition has two equally specific checks for one rule.
    /// </summary>
    /// <exception cref="InvalidOperationException">An ID is unknown to the KB, or a binding is ambiguous.</exception>
    public void Verify(IKnowledgeBase kb)
    {
        ArgumentNullException.ThrowIfNull(kb);
        var editions = kb.Editions.Select(kb.ForEdition).OfType<IEditionKb>().ToList();
        bool Known(string id) => editions.Exists(e => e.Rule(id) is not null);

        var unknown = RuleIds.Where(id => !Known(id)).Select(id => $"[KbRule(\"{id}\")]")
            .Concat(Parser.RuleIds.Where(id => !Known(id)).Select(id => $"parser rule {id}"))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException($"Rule IDs not in any KB edition: {string.Join(", ", unknown)}.");
        }

        foreach (var edition in editions)
        {
            _ = ChecksFor(edition);
        }
    }

    /// <summary>The checks that run in <paramref name="kb"/>'s edition: one per rule ID the edition's KB has, the most specific variant.</summary>
    /// <exception cref="InvalidOperationException">Two variants of one rule are equally specific for the edition.</exception>
    public ImmutableArray<(KbRule Rule, IRuleCheck Check)> ChecksFor(IEditionKb kb)
    {
        ArgumentNullException.ThrowIfNull(kb);
        var result = ImmutableArray.CreateBuilder<(KbRule, IRuleCheck)>();
        foreach (var group in _bindings.Where(b => b.Binding.AppliesTo(kb.Edition)).GroupBy(b => b.Binding.RuleId, StringComparer.Ordinal))
        {
            if (kb.Rule(group.Key) is not { } rule)
            {
                continue;
            }

            var ranked = group.Select(b => (b.Check, Rank: Specificity(b.Binding, kb.Edition))).OrderByDescending(x => x.Rank).ToList();
            if (ranked.Count > 1 && ranked[0].Rank == ranked[1].Rank)
            {
                throw new InvalidOperationException(
                    $"Rule {group.Key}, ISCN {kb.Edition}: {ranked[0].Check.GetType().Name} and {ranked[1].Check.GetType().Name} are equally specific.");
            }

            result.Add((rule, ranked[0].Check));
        }

        return [.. result.OrderBy(x => x.Item1.Id, StringComparer.Ordinal)];
    }

    /// <summary>2 = the edition is named exactly, 1 = an open range covers it, 0 = every edition.</summary>
    private static int Specificity(KbRuleAttribute binding, string edition) =>
        string.IsNullOrWhiteSpace(binding.Editions) ? 0
        : binding.Editions.Split(',', StringSplitOptions.TrimEntries).Contains(edition, StringComparer.Ordinal) ? 2
        : 1;
}
