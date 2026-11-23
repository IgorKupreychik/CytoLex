using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Composing;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;

namespace CytoLex.Core.Semantics;

/// <summary>
/// Builds the semantic model of a karyotype segment from its syntax tree (docs/design/core.md §5, T-065).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The binder interprets; it does not judge. It reports no diagnostics: a band missing from the band set, a
/// breakpoint group without a chromosome and the like are facts in the model, reported by the rules (T-029+).</item>
/// <item>Short and detailed systems give the same <see cref="Breakpoint"/>s: in the detailed system they are the band
/// ends at the joins (<c>::</c>, <c>:</c>). The band composition (<see cref="DerivativeStructure"/>) is kept as written;
/// <see cref="Abnormality.Composition"/> gives it for both systems (computed from the short system by the
/// <see cref="Composer"/>, T-066).</item>
/// <item>Every node built from recovered or skipped input carries those <see cref="SyntaxDiagnostic"/>s as
/// <see cref="MeaningNode.Issues"/> (partial); its ancestors too.</item>
/// <item>KB refs: each symbol's vocab item and its format rule(s) (<see cref="SymbolRules"/>), each band of the band
/// set, and the rule that defines a construct (mosaic prefix, detailed system, <c>or</c>, <c>~</c>, <c>c</c>, …). Only
/// rule IDs the edition's KB has are attached.</item>
/// </list>
/// </remarks>
public sealed partial class Binder
{
    /// <summary>
    /// The rule(s) that define each abnormality symbol's format. Each rule's section is one of the symbol's vocab sections
    /// (checked by a test), so the mapping traces to the KB.
    /// </summary>
    internal static readonly FrozenDictionary<string, ImmutableArray<string>> SymbolRules =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            ["ace"] = ["ACE.NOT_IN_KARYOTYPE"],
            ["add"] = ["ADD.FORMAT"],
            ["del"] = ["DEL.FORMAT"],
            ["der"] = ["DER.DEFINITION"],
            ["dic"] = ["DIC.FORMAT"],
            ["dmin"] = ["DMIN.FORMAT"],
            ["dup"] = ["DUP.FORMAT"],
            ["fis"] = ["FIS.FORMAT"],
            ["fra"] = ["FRA.FORMAT"],
            ["hsr"] = ["HSR.FORMAT"],
            ["i"] = ["ISO.FORMAT"],
            ["idic"] = ["DIC.FORMAT", "ISO.FORMAT"],
            ["ider"] = ["DER.ISODERIVATIVE"],
            ["inc"] = ["UNCERTAIN.INC_AT_END"],
            ["ins"] = ["INS.ORIENTATION"],
            ["inv"] = ["INV.FORMAT"],
            ["mar"] = ["MAR.PLUS_SIGN"],
            ["neo"] = ["NEO.FORMAT"],
            ["psu"] = ["DIC.PSEUDODICENTRIC_ORDER"],
            ["qdp"] = ["QDP.FORMAT"],
            ["r"] = ["RING.FORMAT"],
            ["rec"] = ["REC.FORMAT"],
            ["rob"] = ["ROB.FORMAT"],
            ["t"] = ["TRANSLOC.FORMAT"],
            ["tas"] = ["TAS.FORMAT"],
            ["trc"] = ["TRC.FORMAT"],
            ["trp"] = ["TRP.FORMAT"],
            ["upd"] = ["UPD.FORMAT"],
        }.ToFrozenDictionary(StringComparer.Ordinal);

    // Rules that define constructs (not symbols); attached when the edition's KB has them.
    internal const string MosaicChimeraPrefix = "KARYO.MOSAIC_CHIMERA_PREFIX";
    internal const string Endoreduplication = "KARYO.ENDOREDUPLICATION";
    internal const string TransplantChimera = "KARYO.TRANSPLANT_CHIMERA";
    internal const string CountSexOrder = "KARYO.COUNT_SEX_ORDER";
    internal const string PloidyInAngleBrackets = "NUM.PLOIDY_IN_ANGLE_BRACKETS";
    internal const string ConstitutionalC = "KARYO.CONSTITUTIONAL_C";
    internal const string ParentalOrigin = "KARYO.PARENTAL_ORIGIN";
    internal const string MultipleCopies = "KARYO.MULTIPLY_SIGN_REARRANGED_ONLY";
    internal const string GainLossSign = "KARYO.GAIN_LOSS_SIGN";
    internal const string QuestionableItem = "UNCERTAIN.QUESTIONABLE_ITEM";
    internal const string QuestionMarkDesignation = "UNCERTAIN.QUESTION_MARK_DESIGNATION";
    internal const string ApproxInterval = "UNCERTAIN.APPROX_INTERVAL";
    internal const string OrAlternative = "UNCERTAIN.OR_ALTERNATIVE";
    internal const string DetailedSystem = "STRUCT.DETAILED_SYSTEM";
    internal const string BreakpointGroups = "STRUCT.BREAKPOINT_GROUPS";
    internal const string VarDefinition = "VAR.DEFINITION";
    internal const string VarLengthSign = "VAR.LENGTH_SIGN";

    /// <summary>Every construct rule ID the binder can attach (a test checks they exist in the KB).</summary>
    internal static readonly ImmutableArray<string> ConstructRuleIds =
    [
        MosaicChimeraPrefix, Endoreduplication, TransplantChimera, CountSexOrder, PloidyInAngleBrackets, ConstitutionalC,
        ParentalOrigin, MultipleCopies, GainLossSign, QuestionableItem, QuestionMarkDesignation, ApproxInterval, OrAlternative,
        DetailedSystem, BreakpointGroups, VarDefinition, VarLengthSign,
        GenomeBuildRequired, ArrRegionFormat, ArrNucleotideFormat, ArrCopyNumberRule, ArrComplex, ArrChromothripsis,
        ArrMosaicProportion, ArrInheritanceSpacing, ArrZygosity, ArrAberrationsOnly,
    ];

    private readonly IEditionKb _kb;

    public Binder(IEditionKb kb)
    {
        ArgumentNullException.ThrowIfNull(kb);
        _kb = kb;
    }

    /// <summary>The meaning of <paramref name="tree"/>.</summary>
    public KaryotypeModel Bind(SyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return new Run(this, tree).BindRoot();
    }

    /// <summary>State of one bind.</summary>
    private sealed partial class Run(Binder binder, SyntaxTree tree)
    {
        private readonly IEditionKb _kb = binder._kb;
        private readonly Composer _composer = new(id => binder._kb.Rule(id) is not null);

        // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>The syntax diagnostics inside <paramref name="elements"/>: their missing tokens, or text overlapping them.</summary>
        private ImmutableArray<SyntaxDiagnostic> IssuesOf(params SyntaxElement?[] elements)
        {
            if (tree.Diagnostics.IsEmpty)
            {
                return [];
            }

            var present = elements.OfType<SyntaxElement>().ToList();
            var tokens = present.SelectMany(e => e.Tokens()).ToList();
            var missing = tokens.Where(t => t.IsMissing).Select(t => t.Span).ToHashSet();
            var spans = present.Select(e => e.Span).Where(s => s.Length > 0).ToList();
            return
            [
                .. tree.Diagnostics.Where(d => d.Kind == SyntaxDiagnosticKind.Missing && d.Span.Length == 0
                    ? missing.Contains(d.Span)
                    : spans.Any(s => Overlaps(s, d.Span))),
            ];
        }

        private static bool Overlaps(TextSpan a, TextSpan b) =>
            b.Length == 0 ? a.Start <= b.Start && b.Start <= a.End : a.Start < b.End && b.Start < a.End;

        private static TextSpan SpanOf(params SyntaxElement?[] elements)
        {
            var present = elements.OfType<SyntaxElement>().Select(e => e.Span).ToList();
            return present.Count == 0 ? default : TextSpan.FromBounds(present.Min(s => s.Start), present.Max(s => s.End));
        }

        /// <summary>Refs to the rules with <paramref name="ids"/> that the edition's KB has.</summary>
        private IEnumerable<KbRef> Rules(params IEnumerable<string> ids) =>
            ids.Where(id => _kb.Rule(id) is not null).Select(KbRef.Rule);

        private static IEnumerable<KbRef> VocabOf(params IEnumerable<SyntaxToken?> tokens) =>
            tokens.OfType<SyntaxToken>().Where(t => t.VocabRef is not null).Select(t => KbRef.Parse(t.VocabRef!));

        private static ImmutableArray<KbRef> Refs(params IEnumerable<KbRef>[] parts) => [.. parts.SelectMany(p => p).Distinct()];

        private static int? Int(SyntaxToken? t) =>
            t is { IsMissing: false, Kind: TokenKind.Number } && int.TryParse(t.ValueText, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

        private static bool IsWord(SyntaxElement e, string text) => e is SyntaxToken { Kind: TokenKind.Word } t && t.Text == text;

        // ── Root and cell lines ──────────────────────────────────────────────────────────────────────

        public KaryotypeModel BindRoot()
        {
            var root = tree.Root;
            var prefixes = ImmutableArray.CreateBuilder<Prefix>();
            var lines = ImmutableArray.CreateBuilder<CellLine>();
            var unbound = ImmutableArray.CreateBuilder<UnboundItem>();
            SyntaxNode? separator = null;

            foreach (var child in root.Children.OfType<SyntaxNode>())
            {
                switch (child.Kind)
                {
                    case SyntaxKind.Prefix or SyntaxKind.MisplacedPrefix:
                        prefixes.Add(BindPrefix(child));
                        break;
                    case SyntaxKind.CellLineSeparator:
                        separator = child;
                        break;
                    case SyntaxKind.CellLine:
                        lines.Add(BindCellLine(child, separator));
                        separator = null;
                        break;
                    default:
                        unbound.Add(Unbound(child));
                        break;
                }
            }

            var relation = root.ChildNodes(SyntaxKind.CellLineSeparator).Any(s => s.Children[0] is SyntaxToken { Kind: TokenKind.DoubleSlash })
                ? CellLineRelation.Transplant
                : prefixes.Where(p => !p.IsMisplaced).Select(p => p.Symbol).FirstOrDefault(s => s is "mos" or "chi") switch
                {
                    "mos" => CellLineRelation.Mosaic,
                    "chi" => CellLineRelation.Chimera,
                    _ => CellLineRelation.None,
                };

            return new KaryotypeModel
            {
                Span = root.Span,
                Syntax = root,
                Tree = tree,
                Issues = tree.Diagnostics,
                IsFragment = root.Kind == SyntaxKind.Fragment,
                Prefixes = prefixes.ToImmutable(),
                CellLines = ResolveCloneReferences(lines.ToImmutable()),
                Unbound = unbound.ToImmutable(),
                Relation = relation,
                IsEndoreduplication = prefixes.Any(p => p.Symbol == "end"),
                Kb = relation == CellLineRelation.Transplant ? Refs(Rules(TransplantChimera)) : [],
            };
        }

        /// <summary>Sets <see cref="CellLine.BasedOn"/> for lines written with <c>idem</c>, <c>sl</c> or <c>sdl</c> (<see cref="CloneReferences"/>).</summary>
        private static ImmutableArray<CellLine> ResolveCloneReferences(ImmutableArray<CellLine> lines)
        {
            var refs = lines.Select(CloneReferences.Of).ToList();
            if (refs.All(r => r is null))
            {
                return lines;
            }

            var targets = CloneReferences.Resolve(refs);
            return [.. lines.Select((line, i) => CloneReferences.ReferenceItem(line) is { } item
                ? line with
                {
                    BasedOn = new CloneReference(item.Kind!, item.Parts[0].SymbolNumber, item.Copies is null ? 1 : item.Copies.Count, item, targets[i]),
                }
                : line)];
        }

        private Prefix BindPrefix(SyntaxNode node)
        {
            var word = (SyntaxToken)node.Children[0];
            return new Prefix
            {
                Span = node.Span,
                Syntax = node,
                Symbol = word.Text,
                IsMisplaced = node.Kind == SyntaxKind.MisplacedPrefix,
                Issues = IssuesOf(node),
                Kb = Refs(VocabOf(word), Rules(word.Text == "end" ? Endoreduplication : MosaicChimeraPrefix)),
            };
        }

        private UnboundItem Unbound(SyntaxElement element) => new()
        {
            Span = element.Span,
            Syntax = element,
            Text = tree.Input.Substring(element.Span.Start, element.Span.Length),
            Issues = IssuesOf(element),
        };

        private CellLine BindCellLine(SyntaxNode node, SyntaxNode? separator)
        {
            NumberRange? count = null;
            Ploidy? ploidy = null;
            SexComplement? sex = null;
            CellCount? cells = null;
            var items = ImmutableArray.CreateBuilder<KaryotypeItem>();
            foreach (var child in node.Children.OfType<SyntaxNode>())
            {
                switch (child.Kind)
                {
                    case SyntaxKind.ChromosomeCount:
                        count = BindRange(child, Rules(CountSexOrder));
                        break;
                    case SyntaxKind.Ploidy:
                        ploidy = BindPloidy(child);
                        break;
                    case SyntaxKind.SexChromosomes:
                        sex = BindSex(child);
                        break;
                    case SyntaxKind.Item:
                        items.Add(BindItem(child));
                        break;
                    case SyntaxKind.CellCount:
                        cells = BindCellCount(child);
                        break;
                    default:
                        items.Add(Unbound(child));
                        break;
                }
            }

            var sepToken = separator?.Children[0] as SyntaxToken;
            var sepKind = sepToken?.Kind switch
            {
                null => CellLineSeparator.None,
                TokenKind.Slash => CellLineSeparator.Slash,
                TokenKind.DoubleSlash => CellLineSeparator.DoubleSlash,
                _ => CellLineSeparator.Comma,
            };
            return new CellLine
            {
                Span = node.Span,
                Syntax = node,
                SeparatorBefore = sepKind,
                Count = count,
                Ploidy = ploidy,
                Sex = sex,
                Items = items.ToImmutable(),
                CellCount = cells,
                Issues = IssuesOf(node, separator),
                Kb = Refs(VocabOf(sepToken), sepKind == CellLineSeparator.DoubleSlash ? Rules(TransplantChimera) : []),
            };
        }

        /// <summary><c>46</c>, <c>45~48</c>, <c>~14</c>, <c>12~20</c>, or a missing count.</summary>
        private NumberRange BindRange(SyntaxNode node, IEnumerable<KbRef> kb)
        {
            var tokens = node.Children.OfType<SyntaxToken>().ToList();
            var tilde = tokens.FindIndex(t => t.Kind == TokenKind.Tilde);
            var from = tilde == 0 ? null : Int(tokens.FirstOrDefault());
            var to = tilde >= 0 && tilde + 1 < tokens.Count ? Int(tokens[tilde + 1]) : null;
            return new NumberRange
            {
                Span = node.Span,
                Syntax = node,
                From = from,
                To = to,
                IsRange = tilde >= 0,
                IsMissing = tokens.All(t => t.IsMissing),
                Issues = IssuesOf(node),
                Kb = Refs(kb, tilde >= 0 ? Rules(ApproxInterval) : []),
            };
        }

        private Ploidy BindPloidy(SyntaxNode node)
        {
            var tokens = node.Children.OfType<SyntaxToken>().ToList();
            var inner = tokens.Skip(1).Take(tokens.Count - 2).ToList();
            return new Ploidy
            {
                Span = node.Span,
                Syntax = node,
                Text = string.Concat(inner.Select(t => t.Text)),
                Level = Int(inner.FirstOrDefault()),
                InAngleBrackets = tokens[0].Kind == TokenKind.OpenAngle,
                Issues = IssuesOf(node),
                Kb = Refs(Rules(PloidyInAngleBrackets)),
            };
        }

        private SexComplement BindSex(SyntaxNode node)
        {
            var letters = node.Children.OfType<SyntaxToken>().ToList();
            var suffix = node.ChildNodes(SyntaxKind.Suffix).FirstOrDefault();
            return new SexComplement
            {
                Span = node.Span,
                Syntax = node,
                Chromosomes = [.. letters.Select(t => t.ValueText)],
                Constitutional = suffix is null ? null : BindConstitutional(suffix),
                Issues = IssuesOf(node),
                Kb = Refs(Rules(CountSexOrder)),
            };
        }

        private CellCount BindCellCount(SyntaxNode node)
        {
            var tokens = node.Children.OfType<SyntaxToken>().ToList();
            return new CellCount
            {
                Span = node.Span,
                Syntax = node,
                Cells = Int(tokens.FirstOrDefault(t => t.Kind == TokenKind.Number)),
                IsComposite = tokens.Any(t => t.Kind == TokenKind.Word && t.Text == "cp"),
                Issues = IssuesOf(node),
                Kb = Refs(VocabOf(tokens.Where(t => t.Kind == TokenKind.Word))),
            };
        }

        // ── Items ────────────────────────────────────────────────────────────────────────────────────

        private KaryotypeItem BindItem(SyntaxNode item)
        {
            var core = (SyntaxNode)item.Children[0];
            if (core.Kind != SyntaxKind.Alternative)
            {
                var bound = BindCore(core);
                return bound is SuffixedItem suffixed && item.ChildNodes(SyntaxKind.ItemCellCount).FirstOrDefault() is { } cells
                    ? suffixed with { CellCount = BindCellCount(cells) }
                    : bound;
            }

            var or = core.Children.OfType<SyntaxToken>().Where(t => IsWord(t, "or"));
            return new AlternativeItem
            {
                Span = core.Span,
                Syntax = core,
                Readings = [.. core.Children.OfType<SyntaxNode>().Select(BindCore)],
                Issues = IssuesOf(core),
                Kb = Refs(VocabOf(or), Rules(OrAlternative)),
            };
        }

        private KaryotypeItem BindCore(SyntaxNode core) => core.Kind switch
        {
            SyntaxKind.Abnormality => BindAbnormality(core),
            SyntaxKind.WholeChromosome => BindWholeChromosome(core),
            SyntaxKind.ChromosomeRegion => BindRegion(core),
            SyntaxKind.Ellipsis => new OmittedItems
            {
                Span = core.Span,
                Syntax = core,
                Issues = IssuesOf(core),
                Kb = Refs(VocabOf(core.Tokens())),
            },
            SyntaxKind.BreakpointList => new BandComposition
            {
                Span = core.Span,
                Syntax = core,
                Groups = BindBreakpointList(core, []),
                Issues = IssuesOf(core),
                Kb = Refs(Rules(DetailedSystem)),
            },
            _ => Unbound(core),
        };

        /// <summary>The sign and <c>?</c> of a <see cref="SyntaxKind.Signs"/> node.</summary>
        private static (GainLoss Sign, bool Questioned, IEnumerable<SyntaxToken> Tokens) Signs(SyntaxNode? signs)
        {
            if (signs is null)
            {
                return (GainLoss.None, false, []);
            }

            var tokens = signs.Children.OfType<SyntaxToken>().ToList();
            var sign = tokens.Select(t => t.Kind).FirstOrDefault(k => k is TokenKind.Plus or TokenKind.Minus) switch
            {
                TokenKind.Plus => GainLoss.Gain,
                TokenKind.Minus => GainLoss.Loss,
                _ => GainLoss.None,
            };
            return (sign, tokens.Any(t => t.Kind == TokenKind.Question), tokens);
        }

        /// <summary>Copies, constitutional mark and inheritance from the <see cref="SyntaxKind.Suffix"/> children of <paramref name="node"/>.</summary>
        private (Copies? Copies, ConstitutionalMark? Constitutional, Inheritance? Inheritance) Suffixes(SyntaxNode node)
        {
            Copies? copies = null;
            ConstitutionalMark? constitutional = null;
            Inheritance? inheritance = null;
            foreach (var suffix in node.ChildNodes(SyntaxKind.Suffix))
            {
                var first = (SyntaxToken)suffix.Children[0];
                if (first.Kind is TokenKind.Multiply or TokenKind.ChromosomeLetter)
                {
                    copies ??= new Copies
                    {
                        Span = suffix.Span,
                        Syntax = suffix,
                        Count = Int(suffix.Children.Length > 1 ? suffix.Children[1] as SyntaxToken : null),
                        Issues = IssuesOf(suffix),
                        Kb = Refs(VocabOf(first), Rules(MultipleCopies)),
                    };
                }
                else if (suffix.Children.Any(c => IsWord(c, "c")))
                {
                    constitutional ??= BindConstitutional(suffix);
                }
                else
                {
                    inheritance ??= new Inheritance
                    {
                        Span = suffix.Span,
                        Syntax = suffix,
                        Symbol = first.Text,
                        Issues = IssuesOf(suffix),
                        Kb = Refs(VocabOf(first), Rules(ParentalOrigin)),
                    };
                }
            }

            return (copies, constitutional, inheritance);
        }

        private ConstitutionalMark BindConstitutional(SyntaxNode suffix) => new()
        {
            Span = suffix.Span,
            Syntax = suffix,
            IsQuestioned = suffix.Children.Any(c => c is SyntaxToken { Kind: TokenKind.Question }),
            Issues = IssuesOf(suffix),
            Kb = Refs(VocabOf(suffix.Tokens().Where(t => t.Kind == TokenKind.Word)), Rules(ConstitutionalC)),
        };

        private WholeChromosomeChange BindWholeChromosome(SyntaxNode node)
        {
            var signsNode = node.ChildNodes(SyntaxKind.Signs).FirstOrDefault();
            var (sign, questioned, signTokens) = Signs(signsNode);
            var chrom = node.Children.OfType<SyntaxToken>().First(t => t.Kind is TokenKind.Number or TokenKind.ChromosomeLetter);
            var (copies, constitutional, inheritance) = Suffixes(node);
            return new WholeChromosomeChange
            {
                Span = node.Span,
                Syntax = node,
                Sign = sign,
                IsQuestioned = questioned,
                Chromosome = Chromosome(chrom),
                Copies = copies,
                Constitutional = constitutional,
                Inheritance = inheritance,
                Issues = IssuesOf(node),
                Kb = Refs(VocabOf(signTokens), Rules(GainLossSign), questioned ? Rules(QuestionableItem) : []),
            };
        }

        private ChromosomeRegion BindRegion(SyntaxNode node)
        {
            var (sign, questioned, signTokens) = Signs(node.ChildNodes(SyntaxKind.Signs).FirstOrDefault());
            var chromToken = node.Children.OfType<SyntaxToken>().FirstOrDefault(t => t.Kind is TokenKind.Number or TokenKind.ChromosomeLetter);
            var chrom = chromToken is null ? null : Chromosome(chromToken);
            var elements = ImmutableArray.CreateBuilder<RegionElement>();
            var hasFeature = false;
            var hasLengthSign = false;
            foreach (var child in node.Children)
            {
                switch (child)
                {
                    case SyntaxNode { Kind: SyntaxKind.Band } band:
                        var bp = BindBand(band, chrom);
                        elements.Add(new RegionElement
                        {
                            Span = band.Span,
                            Syntax = band,
                            Kind = RegionElementKind.Band,
                            Text = bp.Text,
                            Band = bp,
                            Issues = bp.Issues,
                        });
                        break;
                    case SyntaxToken { Kind: TokenKind.Word } word:
                        hasFeature = true;
                        elements.Add(new RegionElement
                        {
                            Span = word.Span,
                            Syntax = word,
                            Kind = RegionElementKind.Feature,
                            Text = word.Text,
                            Issues = IssuesOf(word),
                            Kb = Refs(VocabOf(word)),
                        });
                        break;
                    case SyntaxToken { Kind: TokenKind.Plus or TokenKind.Minus } s when s != chromToken && !signTokens.Contains(s):
                        hasLengthSign = true;
                        elements.Add(new RegionElement
                        {
                            Span = s.Span,
                            Syntax = s,
                            Kind = s.Kind == TokenKind.Plus ? RegionElementKind.Increase : RegionElementKind.Decrease,
                            Text = s.Text,
                            Issues = IssuesOf(s),
                            Kb = Refs(VocabOf(s)),
                        });
                        break;
                    default:
                        break;
                }
            }

            var (copies, constitutional, inheritance) = Suffixes(node);
            return new ChromosomeRegion
            {
                Span = node.Span,
                Syntax = node,
                Sign = sign,
                IsQuestioned = questioned,
                Chromosome = chrom,
                Elements = elements.ToImmutable(),
                Copies = copies,
                Constitutional = constitutional,
                Inheritance = inheritance,
                Issues = IssuesOf(node),
                Kb = Refs(VocabOf(signTokens), hasFeature ? Rules(VarDefinition) : [], hasLengthSign ? Rules(VarLengthSign) : []),
            };
        }

        private ChromosomeRef Chromosome(SyntaxToken token, SyntaxToken? questionMark = null, string? arm = null, SyntaxNode? node = null)
        {
            var name = token.Kind == TokenKind.Question ? "?" : token.ValueText;
            return new ChromosomeRef
            {
                Span = node?.Span ?? token.Span,
                Syntax = (SyntaxElement?)node ?? token,
                Name = name,
                Number = Int(token),
                IsQuestioned = questionMark is not null,
                IsUnderlined = token.IsUnderlined,
                Arm = arm,
                Issues = node is null ? IssuesOf(token) : IssuesOf(node),
            };
        }

        // ── Abnormalities ────────────────────────────────────────────────────────────────────────────

        private Abnormality BindAbnormality(SyntaxNode node)
        {
            var (sign, questioned, signTokens) = Signs(node.ChildNodes(SyntaxKind.Signs).FirstOrDefault());
            var leading = node.ChildNodes(SyntaxKind.LeadingCount).FirstOrDefault();
            var parts = node.ChildNodes(SyntaxKind.AbnormalityPart).Select(BindPart).ToImmutableArray();
            var (copies, constitutional, inheritance) = Suffixes(node);
            var abnormality = new Abnormality
            {
                Span = node.Span,
                Syntax = node,
                Sign = sign,
                IsQuestioned = questioned,
                LeadingCount = leading is null ? null : BindRange(leading, []),
                Parts = parts,
                Copies = copies,
                Constitutional = constitutional,
                Inheritance = inheritance,
                Issues = IssuesOf(node),
                Kb = Refs(VocabOf(signTokens), questioned ? Rules(QuestionableItem) : []),
            };
            return abnormality with { Composition = _composer.Compose(abnormality) };
        }

        private AbnormalityPart BindPart(SyntaxNode node)
        {
            var questioned = node.Children[0] is SyntaxToken { Kind: TokenKind.Question };
            var symbols = node.ChildNodes(SyntaxKind.Symbols).SelectMany(s => s.Children.OfType<SyntaxToken>()).Select(BindSymbol).ToImmutableArray();
            var number = node.ChildNodes(SyntaxKind.SymbolNumber).FirstOrDefault();
            var bare = node.ChildNodes(SyntaxKind.BareChromosome).FirstOrDefault();
            var list = node.ChildNodes(SyntaxKind.ChromosomeList).FirstOrDefault();

            ImmutableArray<ChromosomeRef> chromosomes = list is not null
                ? [.. list.ChildNodes(SyntaxKind.ChromosomeSpec).Select(BindChromosomeSpec)]
                : bare is not null ? [Chromosome((SyntaxToken)bare.Children[0], node: bare)] : [];

            // dmin14 (recovered) keeps its number as SymbolNumber; mar1, r2 likewise.
            var groups = node.ChildNodes(SyntaxKind.BreakpointList).SelectMany(b => BindBreakpointList(b, chromosomes)).ToImmutableArray();
            var detailed = groups.Any(g => g.Structure is not null);
            return new AbnormalityPart
            {
                Span = node.Span,
                Syntax = node,
                IsQuestioned = questioned,
                Symbols = symbols,
                SymbolNumber = number is null ? null : Int(number.Children[0] as SyntaxToken),
                Chromosomes = chromosomes,
                HasChromosomeList = list is not null,
                Groups = groups,
                Issues = IssuesOf(node),
                Kb = Refs(questioned ? Rules(QuestionableItem) : [], detailed ? Rules(DetailedSystem) : []),
            };
        }

        private SymbolRef BindSymbol(SyntaxToken token) => new()
        {
            Span = token.Span,
            Syntax = token,
            Symbol = token.Text,
            IsListed = token.Kind == TokenKind.Word,
            Issues = IssuesOf(token),
            Kb = Refs(VocabOf(token), SymbolRules.TryGetValue(token.Text, out var ids) ? Rules(ids) : []),
        };

        /// <summary><c>1</c>, <c>X</c>, <c>?</c>, <c>1̲</c>, <c>2p</c> (arm), in a chromosome list.</summary>
        private ChromosomeRef BindChromosomeSpec(SyntaxNode spec)
        {
            var tokens = spec.Children.OfType<SyntaxToken>().ToList();
            SyntaxToken? question = null;
            var chrom = tokens[0];
            if (chrom.Kind == TokenKind.Question && tokens.Count > 1 && tokens[1].Kind is TokenKind.Number or TokenKind.ChromosomeLetter)
            {
                question = chrom;
                chrom = tokens[1];
            }

            var arm = spec.ChildNodes(SyntaxKind.Band).Select(b => b.Children.OfType<SyntaxToken>().First().Text).FirstOrDefault();
            return Chromosome(chrom, question, arm, spec);
        }

        // ── Breakpoints ──────────────────────────────────────────────────────────────────────────────

        /// <summary>The groups of a breakpoint list; group <c>i</c> belongs to chromosome <c>i</c> (all to the only one, if one).</summary>
        private ImmutableArray<BreakpointGroup> BindBreakpointList(SyntaxNode list, ImmutableArray<ChromosomeRef> chromosomes)
        {
            var groups = list.ChildNodes(SyntaxKind.BreakpointGroup).ToList();
            return
            [
                .. groups.Select((g, i) => BindGroup(
                    g,
                    chromosomes.Length == 1 ? chromosomes[0] : i < chromosomes.Length ? chromosomes[i] : null)),
            ];
        }

        private BreakpointGroup BindGroup(SyntaxNode group, ChromosomeRef? chromosome)
        {
            var detailed = group.Children.Any(c => c is SyntaxToken { Kind: TokenKind.Arrow or TokenKind.Colon or TokenKind.DoubleColon });
            if (!detailed)
            {
                return new BreakpointGroup
                {
                    Span = group.Span,
                    Syntax = group,
                    Chromosome = chromosome,
                    Breakpoints = ShortBreakpoints(group, chromosome),
                    Kb = Refs(Rules(BreakpointGroups)),
                    Issues = IssuesOf(group),
                };
            }

            var structure = BindStructure(group, chromosome);
            return new BreakpointGroup
            {
                Span = group.Span,
                Syntax = group,
                Chromosome = chromosome,
                Structure = structure,
                Breakpoints = JoinBreakpoints(structure),
                Kb = Refs(Rules(BreakpointGroups)),
                Issues = IssuesOf(group),
            };
        }

        /// <summary>Short system: each band or <c>?</c> is a breakpoint; <c>or</c> attaches alternatives, a spaced <c>~</c> a range end.</summary>
        private ImmutableArray<Breakpoint> ShortBreakpoints(SyntaxNode group, ChromosomeRef? chromosome)
        {
            var result = new List<Breakpoint>();
            var pending = PendingLink.None;
            foreach (var child in group.Children)
            {
                if (IsWord(child, "or"))
                {
                    pending = PendingLink.Or;
                    continue;
                }

                if (child is SyntaxToken { Kind: TokenKind.Tilde })
                {
                    pending = PendingLink.Range;
                    continue;
                }

                if (Point(child, chromosome) is not { } point)
                {
                    continue;
                }

                if (result.Count > 0 && pending == PendingLink.Or)
                {
                    var previous = result[^1];
                    result[^1] = previous with
                    {
                        Alternatives = previous.Alternatives.Add(point),
                        Span = TextSpan.FromBounds(previous.Span.Start, point.Span.End),
                        Kb = Refs(previous.Kb, Rules(OrAlternative)),
                    };
                }
                else if (result.Count > 0 && pending == PendingLink.Range)
                {
                    var previous = result[^1];
                    result[^1] = previous with
                    {
                        RangeEnd = point,
                        Span = TextSpan.FromBounds(previous.Span.Start, point.Span.End),
                        Kb = Refs(previous.Kb, Rules(ApproxInterval)),
                    };
                }
                else
                {
                    result.Add(point);
                }

                pending = PendingLink.None;
            }

            return [.. result];
        }

        private enum PendingLink
        {
            None,
            Or,
            Range,
        }

        /// <summary>A band or landmark child of a breakpoint group as a point; <c>null</c> for anything else.</summary>
        private Breakpoint? Point(SyntaxElement element, ChromosomeRef? chromosome) => element switch
        {
            SyntaxNode { Kind: SyntaxKind.Band } band => BindBand(band, chromosome),
            SyntaxNode { Kind: SyntaxKind.Landmark } landmark => BindLandmark(landmark, chromosome),
            _ => null,
        };

        /// <summary>Detailed system: pieces of points joined by <c>→</c>, separated by <c>::</c> or ended by <c>:</c>.</summary>
        private DerivativeStructure BindStructure(SyntaxNode group, ChromosomeRef? chromosome)
        {
            JoinKind? leading = null;
            JoinKind? trailing = null;
            var pieces = new List<List<(SyntaxElement Element, Breakpoint Point)>>();
            var joins = new List<JoinKind>();
            List<(SyntaxElement, Breakpoint)>? current = null;
            JoinKind? pendingJoin = null;
            var pendingOr = false;

            foreach (var child in group.Children)
            {
                if (child is SyntaxToken { Kind: TokenKind.Colon or TokenKind.DoubleColon } join)
                {
                    var kind = join.Kind == TokenKind.Colon ? JoinKind.Break : JoinKind.Junction;
                    if (current is null && pieces.Count == 0)
                    {
                        leading = kind;
                    }
                    else
                    {
                        pendingJoin = kind;
                        current = null;
                    }

                    continue;
                }

                if (IsWord(child, "or"))
                {
                    pendingOr = true;
                    continue;
                }

                if (Point(child, chromosome) is not { } point)
                {
                    continue; // →, ~ and recovered text: the arrow only links points of one piece
                }

                if (pendingOr && current is { Count: > 0 })
                {
                    var (element, previous) = current[^1];
                    current[^1] = (element, previous with { Alternatives = previous.Alternatives.Add(point), Kb = Refs(previous.Kb, Rules(OrAlternative)) });
                    pendingOr = false;
                    continue;
                }

                pendingOr = false;
                if (current is null)
                {
                    if (pendingJoin is { } j)
                    {
                        joins.Add(j);
                        pendingJoin = null;
                    }

                    current = [];
                    pieces.Add(current);
                }

                current.Add((child, point));
            }

            trailing = pendingJoin;
            return new DerivativeStructure
            {
                Span = group.Span,
                Syntax = group,
                LeadingJoin = leading,
                TrailingJoin = trailing,
                Joins = [.. joins],
                Pieces =
                [
                    .. pieces.Select(p => new SegmentPiece
                    {
                        Span = SpanOf([.. p.Select(x => x.Element)]),
                        Points = [.. p.Select(x => x.Point)],
                        Issues = IssuesOf([.. p.Select(x => x.Element)]),
                    }),
                ],
                Issues = IssuesOf(group),
                Kb = Refs(Rules(DetailedSystem)),
            };
        }

        /// <summary>The breakpoints of a band composition: the points at the joins, except arm ends (pter/qter/ter) and inserted hsr/neo material; distinct, in order.</summary>
        private static ImmutableArray<Breakpoint> JoinBreakpoints(DerivativeStructure s)
        {
            var points = new List<Breakpoint>();
            for (var i = 0; i < s.Pieces.Length; i++)
            {
                var piece = s.Pieces[i];
                var joinBefore = i == 0 ? s.LeadingJoin : s.Joins[i - 1];
                var joinAfter = i == s.Pieces.Length - 1 ? s.TrailingJoin : s.Joins[i];
                if (joinBefore is not null)
                {
                    points.Add(piece.From);
                }

                if (joinAfter is not null && (piece.Points.Length > 1 || joinBefore is null))
                {
                    points.Add(piece.To);
                }
            }

            return [.. points.Where(p => !p.IsTerminal && p.Landmark is not ("hsr" or "neo")).DistinctBy(p => (p.Chromosome?.Name, p.Text))];
        }

        /// <summary>A band: <c>[chromosome]arm[digits[.digits]]</c> with <c>?</c> and <c>~</c>.</summary>
        private Breakpoint BindBand(SyntaxNode band, ChromosomeRef? implied)
        {
            var tokens = band.Children.OfType<SyntaxToken>().ToList();
            var i = 0;
            ChromosomeRef? chromosome = implied;
            var written = false;
            if (tokens[0].Kind is TokenKind.Number or TokenKind.ChromosomeLetter or TokenKind.Question)
            {
                chromosome = Chromosome(tokens[0]);
                written = true;
                i = 1;
            }

            var arm = tokens[i];
            var tilde = tokens.FindIndex(i, t => t.Kind == TokenKind.Tilde);
            var main = tokens.Skip(i).Take((tilde < 0 ? tokens.Count : tilde) - i).ToList();
            var text = Text(main);
            var questioned = main.Any(t => t.Kind == TokenKind.Question);
            var digits = Text(main.Skip(1));
            var bandNumber = questioned || digits.Length == 0 ? null : digits.Replace(',', '.');

            Breakpoint? rangeEnd = null;
            if (tilde >= 0 && tilde + 1 < tokens.Count)
            {
                var endTokens = tokens.Skip(tilde + 1).ToList();
                var endDigits = Text(endTokens);
                rangeEnd = Resolve(new Breakpoint
                {
                    Span = SpanOf([.. endTokens]),
                    Syntax = endTokens[0],
                    Chromosome = chromosome,
                    ChromosomeWritten = written,
                    Text = arm.Text + endDigits,
                    Arm = arm.Text,
                    BandNumber = endDigits.Contains('?', StringComparison.Ordinal) ? null : endDigits,
                    IsQuestioned = endDigits.Contains('?', StringComparison.Ordinal),
                    Issues = IssuesOf([.. endTokens]),
                });
            }

            return Resolve(new Breakpoint
            {
                Span = band.Span,
                Syntax = band,
                Chromosome = chromosome,
                ChromosomeWritten = written,
                Text = tilde < 0 ? text : Text(tokens.Skip(i)),
                Arm = arm.Text,
                BandNumber = bandNumber,
                IsQuestioned = questioned,
                RangeEnd = rangeEnd,
                Issues = IssuesOf(band),
                Kb = Refs(VocabOf(arm), questioned ? Rules(QuestionMarkDesignation) : [], rangeEnd is not null ? Rules(ApproxInterval) : []),
            });
        }

        /// <summary><c>pter</c>, <c>3qter</c>, <c>cen</c>, <c>?cen</c>, <c>hsr</c>, <c>neo</c>, <c>?</c>.</summary>
        private Breakpoint BindLandmark(SyntaxNode landmark, ChromosomeRef? implied)
        {
            var tokens = landmark.Children.OfType<SyntaxToken>().ToList();
            var word = tokens[^1];
            var chromosome = tokens.Count > 1 ? Chromosome(tokens[0]) : implied;
            var name = word.Kind == TokenKind.Question ? "?" : word.Text;
            return new Breakpoint
            {
                Span = landmark.Span,
                Syntax = landmark,
                Chromosome = chromosome,
                ChromosomeWritten = tokens.Count > 1,
                Text = name,
                Landmark = name,
                IsQuestioned = name == "?",
                Issues = IssuesOf(landmark),
                Kb = Refs(VocabOf(word)),
            };
        }

        /// <summary>Adds the band set's entry (and its <c>band:</c> ref) when the full name is a band of the edition.</summary>
        private Breakpoint Resolve(Breakpoint bp)
        {
            if (bp.Name is not { } name || _kb.Bands?.Find(name) is not { } band)
            {
                return bp;
            }

            return bp with { KbBand = band, Kb = Refs(bp.Kb, [KbRef.Band(band.Name)]) };
        }

        private static string Text(IEnumerable<SyntaxToken> tokens)
        {
            var sb = new StringBuilder();
            foreach (var t in tokens.Where(t => !t.IsMissing))
            {
                sb.Append(t.ValueText);
            }

            return sb.ToString();
        }
    }
}
