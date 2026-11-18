using CytoLex.Core.Context;

namespace CytoLex.Core.Validation;

/// <summary>Which editions to validate against (docs/design/core.md §9).</summary>
public sealed record EditionSelection
{
    private EditionSelection(string? edition, bool all)
    {
        Edition = edition;
        IsAll = all;
    }

    /// <summary>The edition chosen with <see cref="One"/>; <c>null</c> for <see cref="All"/> and <see cref="Default"/>.</summary>
    public string? Edition { get; }

    /// <summary>Every known edition (implemented or not).</summary>
    public bool IsAll { get; }

    /// <summary>The newest implemented edition (2016 in the pilot).</summary>
    public static EditionSelection Default { get; } = new(null, false);

    /// <summary>Every known edition: the KB's and the ISCN editions the core knows of (<see cref="Validator.KnownEditions"/>).</summary>
    public static EditionSelection All { get; } = new(null, true);

    /// <summary>One edition, e.g. <c>"2016"</c>. An edition without KB data or profile gives a <c>NotImplemented</c> result.</summary>
    public static EditionSelection One(string edition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edition);
        return new EditionSelection(edition, false);
    }
}

/// <summary>Options of one <see cref="Validator.Validate"/> call (docs/design/core.md §9).</summary>
public sealed record ValidationOptions
{
    /// <summary>The default options: newest implemented edition, inferred context, whole notation string.</summary>
    public static ValidationOptions Default { get; } = new();

    /// <summary>Which editions to validate against.</summary>
    public EditionSelection Editions { get; init; } = EditionSelection.Default;

    /// <summary>An explicit sample context; overrides inference (§3.3). <c>null</c> = infer.</summary>
    public SampleContext? ContextHint { get; init; }

    /// <summary>
    /// The input is a book fragment (an abnormality such as <c>inv(2)</c>, a band, a symbol, or a string with <c>…</c>)
    /// rather than a whole notation string: karyotype segments use the fragment entry of the parser (count and sex
    /// chromosomes optional). Used by the KB-example harness and the rule browser.
    /// </summary>
    public bool IsFragment { get; init; }
}
