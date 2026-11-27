using System.Collections.Immutable;
using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>Where an association rule holds (T-108): constitutional, neoplastic, or both.</summary>
public enum AssociationContext
{
    /// <summary>A constitutional abnormality (present from birth).</summary>
    Constitutional,

    /// <summary>An acquired abnormality of a neoplasm.</summary>
    Neoplastic,

    /// <summary>Either.</summary>
    Any,
}

/// <summary>How closely a condition is tied to the abnormality (T-108).</summary>
public enum AssociationStrength
{
    /// <summary>The hallmark abnormality of the condition.</summary>
    Characteristic,

    /// <summary>Found again and again, but not the hallmark.</summary>
    Recurrent,

    /// <summary>Described, less common.</summary>
    Reported,
}

/// <summary>A code of a condition in a coding system (<c>orphanet</c> <c>ORPHA:870</c>).</summary>
/// <param name="System"><c>orphanet</c>, <c>icd10</c>, <c>icdo3</c>, <c>mondo</c> or <c>other</c>.</param>
/// <param name="Code">The code as written.</param>
public sealed record ConditionId(string System, string Code);

/// <summary>The condition an association rule names.</summary>
/// <param name="Name">The name as its source gives it.</param>
/// <param name="Ids">Its codes; empty when none is filled in.</param>
public sealed record AssociationCondition(string Name, ImmutableArray<ConditionId> Ids);

/// <summary>A classification in which the abnormality is a defining criterion of the condition (kept for T-112, not shown as a diagnosis).</summary>
/// <param name="Source">The classification (<c>WHO-HAEM5</c>, <c>ICC-2022</c>).</param>
/// <param name="Citation">Its entry, in own words.</param>
public sealed record DefiningCriterion(string Source, string Citation);

/// <summary>A node of an association rule's <c>match</c> tree (T-108).</summary>
public abstract record MatchNode
{
    /// <summary>The node as a short text for evidence and messages (<c>exact t(9;22)(q34;q11.2)</c>).</summary>
    public abstract string Text { get; }
}

/// <summary>One abnormality as written in its short form; band-level tolerant (T-109).</summary>
/// <param name="Form">The form (<c>t(9;22)(q34;q11.2)</c>, <c>+21</c>).</param>
public sealed record ExactLeaf(string Form) : MatchNode
{
    /// <inheritdoc />
    public override string Text => "exact " + Form;
}

/// <summary>Two bands joined by any rearrangement, including an in situ <c>con</c> signal.</summary>
/// <param name="BandA">The first band (<c>9q34</c>).</param>
/// <param name="BandB">The second band (<c>22q11.2</c>).</param>
public sealed record FusionLeaf(string BandA, string BandB) : MatchNode
{
    /// <inheritdoc />
    public override string Text => $"fusion {BandA}+{BandB}";
}

/// <summary>Gain or loss of copies.</summary>
public enum CopyEffect
{
    /// <summary>More copies.</summary>
    Gain,

    /// <summary>Fewer copies.</summary>
    Loss,
}

/// <summary>A gain or loss that overlaps a GRCh38 region.</summary>
/// <param name="Effect">Gain or loss.</param>
/// <param name="Chromosome">1–22, X or Y.</param>
/// <param name="Grch38">The region (GRCh38 positions).</param>
/// <param name="Band">The band named with the region, if any (checked against the positions by <c>kb build</c>).</param>
public sealed record CopyLeaf(CopyEffect Effect, string Chromosome, GenomicRange Grch38, string? Band) : MatchNode
{
    /// <inheritdoc />
    public override string Text => $"{(Effect == CopyEffect.Gain ? "gain" : "loss")} of chr{Chromosome}:{Grch38.Start}-{Grch38.End}" + (Band is null ? string.Empty : $" ({Band})");
}

/// <summary>Every part must match.</summary>
/// <param name="Parts">The parts.</param>
public sealed record AllNode(ImmutableArray<MatchNode> Parts) : MatchNode
{
    /// <inheritdoc />
    public override string Text => "all of (" + string.Join("; ", Parts.Select(p => p.Text)) + ")";
}

/// <summary>At least one part must match.</summary>
/// <param name="Parts">The parts.</param>
public sealed record AnyNode(ImmutableArray<MatchNode> Parts) : MatchNode
{
    /// <inheritdoc />
    public override string Text => "any of (" + string.Join("; ", Parts.Select(p => p.Text)) + ")";
}

/// <summary>The part must not match anywhere in the string.</summary>
/// <param name="Part">The part.</param>
public sealed record NotNode(MatchNode Part) : MatchNode
{
    /// <inheritdoc />
    public override string Text => "not " + Part.Text;
}

