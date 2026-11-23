using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using static CytoLex.Core.Semantics.Binder;

namespace CytoLex.Core.Semantics;

/// <summary>Binding of in situ segments (ISCN 2016 ch. 13, T-033): the FISH syntax tree → <see cref="FishModel"/>.</summary>
/// <remarks>
/// Abnormalities are bound by the <see cref="KaryotypeBinder"/> (so the general band and chromosome rules read them
/// unchanged); their locus groups, the regions, loci, counts and cells are bound here. As for karyotypes, the binder
/// interprets and does not judge.
/// </remarks>
internal sealed class FishBinder(IEditionKb kb, SyntaxTree tree, KaryotypeBinder karyotype) : BinderBase(kb, tree)
{
    public FishModel BindRoot()
    {
        var root = Tree.Root;
        var prefixes = ImmutableArray.CreateBuilder<Prefix>();
        var lines = ImmutableArray.CreateBuilder<FishCellLine>();
        var unbound = ImmutableArray.CreateBuilder<UnboundItem>();
        var keywords = new List<SyntaxToken>();
        var startsWithDoubleSlash = false;
        SyntaxNode? separator = null;

        foreach (var child in root.Children)
        {
            switch (child)
            {
                case SyntaxToken { Kind: TokenKind.Word } word when lines.Count == 0 && prefixes.Count == 0 && FishGrammar.KeywordWords.Contains(word.Text):
                    keywords.Add(word);
                    break;
                case SyntaxToken:
                    break; // EndOfSegment
                case SyntaxNode { Kind: SyntaxKind.CellLineSeparator } when keywords.Count == 0 && lines.Count == 0:
                    startsWithDoubleSlash = true;
                    break;
                case SyntaxNode { Kind: SyntaxKind.CellLineSeparator } sep:
                    separator = sep;
                    break;
                case SyntaxNode { Kind: SyntaxKind.Prefix } prefix:
                    var symbol = (SyntaxToken)prefix.Children[0];
                    prefixes.Add(new Prefix
                    {
                        Span = prefix.Span,
                        Syntax = prefix,
                        Symbol = symbol.Text,
                        Issues = IssuesOf(prefix),
                        Kb = Refs(VocabOf(symbol), Rules(MosaicChimeraPrefix)),
                    });
                    break;
                case SyntaxNode { Kind: SyntaxKind.FishCellLine } line:
                    lines.Add(BindCellLine(line, separator));
                    separator = null;
                    break;
                default:
                    unbound.Add(Unbound(child));
                    break;
            }
        }

        var relation = lines.Skip(1).Any(l => l.Separator == CellLineSeparator.DoubleSlash) || startsWithDoubleSlash
            ? CellLineRelation.Transplant
            : prefixes.Select(p => p.Symbol).FirstOrDefault(s => s is "mos" or "chi") switch
            {
                "mos" => CellLineRelation.Mosaic,
                "chi" => CellLineRelation.Chimera,
                _ => CellLineRelation.None,
            };

        var keywordSpan = keywords.Count == 0 ? new TextSpan(root.Span.Start, 0) : TextSpan.FromBounds(keywords[0].Span.Start, keywords[^1].Span.End);
        return new FishModel
        {
            Span = root.Span,
            Syntax = root,
            Tree = Tree,
            Type = Tree.Segment.Type,
            Keyword = keywordSpan,
            KeywordFollowedBySpace = keywords.Count > 0 && keywords[^1].HasTrailingTrivia,
            StartsWithDoubleSlash = startsWithDoubleSlash,
            Prefixes = prefixes.ToImmutable(),
            CellLines = lines.ToImmutable(),
            Unbound = unbound.ToImmutable(),
            Relation = relation,
            Issues = Tree.Diagnostics,
            Kb = Refs(VocabOf(keywords), Rules(KeywordRule(Tree.Segment.Type))),
        };
    }

