using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules;

/// <summary>
/// A rule check bound to a KB rule by <see cref="KbRuleAttribute"/> (docs/design/core.md §7.2). The engine creates one
/// instance per type (parameterless constructor) and calls it concurrently, so checks must be stateless.
/// A check runs for an edition only when that edition's KB has its rule ID, and only on segments whose notation
/// type the rule's <c>applies_to</c> covers.
/// </summary>
public interface IRuleCheck
{
    /// <summary>Checks one segment and reports findings through <see cref="RuleContext.Report"/>.</summary>
    void Check(RuleContext context);
}

/// <summary>The facts a check reported; the engine turns them into a <see cref="Diagnostic"/> (§8.2).</summary>
/// <param name="Span">The primary span in the whole input.</param>
/// <param name="Detail">What was found, beyond the rule title.</param>
/// <param name="SecondarySpans">Further spans involved.</param>
/// <param name="Certainty">Whether the check is certain.</param>
/// <param name="Contexts">The contexts the finding applies to; empty = the rule's KB contexts (or every context).</param>
/// <param name="CannotVerify">The string alone cannot show whether the rule is met (→ info).</param>
public sealed record RuleFinding(
    TextSpan Span,
    string? Detail,
    ImmutableArray<TextSpan> SecondarySpans,
    Certainty Certainty,
    ImmutableArray<SampleContext> Contexts,
    bool CannotVerify);

/// <summary>What a check sees of one segment in one edition, and where it reports (docs/design/core.md §7.2).</summary>
public sealed class RuleContext
{
    private readonly List<RuleFinding> _findings = [];

    internal RuleContext(string input, Segment segment, SyntaxTree syntax, ContextInfo context, KbRule rule, IEditionKb kb)
    {
        Input = input;
        Segment = segment;
        Syntax = syntax;
        Context = context;
        Rule = rule;
        Kb = kb;
    }

    /// <summary>The whole input string; every span refers to it.</summary>
    public string Input { get; }

    /// <summary>The segment being checked.</summary>
    public Segment Segment { get; }

    /// <summary>The segment's lossless syntax tree (the semantic model follows with T-065).</summary>
    public SyntaxTree Syntax { get; }

    /// <summary>The sample context of the whole string (hint, inferred, effective).</summary>
    public ContextInfo Context { get; }

    /// <summary>The KB rule the check is bound to, in this edition.</summary>
    public KbRule Rule { get; }

    /// <summary>The edition's KB (vocabularies, bands, other rules).</summary>
    public IEditionKb Kb { get; }

    /// <summary>The ISCN edition, e.g. <c>2016</c>.</summary>
    public string Edition => Kb.Edition;

    /// <summary>The findings reported so far.</summary>
    public IReadOnlyList<RuleFinding> Findings => _findings;

    /// <summary>Reports a finding: only the facts. Severity, citation and context handling come from <see cref="Rule"/> (§8.2).</summary>
    /// <param name="span">The primary span in the whole input.</param>
    /// <param name="detail">What was found, beyond the rule title.</param>
    /// <param name="secondarySpans">Further spans involved.</param>
    /// <param name="certainty">Whether the check is certain; uncertain findings are capped at warning.</param>
    /// <param name="contexts">The contexts the finding applies to, when the check itself knows (e.g. <c>47,XXX</c>); default = the rule's KB contexts.</param>
    /// <param name="cannotVerify">The string alone cannot show whether the rule is met: reported as info.</param>
    public void Report(
        TextSpan span,
        string? detail = null,
        ImmutableArray<TextSpan> secondarySpans = default,
        Certainty certainty = Certainty.Certain,
        ImmutableArray<SampleContext> contexts = default,
        bool cannotVerify = false) =>
        _findings.Add(new RuleFinding(
            span,
            detail,
            secondarySpans.IsDefault ? [] : secondarySpans,
            certainty,
            contexts.IsDefault ? [] : contexts,
            cannotVerify));
}
