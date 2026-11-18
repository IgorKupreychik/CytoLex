using System.Reflection;
using CytoLex.Core.Rules;

namespace CytoLex.Core.Tests.KbExamples;

/// <summary>
/// Which KB rules have an implementation: the <see cref="KbRuleAttribute"/>s on the types of an assembly
/// (by default <c>CytoLex.Core</c>). T-028 may replace this with the rule engine's coverage list (core.md §7.2).
/// </summary>
internal sealed class RuleImplementationScan
{
    private static readonly Lazy<RuleImplementationScan> _default = new(() => new RuleImplementationScan(typeof(KbRuleAttribute).Assembly));
    private readonly List<(KbRuleAttribute Attribute, Type Type)> _bindings;

    public RuleImplementationScan(Assembly assembly)
    {
        _bindings =
        [
            .. assembly.GetTypes()
                .SelectMany(t => t.GetCustomAttributes<KbRuleAttribute>(inherit: false).Select(a => (a, t))),
        ];
    }

    /// <summary>The scan of <c>CytoLex.Core</c>.</summary>
    public static RuleImplementationScan Default => _default.Value;

    /// <summary>Every rule ID that has a <see cref="KbRuleAttribute"/>, sorted.</summary>
    public IReadOnlyList<string> AllRuleIds =>
        [.. _bindings.Select(b => b.Attribute.RuleId).Distinct().Order(StringComparer.Ordinal)];

    public bool IsImplemented(string edition, string ruleId) =>
        _bindings.Any(b => b.Attribute.RuleId == ruleId && b.Attribute.AppliesTo(edition));

    /// <summary>The implementing types of a rule for an edition (for the report).</summary>
    public IReadOnlyList<string> Implementations(string edition, string ruleId) =>
        [.. _bindings.Where(b => b.Attribute.RuleId == ruleId && b.Attribute.AppliesTo(edition)).Select(b => b.Type.Name).Order(StringComparer.Ordinal)];
}
