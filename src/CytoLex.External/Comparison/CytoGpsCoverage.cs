using System.Collections.Frozen;
using System.Collections.Immutable;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.External.CytoGps;

namespace CytoLex.External.Comparison;

/// <summary>
/// Decides which strings an external checker's grammar doesn't cover, so the comparison (T-051) keeps them out of the
/// agreement matrix instead of counting them as disagreements.
/// </summary>
public interface IExternalCoverage
{
    /// <summary>Why the checker can't read <paramref name="input"/>, or <c>null</c> when it covers it.</summary>
    string? Unsupported(string input);

    /// <summary>
    /// Whether the checker is known not to check what the CytoLex rule <paramref name="ruleId"/> checks; used only to
    /// suggest the <see cref="TriageLabel.CheckerLimitation"/> label for a string only CytoLex rejects (a person confirms it).
    /// </summary>
    bool DoesNotCheck(string ruleId);
}

/// <summary>
/// The notation CytoGPS's grammar has no rules for (docs/research/cytogps.md §4, read from its grammar and confirmed on
/// the KB examples, §5.2): every segment that is not a karyotype (ish, nuc ish, arr, …), the detailed system
/// (<c>::</c>, <c>→</c>/<c>-&gt;</c>), the symbols <c>fra</c>, <c>upd</c>, <c>rec</c>, <c>fis</c>, <c>tas</c>, <c>neo</c>,
/// the chimera separator <c>//</c> and per-abnormality cell counts. Read from our own parse of the string.
/// </summary>
public sealed class CytoGpsCoverage : IExternalCoverage
{
    private static readonly FrozenSet<string> _unsupportedSymbols =
        FrozenSet.Create(StringComparer.Ordinal, "fra", "upd", "rec", "fis", "tas", "neo");

    /// <summary>
    /// The CytoLex rules whose faults CytoGPS accepted on the KB invalid examples (docs/research/cytogps.md §5.2): exact
    /// IDs, or families ending in <c>.</c>. Order, counts, chromosome order, sex conventions, required forms, spaces and
    /// case, clone and cell-count rules, unidentified chromosomes and uncertainty.
    /// </summary>
    internal static readonly ImmutableArray<string> UncheckedRules =
    [
        "CLONE.", "ORDER.", "SEX.", "MAR.", "RING.", "UNCERTAIN.",
        "KARYO.ABNORMALITY_ORDER", "KARYO.CLONE_ORDER_CONSTITUTIONAL", "KARYO.CONSTITUTIONAL_C", "KARYO.DE_NOVO_SPACE",
        "NUM.COUNT_MATCHES_CHANGES", "NUM.PLOIDY_REFERENCE",
        "STRUCT.CHROMOSOME_ORDER", "STRUCT.MULTI_CHROMOSOME_ORDER",
        "DEL.FORMAT", "ISO.FORMAT", "TRANSLOC.JUMPING",
        "SYM.NO_OTHER_SPACE", "SYM.NO_SPACE_AT_PARENTHESIS", "SYM.SPACE_BEFORE_CHROMOSOME_COUNT",
        "SYM.SPACE_BETWEEN_SYMBOLS", "SYM.LETTER_CASE", "SYM.TYPED_SYMBOL_FORMS",
    ];

    private readonly Parser _parser;

    /// <summary>Creates the coverage test with the parser of CytoGPS's edition.</summary>
    public CytoGpsCoverage()
        : this(Parser.ForEdition(CytoGpsChecker.CytoGpsEdition))
    {
    }

    internal CytoGpsCoverage(Parser parser) => _parser = parser;

    /// <summary>The shared instance.</summary>
    public static CytoGpsCoverage Instance => _instance.Value;

    private static readonly Lazy<CytoGpsCoverage> _instance = new(() => new CytoGpsCoverage());

    public string? Unsupported(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        foreach (var segment in Segmenter.Split(input))
        {
            if (segment.Type != NotationType.Karyotype)
            {
                return $"{TypeName(segment.Type)} segment";
            }

            var tree = _parser.ParseKaryotype(input, segment);
            foreach (var token in tree.Root.Tokens())
            {
                if (token.IsMissing)
                {
                    continue;
                }

                switch (token.Kind)
                {
                    case TokenKind.DoubleColon:
                    case TokenKind.Arrow:
                        return "detailed system";
                    case TokenKind.DoubleSlash:
                        return "chimera separator //";
                    case TokenKind.Word when _unsupportedSymbols.Contains(token.ValueText):
                        return $"symbol {token.ValueText}";
                }
            }

            if (tree.Root.DescendantNodesAndSelf().Any(n => n.Kind == SyntaxKind.ItemCellCount))
            {
                return "per-abnormality cell count";
            }
        }

        return null;
    }

    public bool DoesNotCheck(string ruleId)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        return UncheckedRules.Any(r => r.EndsWith('.')
            ? ruleId.StartsWith(r, StringComparison.Ordinal)
            : string.Equals(ruleId, r, StringComparison.Ordinal));
    }

    private static string TypeName(NotationType type) => type switch
    {
        NotationType.Ish => "ish",
        NotationType.NucIsh => "nuc ish",
        NotationType.FibIsh => "fib ish",
        NotationType.RevIsh => "rev ish",
        NotationType.Cgh => "cgh",
        NotationType.Arr => "arr",
        NotationType.Rsa => "rsa",
        NotationType.Seq => "seq",
        _ => type.ToString().ToLowerInvariant(),
    };
}
