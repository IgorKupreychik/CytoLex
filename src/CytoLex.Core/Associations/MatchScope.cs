using System.Collections.Immutable;
using CytoLex.Core.Context;
using CytoLex.Core.Kb;
using CytoLex.Core.Validation;

namespace CytoLex.Core.Associations;

/// <summary>An abnormality the leaves look at: a top-level entry, or one reading of an <c>or</c> entry (with its parent's index).</summary>
/// <param name="Entry">The entry or reading.</param>
/// <param name="Index">The index of the top-level entry in <see cref="EditionResult.Abnormalities"/>.</param>
internal sealed record Candidate(AbnormalityEntry Entry, int Index);

/// <summary>
/// What one evaluation of a rule sees (T-109): the string's abnormalities and loci, the band nesting, the sample
/// context being read, and the rule's own context. An entry marked constitutional (<c>c</c>) is constitutional in any
/// string; every other entry has the context being read.
/// </summary>
internal sealed class MatchScope(
    ImmutableArray<AbnormalityEntry> entries,
    ImmutableArray<LocusEntry> loci,
    BandNesting nesting,
    SampleContext reading,
    AssociationContext rule)
{
    /// <summary>Every abnormality, readings of <c>or</c> entries included, whose context fits the rule's.</summary>
    public IEnumerable<Candidate> Candidates =>
        entries.SelectMany(e => e.Kind == AbnormalityKind.Alternatives
                ? e.Readings.Select(r => new Candidate(r, e.Index))
                : [new Candidate(e, e.Index)])
            .Where(c => !c.Entry.Flags.IsPartial && Fits(c.Entry));

    /// <summary>Every locus whose entry's context fits the rule's.</summary>
    public IEnumerable<LocusEntry> Loci => loci.Where(l => Entry(l.Abnormality) is { } e && Fits(e));

    /// <summary>The band nesting of the edition.</summary>
    public BandNesting Nesting => nesting;

    /// <summary>The loci of the entry with <paramref name="index"/>.</summary>
    public IEnumerable<LocusEntry> LociOf(int index) => loci.Where(l => l.Abnormality == index);

    /// <summary>The top-level entry with <paramref name="index"/>; <c>null</c> when out of range.</summary>
    public AbnormalityEntry? Entry(int index) => index >= 0 && index < entries.Length ? entries[index] : null;

    private bool Fits(AbnormalityEntry entry)
    {
        var context = entry.Flags.Constitutional ? SampleContext.Constitutional : reading;
        return rule switch
        {
            AssociationContext.Constitutional => context == SampleContext.Constitutional,
            AssociationContext.Neoplastic => context == SampleContext.Acquired,
            _ => true,
        };
    }
}