    private static string KeywordRule(NotationType type) => type switch
    {
        NotationType.NucIsh => NucIshSignalCountFormat,
        NotationType.FibIsh => FibIshFormat,
        NotationType.RevIsh => RevIshFormat,
        NotationType.Cgh => CghFormat,
        _ => IshStructuralFormat,
    };

    private FishCellLine BindCellLine(SyntaxNode node, SyntaxNode? separator) => new()
    {
        Span = node.Span,
        Syntax = node,
        Separator = (separator?.Children[0] as SyntaxToken)?.Kind switch
        {
            null => CellLineSeparator.None,
            TokenKind.DoubleSlash => CellLineSeparator.DoubleSlash,
            _ => CellLineSeparator.Slash,
        },
        Items = [.. node.ChildNodes(SyntaxKind.FishItem).Select(BindItem)],
        Issues = IssuesOf(node),
        Kb = Refs(Rules(IshCellLines)),
    };

    private FishItem BindItem(SyntaxNode node)
    {
        var core = (SyntaxNode)node.Children[0];
        var cells = node.ChildNodes(SyntaxKind.FishCellCount).FirstOrDefault();
        var stray = node.Children.OfType<SyntaxToken>().FirstOrDefault(t => t.Kind is TokenKind.Plus or TokenKind.Minus);
        var item = new FishItem
        {
            Span = node.Span,
            Syntax = node,
            Cells = cells is null ? null : BindCells(cells),
            StraySign = stray?.Span,
            Issues = IssuesOf(node),
        };

        return core.Kind switch
        {
            SyntaxKind.FishRegion => item with { Region = BindRegion(core) },
            SyntaxKind.Item => item with { Abnormality = karyotype.BindItem(core), Groups = AbnormalityGroups(core) },
            _ => item,
        };
    }

    /// <summary>The parenthesis groups of an ish abnormality in input order, with the part each follows.</summary>
    private ImmutableArray<FishGroup> AbnormalityGroups(SyntaxNode item)
    {
        var groups = ImmutableArray.CreateBuilder<FishGroup>();
        foreach (var abnormality in item.DescendantNodesAndSelf().Where(n => n.Kind == SyntaxKind.Abnormality))
        {
            var part = -1;
            foreach (var child in abnormality.Children.OfType<SyntaxNode>())
            {
                if (child.Kind == SyntaxKind.AbnormalityPart)
                {
                    part++;
                    foreach (var g in child.Children.OfType<SyntaxNode>())
                    {
                        Add(g, part);
                    }
                }
                else
                {
                    Add(child, Math.Max(part, 0));
                }
            }
        }

        return groups.ToImmutable();

        void Add(SyntaxNode g, int part)
        {
            switch (g.Kind)
            {
                case SyntaxKind.ChromosomeList:
                    groups.Add(new FishGroup(part, FishGroupKind.Chromosomes, g.Span, null));
                    break;
                case SyntaxKind.BreakpointList:
                    groups.Add(new FishGroup(part, FishGroupKind.Breakpoints, g.Span, null));
                    break;
                case SyntaxKind.LocusGroup:
                    groups.Add(new FishGroup(part, FishGroupKind.Loci, g.Span, BindGroup(g)));
                    break;
            }
        }
    }

