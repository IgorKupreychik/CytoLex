using System.Collections.Immutable;
using System.Globalization;
using CytoLex.Core.Kb;
using CytoLex.Core.Syntax;
using CytoLex.Core.Text;
using static CytoLex.Core.Semantics.Binder;

namespace CytoLex.Core.Semantics;

/// <summary>Binding of arr segments (ISCN 2016 ch. 14, T-034): the arr syntax tree → <see cref="ArrModel"/>.</summary>
/// <remarks>
/// Bands are the karyotype's <see cref="Breakpoint"/> nodes and chromosomes its <see cref="ChromosomeRef"/>s, so the
/// general band and chromosome rules (<c>BAND.MUST_EXIST</c>, <c>CHROM.IDENTIFIERS</c>) read arr results unchanged.
/// As for karyotypes, the binder interprets and does not judge.
/// </remarks>
internal sealed class ArrBinder(IEditionKb kb, SyntaxTree tree) : BinderBase(kb, tree)
{
    public ArrModel BindArrRoot()
    {
        var root = Tree.Root;
        var keyword = (SyntaxToken)root.Children[0];
        var aberrations = ImmutableArray.CreateBuilder<ArrAberration>();
        var unbound = ImmutableArray.CreateBuilder<UnboundItem>();
        GenomeBuild? build = null;
        foreach (var child in root.Children.OfType<SyntaxNode>())
        {
            switch (child.Kind)
            {
                case SyntaxKind.GenomeBuild:
                    build = BindGenomeBuild(child);
                    break;
                case SyntaxKind.ArrAberration:
                    aberrations.Add(BindArrAberration(child));
                    break;
                default:
                    unbound.Add(Unbound(child));
                    break;
            }
        }

        return new ArrModel
        {
            Span = root.Span,
            Syntax = root,
            Tree = Tree,
            Issues = Tree.Diagnostics,
            Keyword = keyword.Span,
            Build = build,
            Aberrations = aberrations.ToImmutable(),
            Unbound = unbound.ToImmutable(),
            Kb = Refs(VocabOf(keyword)),
        };
    }

    private GenomeBuild BindGenomeBuild(SyntaxNode node)
    {
        var tokens = node.Children.OfType<SyntaxToken>().ToList();
        var close = tokens[^1];
        var inner = tokens.Skip(1).Take(close.Kind == TokenKind.CloseBracket ? tokens.Count - 2 : tokens.Count - 1);
        return new GenomeBuild
        {
            Span = node.Span,
            Syntax = node,
            Text = Text(inner),
            CloseBracket = close.Span,
            FollowedBySpace = close.IsMissing ? close.Span.End < node.FullSpan.End : close.HasTrailingTrivia,
            Issues = IssuesOf(node),
            Kb = Refs(VocabOf(tokens), Rules(GenomeBuildRequired)),
        };
    }

    private ArrAberration BindArrAberration(SyntaxNode node)
    {
        var chromosomes = ImmutableArray<ArrChromosomeEntry>.Empty;
        var regions = ImmutableArray.CreateBuilder<ArrRegion>();
        var isGroup = false;
        var results = ImmutableArray.CreateBuilder<ArrCopyNumber>();
        ArrProportion? proportion = null;
        var qualifiers = ImmutableArray.CreateBuilder<ArrQualifier>();
        TextSpan regionSpan = default;
        SyntaxToken? previous = null;

        foreach (var child in node.Children)
        {
            if (child is SyntaxNode n)
            {
                switch (n.Kind)
                {
                    case SyntaxKind.ArrChromosomeGroup:
                        chromosomes = [.. n.ChildNodes(SyntaxKind.ChromosomeSpec).Select(BindChromosomeEntry)];
                        regionSpan = n.Span;
                        break;
                    case SyntaxKind.ArrRegion:
                        regions.Add(BindArrRegion(n));
                        regionSpan = n.Span;
                        break;
                    case SyntaxKind.ArrRegionGroup:
                        regions.AddRange(n.ChildNodes(SyntaxKind.ArrRegion).Select(BindArrRegion));
                        isGroup = true;
                        regionSpan = n.Span;
                        break;
                    case SyntaxKind.CopyNumber:
                        results.Add(BindCopyNumber(n));
                        break;
                    case SyntaxKind.Proportion:
                        proportion ??= BindProportion(n, beforeCopy: results.Count == 0 && !regions.Any(r => r.Positions is { IsDetailed: true }));
                        break;
                    case SyntaxKind.ArrQualifier:
                        qualifiers.Add(BindQualifier(n, previous));
                        break;
                }
            }

            if (child.Tokens().LastOrDefault(t => !t.IsMissing) is { } last)
            {
                previous = last;
            }
        }

        var regionList = regions.ToImmutable();
        return new ArrAberration
        {
            Span = node.Span,
            Syntax = node,
            Chromosomes = chromosomes,
            Regions = regionList,
            IsRegionGroup = isGroup,
            Copy = results.Count > 0 ? results[0] : null,
            Results = results.ToImmutable(),
            Proportion = proportion,
            Qualifiers = qualifiers.ToImmutable(),
            RegionSpan = regionSpan,
            Issues = IssuesOf(node),
            Kb = Refs(chromosomes.IsEmpty ? [] : Rules(ArrAberrationsOnly), regionList.IsEmpty ? [] : Rules(ArrRegionFormat)),
        };
    }

