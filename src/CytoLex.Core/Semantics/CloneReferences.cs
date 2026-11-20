namespace CytoLex.Core.Semantics;

/// <summary>
/// Resolves the clone references of a karyotype's cell lines (§11.1.4 ¶1–2): <c>idem</c> and <c>sl</c>/<c>sl1</c> refer to
/// the first line; <c>slN</c> to the N-th line written in full (the N-th stemline, E7); <c>sdl</c> to the nearest earlier
/// sideline and <c>sdlN</c> to the N-th sideline (E1, E2, E6). A sideline is a line written with a reference. No target
/// (a reference on the first line, <c>sdl</c> before any sideline) gives <c>null</c>. Shared by the binder and by the
/// clone-order fixes, which must not change what a reference points to (T-032).
/// </summary>
internal static class CloneReferences
{
    /// <summary>One line's reference: its symbol and number, or <c>null</c> for a line written in full.</summary>
    public readonly record struct Reference(string Symbol, int? Number);

    /// <summary>The target line index of each line (<c>null</c> for lines written in full and unresolved references).</summary>
    public static int?[] Resolve(IReadOnlyList<Reference?> lines)
    {
        var targets = new int?[lines.Count];
        var full = new List<int>();
        var sidelines = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not { } r)
            {
                full.Add(i);
                continue;
            }

            targets[i] = (r.Symbol, r.Number) switch
            {
                ("idem", _) or ("sl", null or 1) => i > 0 ? 0 : null,
                ("sl", { } n) => n >= 1 && n <= full.Count ? full[n - 1] : null,
                ("sdl", null) => sidelines.Count > 0 ? sidelines[^1] : null,
                ("sdl", { } n) => n >= 1 && n <= sidelines.Count ? sidelines[n - 1] : null,
                _ => null,
            };
            sidelines.Add(i);
        }

        return targets;
    }

    /// <summary>The first <c>idem</c>/<c>sl</c>/<c>sdl</c> item of a line, if any.</summary>
    public static Abnormality? ReferenceItem(CellLine line) =>
        line.Items.OfType<Abnormality>().FirstOrDefault(a => a.Kind is "idem" or "sl" or "sdl");

    /// <summary>The reference of a line for <see cref="Resolve"/>.</summary>
    public static Reference? Of(CellLine line) =>
        ReferenceItem(line) is { } item ? new Reference(item.Kind!, item.Parts[0].SymbolNumber) : null;
}