    private FishRegion BindRegion(SyntaxNode node)
    {
        ChromosomeRef? chromosome = null;
        Breakpoint? band = null;
        var inParentheses = false;
        var groups = new List<LocusGroup>();
        foreach (var child in node.Children)
        {
            switch (child)
            {
                case SyntaxToken { Kind: TokenKind.Number or TokenKind.ChromosomeLetter } chrom:
                    chromosome = Chromosome(chrom);
                    break;
                case SyntaxNode { Kind: SyntaxKind.Band } b:
                    band = BindBand(b, null);
                    chromosome = band.Chromosome;
                    break;
                case SyntaxNode { Kind: SyntaxKind.BreakpointList } list:
                    inParentheses = true;
                    var inner = list.DescendantNodesAndSelf().FirstOrDefault(n => n.Kind == SyntaxKind.Band);
                    if (inner is not null)
                    {
                        band = BindBand(inner, null);
                        chromosome = band.Chromosome;
                    }

                    break;
                case SyntaxNode { Kind: SyntaxKind.LocusGroup } g:
                    groups.Add(BindGroup(g));
                    break;
                case SyntaxNode { Kind: SyntaxKind.LocusCount } count when groups.Count > 0:
                    groups[^1] = groups[^1] with
                    {
                        SharedCount = BindCount(count),
                        SpaceBeforeSharedCount = HasSpaceBefore(count),
                    };
                    break;
                case SyntaxToken { Kind: TokenKind.Word } word when groups.Count > 0:
                    groups[^1] = groups[^1] with { Qualifiers = groups[^1].Qualifiers.Add(Qualifier(word)) };
                    break;
            }
        }

        return new FishRegion
        {
            Span = node.Span,
            Syntax = node,
            Chromosome = chromosome,
            Band = band,
            BandInParentheses = inParentheses,
            Groups = [.. groups],
            Issues = IssuesOf(node),
            Kb = Refs(Rules(IshNormalChromosomeFormat)),
        };
    }

    private LocusGroup BindGroup(SyntaxNode node)
    {
        var loci = ImmutableArray.CreateBuilder<Locus>();
        var separators = ImmutableArray.CreateBuilder<LocusSeparator>();
        var separatorSpans = ImmutableArray.CreateBuilder<TextSpan>();
        var breakpoints = ImmutableArray.CreateBuilder<Breakpoint>();
        SyntaxToken? open = null;
        SyntaxToken? close = null;
        var expectSeparator = false;
        foreach (var child in node.Children)
        {
            switch (child)
            {
                case SyntaxToken { Kind: TokenKind.OpenParen } t when open is null:
                    open = t;
                    break;
                case SyntaxToken { Kind: TokenKind.CloseParen } t:
                    close = t;
                    break;
                case SyntaxNode { Kind: SyntaxKind.Band } band:
                    breakpoints.Add(BindBand(band, null));
                    break;
                case SyntaxNode { Kind: SyntaxKind.Locus } locus:
                    if (expectSeparator)
                    {
                        separators.Add(LocusSeparator.None);
                        separatorSpans.Add(new TextSpan(loci[^1].Span.End, 0));
                    }

                    loci.Add(BindLocus(locus));
                    expectSeparator = true;
                    break;
                case SyntaxToken t when SeparatorOf(t) is { } sep:
                    separators.Add(sep);
                    separatorSpans.Add(t.Span);
                    expectSeparator = false;
                    break;
            }
        }

        // A separator after the last locus (none written in the book) is not kept.
        while (separators.Count >= loci.Count && separators.Count > 0)
        {
            separators.RemoveAt(separators.Count - 1);
            separatorSpans.RemoveAt(separatorSpans.Count - 1);
        }

        var position = separators.Any(s => s is LocusSeparator.Con or LocusSeparator.Sep);
        return new LocusGroup
        {
            Span = node.Span,
            Syntax = node,
            Loci = loci.ToImmutable(),
            Separators = separators.ToImmutable(),
            SeparatorSpans = separatorSpans.ToImmutable(),
            Breakpoints = breakpoints.ToImmutable(),
            SpaceBefore = open is not null && HasSpaceBefore(open),
            Open = open?.Span ?? new TextSpan(node.Span.Start, 0),
            Close = close?.Span ?? new TextSpan(node.Span.End, 0),
            Issues = IssuesOf(node),
            Kb = Refs(Rules(position ? NucIshSignalPositionFormat : IshLocusList)),
        };
    }

    private static LocusSeparator? SeparatorOf(SyntaxToken t) => t.Kind switch
    {
        TokenKind.Comma => LocusSeparator.Comma,
        TokenKind.Semicolon => LocusSeparator.Semicolon,
        TokenKind.Slash => LocusSeparator.Slash,
        TokenKind.Word when t.Text == "con" => LocusSeparator.Con,
        TokenKind.Word when t.Text == "sep" => LocusSeparator.Sep,
        _ => null,
    };

