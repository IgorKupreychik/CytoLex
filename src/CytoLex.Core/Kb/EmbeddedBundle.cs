using System.Text.Json;

namespace CytoLex.Core.Kb;

/// <summary>
/// Access to the KB bundle embedded in this assembly at build time (docs/design/core.md §6).
/// The loader behind <c>IKnowledgeBase</c> (T-028, T-060) reads it through this class.
/// </summary>
internal static class EmbeddedBundle
{
    /// <summary>Manifest resource name set by <c>CytoLex.Core.csproj</c>.</summary>
    public const string ResourceName = "CytoLex.Kb.bundle.json";

    /// <summary>Opens the embedded bundle. The caller disposes the stream.</summary>
    /// <exception cref="InvalidOperationException">The assembly was built without the bundle.</exception>
    public static Stream Open() =>
        typeof(EmbeddedBundle).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException(
            $"Embedded KB bundle '{ResourceName}' is missing; rebuild after 'uv run --directory tools kb build'.");

    private static readonly Lazy<JsonDocument> _document = new(() =>
    {
        using var stream = Open();
        return JsonDocument.Parse(stream);
    });

    /// <summary>The embedded bundle, parsed once and shared by <see cref="KnowledgeBase"/> and <see cref="Vocabulary"/>.</summary>
    public static JsonDocument Document => _document.Value;
}