/// <summary>
/// One association rule (<c>curated/associations/associations.yaml</c>, T-108): a condition associated in the
/// literature with an abnormality, with its source. Not a diagnosis (T-104).
/// </summary>
/// <param name="Id">The stable ID (<c>ASSOC.T9_22_CML</c>).</param>
/// <param name="Condition">The condition.</param>
/// <param name="Context">Where the association holds.</param>
/// <param name="Strength">How closely the condition is tied to the abnormality.</param>
/// <param name="Defining">Classifications where the abnormality is a defining criterion; empty when none.</param>
/// <param name="Source">The source (<c>WHO-HAEM5</c>, <c>ICC-2022</c>, <c>ClinGen</c>, <c>Orphanet</c>, <c>other</c>).</param>
/// <param name="Citation">The entry or dataset release, in own words.</param>
/// <param name="Reviewed">Whether a person has checked the rule against its source.</param>
/// <param name="Match">What the rule matches.</param>
public sealed record AssociationRule(
    string Id,
    AssociationCondition Condition,
    AssociationContext Context,
    AssociationStrength Strength,
    ImmutableArray<DefiningCriterion> Defining,
    string Source,
    string Citation,
    bool Reviewed,
    MatchNode Match);

/// <summary>The association rules of the runtime bundle (top-level <c>associations</c>, T-108); edition-independent.</summary>
public sealed class AssociationRules
{
    private AssociationRules(ImmutableArray<AssociationRule> rules) => Rules = rules;

    /// <summary>No rules.</summary>
    public static AssociationRules Empty { get; } = new([]);

    /// <summary>Every rule, in file order.</summary>
    public ImmutableArray<AssociationRule> Rules { get; }

    /// <summary>Every <see cref="ExactLeaf.Form"/> of the rules, distinct, in rule order.</summary>
    public IEnumerable<string> ExactForms => Rules.SelectMany(r => Leaves(r.Match)).OfType<ExactLeaf>().Select(l => l.Form).Distinct(StringComparer.Ordinal);

    /// <summary>Reads the top-level <c>associations</c> array of a bundle (or a test fixture).</summary>
    /// <exception cref="InvalidDataException">A rule's <c>match</c> or enum value is not in the T-108 format.</exception>
    public static AssociationRules FromBundle(JsonElement associations)
    {
        if (associations.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        return new AssociationRules([.. associations.EnumerateArray().Select(ReadRule)]);
    }

    /// <summary>The leaves of <paramref name="node"/>, depth first.</summary>
    public static IEnumerable<MatchNode> Leaves(MatchNode node) => node switch
    {
        AllNode all => all.Parts.SelectMany(Leaves),
        AnyNode any => any.Parts.SelectMany(Leaves),
        NotNode not => Leaves(not.Part),
        _ => [node],
    };

    private static AssociationRule ReadRule(JsonElement r)
    {
        var condition = r.GetProperty("condition");
        return new AssociationRule(
            r.GetProperty("id").GetString()!,
            new AssociationCondition(
                condition.GetProperty("name").GetString()!,
                condition.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Array
                    ? [.. ids.EnumerateArray().Select(i => new ConditionId(i.GetProperty("system").GetString()!, i.GetProperty("code").GetString()!))]
                    : []),
            r.GetProperty("context").GetString() switch
            {
                "constitutional" => AssociationContext.Constitutional,
                "neoplastic" => AssociationContext.Neoplastic,
                "any" => AssociationContext.Any,
                var c => throw new InvalidDataException($"Unknown association context '{c}'."),
            },
            r.GetProperty("strength").GetString() switch
            {
                "characteristic" => AssociationStrength.Characteristic,
                "recurrent" => AssociationStrength.Recurrent,
                "reported" => AssociationStrength.Reported,
                var s => throw new InvalidDataException($"Unknown association strength '{s}'."),
            },
            r.TryGetProperty("defining", out var d) && d.ValueKind == JsonValueKind.Array
                ? [.. d.EnumerateArray().Select(x => new DefiningCriterion(x.GetProperty("source").GetString()!, x.GetProperty("citation").GetString()!))]
                : [],
            r.GetProperty("source").GetString()!,
            r.GetProperty("citation").GetString()!,
            r.TryGetProperty("review", out var review) && review.TryGetProperty("reviewed", out var reviewed) && reviewed.GetBoolean(),
            ReadNode(r.GetProperty("match")));
    }

    private static MatchNode ReadNode(JsonElement node)
    {
        if (node.TryGetProperty("exact", out var exact))
        {
            return new ExactLeaf(exact.GetString()!);
        }

        if (node.TryGetProperty("fusion", out var fusion))
        {
            return new FusionLeaf(fusion[0].GetString()!, fusion[1].GetString()!);
        }

        if (node.TryGetProperty("copy", out var copy))
        {
            var range = copy.GetProperty("grch38");
            return new CopyLeaf(
                copy.GetProperty("effect").GetString() == "gain" ? CopyEffect.Gain : CopyEffect.Loss,
                copy.GetProperty("chrom").GetString()!,
                new GenomicRange(range[0].GetInt64(), range[1].GetInt64()),
                copy.TryGetProperty("band", out var band) && band.ValueKind == JsonValueKind.String ? band.GetString() : null);
        }

        if (node.TryGetProperty("all", out var all))
        {
            return new AllNode([.. all.EnumerateArray().Select(ReadNode)]);
        }

        if (node.TryGetProperty("any", out var any))
        {
            return new AnyNode([.. any.EnumerateArray().Select(ReadNode)]);
        }

        if (node.TryGetProperty("not", out var not))
        {
            return new NotNode(ReadNode(not));
        }

        throw new InvalidDataException("An association match node needs one of exact, fusion, copy, all, any, not: " + node.GetRawText());
    }
}