    private Locus BindLocus(SyntaxNode node)
    {
        var name = node.Children.OfType<SyntaxToken>().FirstOrDefault(t => t.Kind == TokenKind.Probe);
        var leading = node.ChildNodes(SyntaxKind.Signs).FirstOrDefault();
        var status = node.Children.OfType<SyntaxToken>().Where(t => t.Kind is TokenKind.Plus or TokenKind.Minus).ToList();
        var count = node.ChildNodes(SyntaxKind.LocusCount).FirstOrDefault();
        var qualifiers = node.Children.OfType<SyntaxToken>().Where(t => t.Kind == TokenKind.Word && LocusGrammar.QualifierWords.Contains(t.Text)).Select(Qualifier);
        return new Locus
        {
            Span = node.Span,
            Syntax = node,
            Name = name is { IsMissing: false } ? name.Text : "",
            NameSpan = name?.Span ?? new TextSpan(node.Span.Start, 0),
            LeadingSigns = leading is null ? "" : string.Concat(leading.Tokens().Select(t => t.Text)),
            LeadingSignsSpan = leading?.Span,
            Status = string.Concat(status.Select(t => t.Text)),
            StatusSpan = status.Count == 0 ? null : TextSpan.FromBounds(status[0].Span.Start, status[^1].Span.End),
            Count = count is null ? null : BindCount(count),
            Qualifiers = [.. qualifiers],
            Issues = IssuesOf(node),
            Kb = Refs(Rules(IshLocusDesignation)),
        };
    }

    private LocusQualifier Qualifier(SyntaxToken word) => new(word.Text, word.Span, HasSpaceBefore(word));

    private LocusCount BindCount(SyntaxNode node)
    {
        var numbers = node.Children.OfType<SyntaxToken>().Where(t => t.Kind == TokenKind.Number && !t.IsMissing).ToList();
        var times = (SyntaxToken)node.Children[0];
        return new LocusCount
        {
            Span = node.Span,
            Syntax = node,
            Low = numbers.Count > 0 ? Int(numbers[0]) : null,
            High = numbers.Count > 1 ? Int(numbers[1]) : null,
            IsOpenEnded = node.Children.OfType<SyntaxToken>().Any(t => t.Kind == TokenKind.CloseAngle),
            Text = Tree.Input[times.Span.End..node.Span.End],
            Issues = IssuesOf(node),
            Kb = Refs(VocabOf(times)),
        };
    }

    private FishCells BindCells(SyntaxNode node)
    {
        var inner = node.Children.OfType<SyntaxToken>().Where(t => t.Kind is not (TokenKind.OpenBracket or TokenKind.CloseBracket) && !t.IsMissing).ToList();
        var text = string.Concat(inner.Select(t => t.Text));
        var numbers = inner.Where(t => t.Kind == TokenKind.Number).ToList();
        var slash = inner.Any(t => t.Kind == TokenKind.Slash);
        return new FishCells
        {
            Span = node.Span,
            Syntax = node,
            Text = text,
            Count = numbers.Count > 0 ? int.Parse(numbers[0].ValueText, NumberStyles.None, CultureInfo.InvariantCulture) : null,
            Total = slash && numbers.Count > 1 ? int.Parse(numbers[1].ValueText, NumberStyles.None, CultureInfo.InvariantCulture) : null,
            IsPercentage = text.Contains('%', StringComparison.Ordinal),
            Issues = IssuesOf(node),
            Kb = Refs(Rules(IshCellLines)),
        };
    }

    /// <summary>Whitespace separates <paramref name="element"/> from the token before it.</summary>
    private bool HasSpaceBefore(SyntaxElement element)
    {
        var first = element.Tokens().First();
        if (!first.Leading.IsEmpty)
        {
            return true;
        }

        var start = first.Span.Start;
        return start > 0 && char.IsWhiteSpace(Tree.Input[start - 1]);
    }
}
