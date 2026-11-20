using CytoLex.Core.Kb;
using CytoLex.Core.Segmentation;
using CytoLex.Core.Semantics;
using CytoLex.Core.Tests.Parsing;

namespace CytoLex.Core.Tests.Semantics;

/// <summary>Parses and binds notation strings the way the validator does (T-065).</summary>
internal static class BindHelper
{
    public static IEditionKb Kb2016 { get; } = KnowledgeBase.LoadEmbedded().ForEdition("2016")!;

    public static Binder Binder2016 { get; } = new(Kb2016);

    /// <summary>The models of the karyotype segments of <paramref name="text"/>.</summary>
    public static IReadOnlyList<KaryotypeModel> Bind(string text, bool fragment = false) =>
        [.. ParseHelper.Parse(text, fragment).Where(t => t.Segment.Type == NotationType.Karyotype).Select(Binder2016.Bind)];

    /// <summary>The models of every parsed segment of <paramref name="text"/> (karyotype and arr), as the validator binds them.</summary>
    public static IReadOnlyList<SegmentModel> BindAll(string text, bool fragment = false) =>
        [.. ParseHelper.Parse(text, fragment).Select(t => t.Segment.Type == NotationType.Arr ? (SegmentModel)Binder2016.BindArr(t) : Binder2016.Bind(t))];

    /// <summary>The model of a one-segment arr string (T-034).</summary>
    public static ArrModel BindArr(string text) => Assert.IsType<ArrModel>(Assert.Single(BindAll(text)));

    /// <summary>The model of a one-segment string.</summary>
    public static KaryotypeModel BindOne(string text, bool fragment = false) => Assert.Single(Bind(text, fragment));

    /// <summary>The nodes of type <typeparamref name="T"/> in <paramref name="model"/>, depth first.</summary>
    public static List<T> All<T>(MeaningNode model)
        where T : MeaningNode => [.. model.DescendantsAndSelf().OfType<T>()];

    /// <summary>The single node of type <typeparamref name="T"/>.</summary>
    public static T One<T>(MeaningNode model)
        where T : MeaningNode => Assert.Single(All<T>(model));

    /// <summary>Breakpoint names (<c>5q13</c>), or the written text with the chromosome when no name (<c>1q2?3</c>, <c>?</c>).</summary>
    public static string Label(Breakpoint b) => b.Name ?? (b.Chromosome?.Name ?? "") + b.Text;

    public static IEnumerable<string> Labels(IEnumerable<Breakpoint> breakpoints) => breakpoints.Select(Label);

    /// <summary>The ref strings of a node.</summary>
    public static IEnumerable<string> Refs(MeaningNode node) => node.Kb.Select(k => k.ToString());
}
