namespace CytoLex.Core.Rules;

/// <summary>
/// Binds a rule check to its KB rule ID (docs/design/core.md §7.2). A check runs for an edition only
/// when that edition's KB contains the ID; <see cref="Editions"/> narrows it to edition-specific variants.
/// The KB-example harness and the coverage report (T-025) read these attributes to see which rules
/// have an implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class KbRuleAttribute(string ruleId) : Attribute
{
    /// <summary>The KB rule ID, e.g. <c>TRANSLOC.CHROMOSOME_ORDER</c>.</summary>
    public string RuleId { get; } = ruleId;

    /// <summary>
    /// Editions this implementation serves: <c>null</c> = every edition whose KB has the rule;
    /// otherwise a comma-separated list (<c>"2016"</c>, <c>"2016,2020"</c>) or an open range (<c>"2020+"</c>).
    /// </summary>
    public string? Editions { get; init; }

    /// <summary>Whether this implementation serves <paramref name="edition"/> (e.g. <c>"2016"</c>).</summary>
    public bool AppliesTo(string edition)
    {
        if (string.IsNullOrWhiteSpace(Editions))
        {
            return true;
        }

        foreach (var part in Editions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.EndsWith('+')
                ? string.CompareOrdinal(edition, part[..^1]) >= 0
                : string.Equals(edition, part, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
