using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Syntax;

namespace CytoLex.Core.Rules;

/// <summary>
/// How one edition is validated (docs/design/core.md §7.1): its KB, the parser built from its vocabulary, the
/// notation types implemented for it and its rule checks.
/// </summary>
/// <param name="Edition">The ISCN edition.</param>
/// <param name="Kb">The edition's KB.</param>
/// <param name="Parser">The karyotype parser configured with the edition's vocabulary.</param>
/// <param name="ImplementedTypes">Notation types validated in this edition; other segments are <c>NotImplemented</c>.</param>
/// <param name="Checks">The rule checks that run, one per KB rule ID.</param>
/// <param name="Binder">The binder that turns the edition's syntax trees into the semantic model (T-065).</param>
internal sealed record EditionProfile(
    string Edition,
    IEditionKb Kb,
    Parser Parser,
    ImmutableHashSet<NotationType> ImplementedTypes,
    ImmutableArray<(KbRule Rule, IRuleCheck Check)> Checks,
    Binder Binder);

/// <summary>The editions the core has a profile for, and what each implements (pilot: ISCN 2016 karyotypes, T-021; arr, T-034; the in situ types, T-033).</summary>
internal static class EditionProfiles
{
    /// <summary>Implemented notation types per edition. An edition missing here (or from the KB) is <c>NotImplemented</c>.</summary>
    public static readonly FrozenDictionary<string, ImmutableHashSet<NotationType>> ImplementedTypes =
        new Dictionary<string, ImmutableHashSet<NotationType>>(StringComparer.Ordinal)
        {
            ["2016"] = [NotationType.Karyotype, NotationType.Arr, NotationType.Ish, NotationType.NucIsh, NotationType.FibIsh, NotationType.RevIsh, NotationType.Cgh],
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
