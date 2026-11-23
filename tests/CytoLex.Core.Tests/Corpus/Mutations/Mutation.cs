using System.Text;
using CytoLex.Core.Kb;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.KbExamples;
using CytoLex.Core.Text;

namespace CytoLex.Core.Tests.Corpus.Mutations;

/// <summary>
/// A book pair that grounds a mutation (T-062): the KB's invalid example of the mutation's rule (<see cref="Invalid"/>)
/// is what the mutation makes of <see cref="Source"/> (the example's <c>corrected</c> form, or a valid string when the
/// book gives no corrected form). When the book's example is a fragment (<c>9q35</c>), <see cref="BookExample"/> names
/// it and <see cref="Invalid"/> is the same fault in a whole karyotype. The mapping mutation → rule comes from the book,
/// not from memory.
/// </summary>
internal sealed record BookPair(string Source, string Invalid, string? BookExample = null);

/// <summary>A string made invalid by one mutation of a trusted valid string; the mutation's rule must fire.</summary>
internal sealed record Mutant(string SourceId, string MutationId, string RuleId, string Text, SampleContext? Context)
{
    public string Key => $"{SourceId}~{MutationId}";
}

/// <summary>What a mutation works on: the input and the model of its (first) karyotype segment.</summary>
internal sealed record MutationInput(string Text, KaryotypeModel Model, BandSet? Bands);

/// <summary>
/// One mutation operator: a small, deterministic edit at the first applicable site of a valid karyotype that breaks
/// exactly one KB rule (<see cref="RuleId"/>). Sites are found through the binder's model, never by guessing at text.
/// </summary>
internal abstract class Mutation
{
    /// <summary>Stable lower-kebab name, part of the mutant key.</summary>
    public abstract string Id { get; }

    /// <summary>The KB rule the mutation breaks.</summary>
    public abstract string RuleId { get; }

    /// <summary>The book pair the mapping to <see cref="RuleId"/> is taken from.</summary>
    public abstract BookPair Grounding { get; }

    /// <summary>The context the rule applies in, if limited (the mutant is generated only from entries labelled with it).</summary>
    public virtual SampleContext? RequiredContext => null;

    /// <summary>The mutated string, or <c>null</c> when the input has no applicable site.</summary>
    public abstract string? Apply(MutationInput input);

    /// <summary>Applies text edits (span → replacement) to <paramref name="text"/>; spans must not overlap.</summary>
    protected static string Edit(string text, params (TextSpan Span, string With)[] edits)
    {
        var sb = new StringBuilder(text);
        foreach (var (span, with) in edits.OrderByDescending(e => e.Span.Start))
        {
            sb.Remove(span.Start, span.Length).Insert(span.Start, with);
        }

        return sb.ToString();
    }

    /// <summary>The items of every cell line, in input order.</summary>
    protected static IEnumerable<(CellLine Line, int Index, KaryotypeItem Item)> Items(KaryotypeModel model) =>
        model.CellLines.SelectMany(l => l.Items.Select((item, i) => (l, i, item)));

    /// <summary>The lowest autosome an item is about (its first chromosome), or <c>null</c> for sex chromosomes and others.</summary>
    protected static int? FirstAutosome(KaryotypeItem item) => item switch
    {
        WholeChromosomeChange w => w.Chromosome.IsSexChromosome ? null : w.Chromosome.Number,
        Abnormality a when a.Parts.Length > 0 && a.Parts[0].Chromosomes.Length > 0 && !a.Parts[0].Chromosomes[0].IsSexChromosome =>
            a.Parts[0].Chromosomes[0].Number,
        _ => null,
    };

    /// <summary>An item with no per-item cell count, questioned mark or multiplier (plain enough to move or edit).</summary>
    protected static bool IsPlain(KaryotypeItem item) =>
        item is SuffixedItem { CellCount: null, Copies: null, IsQuestioned: false };
}
