using System.Collections.Immutable;
using CytoLex.Core.Semantics;
using CytoLex.Core.Text;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Rules.Checks;

/// <summary>A finding of one of the structural analyses (T-031), addressed to the rule that explains it.</summary>
internal sealed record StructuralFinding(
    string Rule,
    TextSpan Span,
    string Detail,
    ImmutableArray<TextSpan> SecondarySpans = default,
    Certainty Certainty = Certainty.Certain);

/// <summary>
/// A check that reports the findings of one structural analysis addressed to its own rule (<see cref="RuleContext.Rule"/>).
/// An analysis decides which rule explains each fault, so every fault is reported once.
/// </summary>
internal abstract class StructuralCheck(Func<KaryotypeModel, string, IEnumerable<StructuralFinding>> analysis) : IRuleCheck
{
    public void Check(RuleContext context)
    {
        foreach (var f in analysis(context.Model, context.Input).Where(f => f.Rule == context.Rule.Id))
        {
            context.Report(f.Span, f.Detail, f.SecondarySpans, f.Certainty);
        }
    }
}

/// <summary>Model queries shared by the structural analyses.</summary>
internal static class Structure
{
    /// <summary>A part of a symbol-led abnormality, with the abnormality and the part's index in it.</summary>
    internal readonly record struct PartRef(Abnormality Item, AbnormalityPart Part, int Index)
    {
        /// <summary>The part is an aberration of a derivative or recombinant (<c>t(1;3)</c> in <c>der(1)t(1;3)(…)</c>).</summary>
        public bool InDerivative => Index > 0 && Item.Kind is "der" or "rec" or "ider";
    }

    /// <summary>Every part of every abnormality of the model (in <c>or</c> readings too), skipping recovered input.</summary>
    public static IEnumerable<PartRef> Parts(KaryotypeModel model) =>
        model.DescendantsAndSelf()
            .OfType<Abnormality>()
            .Where(a => !a.IsPartial)
            .SelectMany(a => a.Parts.Select((p, i) => new PartRef(a, p, i)));

    /// <summary>X = 0, Y = 1, autosome n = n + 1; <c>null</c> for <c>?</c> or an invalid identifier.</summary>
    public static int? Rank(ChromosomeRef c) =>
        c.Name switch
        {
            "X" => 0,
            "Y" => 1,
            _ when c.IsValidIdentifier() && c.Number is { } n => n + 1,
            _ => null,
        };

    /// <summary>The ranks of the part's chromosomes; <c>null</c> when any is unknown or invalid.</summary>
    public static int[]? Ranks(AbnormalityPart part)
    {
        var ranks = part.Chromosomes.Select(Rank).ToArray();
        return part.Chromosomes.IsEmpty || ranks.Any(r => r is null) ? null : [.. ranks.Select(r => r!.Value)];
    }

    /// <summary>Whether the part carries one of <paramref name="symbols"/> (<c>dic</c> in <c>dic r</c>).</summary>
    public static bool Has(this AbnormalityPart part, params string[] symbols) =>
        part.Symbols.Any(s => symbols.Contains(s.Symbol, StringComparer.Ordinal));

    /// <summary>The chromosome list as written, for messages: <c>(12;16)</c>.</summary>
    public static string ChromosomeList(this AbnormalityPart part) =>
        "(" + string.Join(";", part.Chromosomes.Select(c => c.Name)) + ")";

    /// <summary>The span from the part's first symbol to the end of its chromosome list (<c>t(16;12)</c>).</summary>
    public static TextSpan HeadSpan(this AbnormalityPart part) =>
        part.Chromosomes.IsEmpty || part.Symbols.IsEmpty
            ? part.Span
            : TextSpan.FromBounds(part.Symbols[0].Span.Start, Math.Min(part.Span.End, part.Chromosomes[^1].Span.End + 1));
}