    /// <summary><c>8</c>, <c>16q</c>, <c>1–22</c> in a chromosome group.</summary>
    private ArrChromosomeEntry BindChromosomeEntry(SyntaxNode spec)
    {
        var tokens = spec.Children.OfType<SyntaxToken>().ToList();
        var arm = tokens.Count > 1 && tokens[1].Kind == TokenKind.Word ? tokens[1] : null;
        var first = Chromosome(tokens[0], arm: arm?.Text, node: arm is null || tokens.Count > 2 ? null : spec);
        var sign = tokens.FindIndex(t => t.Kind == TokenKind.Minus);
        ChromosomeRef? last = sign >= 0 && sign + 1 < tokens.Count ? Chromosome(tokens[sign + 1]) : null;
        ImmutableArray<string> names = last is null ? [first.Name]
            : first.Number is { } a && last.Number is { } b && a <= b ? [.. Enumerable.Range(a, b - a + 1).Select(i => i.ToString(CultureInfo.InvariantCulture))]
            : [first.Name, last.Name];
        return new ArrChromosomeEntry
        {
            Span = spec.Span,
            Syntax = spec,
            First = first,
            Last = last,
            RangeSign = sign >= 0 ? tokens[sign].Span : null,
            Names = names,
            Issues = IssuesOf(spec),
            Kb = Refs(VocabOf(tokens)),
        };
    }

    /// <summary>Bands (split into <c>or</c> readings) and the positions of a region.</summary>
    private ArrRegion BindArrRegion(SyntaxNode node)
    {
        var readings = new List<List<SyntaxElement>> { new() };
        SyntaxNode? positions = null;
        foreach (var child in node.Children)
        {
            if (child is SyntaxToken { Kind: TokenKind.Word, Text: "or" })
            {
                readings.Add([]);
            }
            else if (child is SyntaxNode { Kind: SyntaxKind.Positions } p)
            {
                positions = p;
            }
            else
            {
                readings[^1].Add(child);
            }
        }

        var main = BindReading(readings[0]);
        return main with
        {
            Span = node.Span,
            Syntax = node,
            Alternatives = [.. readings.Skip(1).Where(r => r.Count > 0).Select(BindReading)],
            Positions = positions is null ? null : BindPositions(positions),
            Issues = IssuesOf(node),
            Kb = Refs(main.Kb, readings.Count > 1 ? Rules(OrAlternative) : []),
        };
    }

    private ArrRegion BindReading(List<SyntaxElement> elements)
    {
        ChromosomeRef? chromosome = null;
        var bands = ImmutableArray.CreateBuilder<Breakpoint>();
        var hyphens = ImmutableArray.CreateBuilder<TextSpan>();
        foreach (var element in elements)
        {
            switch (element)
            {
                case SyntaxNode { Kind: SyntaxKind.Band } band:
                    var bp = BindBand(band, chromosome);
                    chromosome ??= bp.Chromosome;
                    bands.Add(bp);
                    break;
                case SyntaxNode { Kind: SyntaxKind.Landmark } landmark:
                    bands.Add(BindLandmark(landmark, chromosome));
                    break;
                case SyntaxToken { Kind: TokenKind.Minus } minus:
                    hyphens.Add(minus.Span);
                    break;
            }
        }

        var span = SpanOf([.. elements]);
        return new ArrRegion
        {
            Span = span,
            Syntax = elements.Count > 0 ? elements[0] : null,
            Chromosome = chromosome,
            Bands = bands.ToImmutable(),
            BandHyphens = hyphens.ToImmutable(),
            BandSpan = span,
            Issues = IssuesOf([.. elements]),
            Kb = Refs(Rules(ArrRegionFormat)),
        };
    }

    private ArrPositions BindPositions(SyntaxNode node)
    {
        var items = new List<ArrPositionItem>();
        var alternative = false;
        foreach (var child in node.Children)
        {
            if (child is SyntaxToken { Kind: TokenKind.Word, Text: "or" })
            {
                alternative = true;
            }
            else if (child is SyntaxNode { Kind: SyntaxKind.PositionSpan } span)
            {
                var item = BindPositionItem(span);
                if (alternative && items.Count > 0)
                {
                    items[^1] = items[^1] with { Alternatives = items[^1].Alternatives.Add(item) };
                }
                else
                {
                    items.Add(item);
                }

                alternative = false;
            }
        }

        var close = node.Children[^1] as SyntaxToken;
        return new ArrPositions
        {
            Span = node.Span,
            Syntax = node,
            Items = [.. items],
            CloseParen = close is { Kind: TokenKind.CloseParen } ? close.Span : new TextSpan(node.Span.End, 0),
            Issues = IssuesOf(node),
            Kb = Refs(Rules(ArrNucleotideFormat)),
        };
    }

