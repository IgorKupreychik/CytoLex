using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CytoLex.Core.Composing;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using static CytoLex.Core.Semantics.Binder;

namespace CytoLex.Core.Semantics;

/// <summary>Binds a karyotype segment's syntax tree to a <see cref="KaryotypeModel"/> (T-065; see <see cref="Binder"/>).</summary>
internal sealed class KaryotypeBinder(IEditionKb kb, SyntaxTree tree) : BinderBase(kb, tree)
{
    private readonly Composer _composer = new(id => kb.Rule(id) is not null);

    // ── Root and cell lines ──────────────────────────────────────────────────────────────────────

    public KaryotypeModel BindRoot()
    {
        var root = Tree.Root;
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
            Tree = Tree,
            Issues = Tree.Diagnostics,
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

    internal KaryotypeItem BindItem(SyntaxNode item)
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
}
