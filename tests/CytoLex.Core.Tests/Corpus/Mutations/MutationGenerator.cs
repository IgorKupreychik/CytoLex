using CytoLex.Core.Tests.Semantics;

namespace CytoLex.Core.Tests.Corpus.Mutations;

/// <summary>
/// Applies every <see cref="MutationCatalog"/> operator to the trusted valid corpus entries (T-062): entries whose valid
/// label is enforced in the edition (<see cref="CorpusData.IsEnforced"/>), that have a karyotype segment and no segment
/// the pilot doesn't implement, and that aren't pending. One mutant per operator and entry (first applicable site),
/// deterministic, in corpus order. Only ISCN 2016 has a KB and bands yet.
/// </summary>
internal static class MutationGenerator
{
    public const string Edition = "2016";

    private static readonly HashSet<string> _implemented = new(["karyotype", "arr"], StringComparer.Ordinal);

    private static readonly Lazy<IReadOnlyList<Mutant>> _mutants = new(() => Generate(CorpusData.Entries));

    public static IReadOnlyList<Mutant> Mutants => _mutants.Value;

    public static Mutant Find(string key) => Mutants.Single(m => m.Key == key);

    public static IReadOnlyList<Mutant> Generate(IEnumerable<CorpusEntry> entries)
    {
        var kbRules = CorpusData.KbRules(Edition);
        var mutants = new List<Mutant>();
        foreach (var entry in entries)
        {
            if (!entry.Expected.TryGetValue(Edition, out var expected) || expected.Verdict != CorpusVerdict.Valid
                || !CorpusData.IsEnforced(entry, Edition, kbRules, out _) || PendingCorpus.Table.ContainsKey(entry.Id)
                || !entry.Types.Contains("karyotype") || !entry.Types.All(_implemented.Contains))
            {
                continue;
            }

            foreach (var mutation in MutationCatalog.All)
            {
                if (mutation.RequiredContext is { } required && expected.Context != required)
                {
                    continue;
                }

                if (Apply(mutation, entry.Iscn) is { } text)
                {
                    mutants.Add(new Mutant(entry.Id, mutation.Id, mutation.RuleId, text, expected.Context));
                }
            }
        }

        return mutants;
    }

    /// <summary>Applies one operator to the first karyotype segment of <paramref name="text"/>; <c>null</c> when it doesn't apply.</summary>
    public static string? Apply(Mutation mutation, string text)
    {
        var models = BindHelper.Bind(text);
        var model = models.Count > 0 ? models[0] : null;
        if (model is null || model.DescendantsAndSelf().Any(n => n.IsPartial))
        {
            return null;
        }

        var mutated = mutation.Apply(new MutationInput(text, model, BindHelper.Kb2016.Bands));
        return mutated == text ? null : mutated;
    }
}