    /// <summary><c>163146681_183022312</c>, <c>163,146,681</c>, <c>163002425×2</c>.</summary>
    private ArrPositionItem BindPositionItem(SyntaxNode node)
    {
        var start = new List<SyntaxToken>();
        var end = new List<SyntaxToken>();
        var separators = ImmutableArray.CreateBuilder<TextSpan>();
        SyntaxToken? joiner = null;
        ArrCopyNumber? copy = null;
        foreach (var child in node.Children)
        {
            switch (child)
            {
                case SyntaxNode { Kind: SyntaxKind.CopyNumber } c:
                    copy = BindCopyNumber(c);
                    break;
                case SyntaxToken { Kind: TokenKind.Underscore or TokenKind.Minus } j when joiner is null:
                    joiner = j;
                    break;
                case SyntaxToken { Kind: TokenKind.Comma } comma:
                    separators.Add(comma.Span);
                    (joiner is null ? start : end).Add(comma);
                    break;
                case SyntaxToken t:
                    (joiner is null ? start : end).Add(t);
                    break;
            }
        }

        return new ArrPositionItem
        {
            Span = node.Span,
            Syntax = node,
            Start = Long(start),
            End = joiner is null ? null : Long(end),
            StartSpan = SpanOf([.. start]),
            EndSpan = end.Count == 0 ? null : SpanOf([.. end]),
            Joiner = joiner?.Text,
            JoinerSpan = joiner?.Span,
            GroupSeparators = separators.ToImmutable(),
            Copy = copy,
            Issues = IssuesOf(node),
            Kb = Refs(VocabOf(joiner)),
        };
    }

    /// <summary>The number written by <paramref name="tokens"/>, thousands separators left out.</summary>
    private static long? Long(List<SyntaxToken> tokens)
    {
        var digits = string.Concat(tokens.Where(t => t is { Kind: TokenKind.Number, IsMissing: false }).Select(t => t.ValueText));
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private ArrCopyNumber BindCopyNumber(SyntaxNode node)
    {
        var tokens = node.Children.OfType<SyntaxToken>().ToList();
        if (tokens[0].Kind == TokenKind.Word)
        {
            var (kind, rule) = tokens[0].Text switch
            {
                "amp" => (ArrResultKind.Amplification, ArrCopyNumberRule),
                "cx" => (ArrResultKind.Complex, ArrComplex),
                _ => (ArrResultKind.Chromothripsis, ArrChromothripsis),
            };
            return new ArrCopyNumber { Span = node.Span, Syntax = node, Kind = kind, Issues = IssuesOf(node), Kb = Refs(VocabOf(tokens), Rules(rule)) };
        }

        var min = tokens.Count > 1 ? Int(tokens[1]) : null;
        var sign = tokens.Count > 3 ? tokens[2] : null;
        return new ArrCopyNumber
        {
            Span = node.Span,
            Syntax = node,
            Kind = ArrResultKind.CopyNumber,
            Min = min,
            Max = sign is null ? min : Int(tokens[3]),
            RangeSign = sign?.Text,
            RangeSignSpan = sign?.Span,
            Issues = IssuesOf(node),
            Kb = Refs(VocabOf(tokens), Rules(ArrCopyNumberRule)),
        };
    }

    private ArrProportion BindProportion(SyntaxNode node, bool beforeCopy)
    {
        var tokens = node.Children.OfType<SyntaxToken>().ToList();
        var inner = tokens.Skip(1).Where(t => t.Kind != TokenKind.CloseBracket).ToList();
        var text = string.Concat(inner.Where(t => !t.IsMissing).Select(t => t.Text));
        var isFraction = inner.Exists(t => t.Kind == TokenKind.Dot)
            && inner.TrueForAll(t => t.Kind is TokenKind.Number or TokenKind.Dot)
            && double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            && value is > 0 and <= 1;
        return new ArrProportion
        {
            Span = node.Span,
            Syntax = node,
            Text = text,
            IsFraction = isFraction,
            BeforeCopyNumber = beforeCopy,
            Issues = IssuesOf(node),
            Kb = Refs(Rules(ArrMosaicProportion)),
        };
    }

    private ArrQualifier BindQualifier(SyntaxNode node, SyntaxToken? previous)
    {
        var word = (SyntaxToken)node.Children[0];
        var space = previous is { HasTrailingTrivia: true };
        var rule = word.Text is "mat" or "pat" or "inh" or "dn" ? ArrInheritanceSpacing : ArrZygosity;
        return new ArrQualifier
        {
            Span = node.Span,
            Syntax = node,
            Word = word.Text,
            SpaceBefore = space,
            AfterParenthesis = previous is { Kind: TokenKind.CloseParen },
            Gap = previous is null ? new TextSpan(word.Span.Start, 0) : TextSpan.FromBounds(previous.Span.End, word.Span.Start),
            Issues = IssuesOf(node),
            Kb = Refs(VocabOf(word), Rules(rule)),
        };
    }
}
