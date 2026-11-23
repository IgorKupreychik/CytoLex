namespace CytoLex.Desktop.ViewModels;

/// <summary>
/// A tab of the single-string results (T-081): an edition panel (<see cref="EditionPanelViewModel"/>) or the CytoGPS panel
/// (<see cref="CytoGpsPanelViewModel"/>). The tab header reads these.
/// </summary>
public interface IResultTab
{
    /// <summary>The tab header, e.g. <c>ISCN 2016 ✓ valid</c>.</summary>
    string Header { get; }

    /// <summary>Whether the header is shown as valid (green).</summary>
    bool IsValid { get; }

    /// <summary>Whether the header is shown as not valid (red).</summary>
    bool IsInvalid { get; }

    /// <summary>Whether the header is greyed out (edition not implemented, CytoGPS not configured).</summary>
    bool IsNotImplemented { get; }
}
