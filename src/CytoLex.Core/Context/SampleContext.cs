namespace CytoLex.Core.Context;

/// <summary>
/// The sample context of a string (docs/design/core.md §3.3). KB <c>context: neoplastic</c> maps to
/// <see cref="Acquired"/>; KB <c>meiotic</c> is a notation type, not a sample context. An unknown
/// context is represented as <c>null</c>.
/// </summary>
public enum SampleContext
{
    /// <summary>Constitutional (inherited or de novo germline).</summary>
    Constitutional,

    /// <summary>Acquired (neoplastic).</summary>
    Acquired,
}
