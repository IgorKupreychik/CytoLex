using CytoLex.External;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop.ExternalCheckers;

/// <summary>Creates the CytoGPS checker of the desktop app's CytoGPS tab (T-081) from this machine's setup.</summary>
public static class CytoGpsSetup
{
    /// <summary>
    /// The checker for <c>config/local.yaml</c> (and the environment); never throws: when the setup can't be read (repo root
    /// not found, malformed YAML), the checker is <c>null</c> and <c>Error</c> says why. A checker whose settings are
    /// missing is returned as is: its <see cref="IExternalChecker.CheckAvailability"/> names the missing key. Called again
    /// after the Settings tab saved (T-089).
    /// </summary>
    /// <param name="repoRoot">The repo root (default: found from the app directory).</param>
    /// <param name="environment">Reads an environment variable (default: the process environment).</param>
    public static (IExternalChecker? Checker, string? Error) TryCreate(string? repoRoot = null, Func<string, string?>? environment = null)
    {
        try
        {
            return (new CytoGpsChecker(CytoGpsConfig.Load(repoRoot, environment)), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return (null, ex.Message);
        }
    }
}
