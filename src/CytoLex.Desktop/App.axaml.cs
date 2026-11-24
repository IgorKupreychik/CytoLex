using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CytoLex.Desktop.ExternalCheckers;
using CytoLex.Desktop.Rules;
using CytoLex.Desktop.ViewModels;
using CytoLex.Desktop.Views;
using CytoLex.External.CytoGps;

namespace CytoLex.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The CytoGPS tab (T-081) is optional: a missing or broken setup only shows its reason in that tab.
            // The Settings tab (T-089) edits config/local.yaml of the repo checkout the app runs from (none: read-only).
            var repoRoot = RepoLocator.Find();
            var (checker, setupError) = CytoGpsSetup.TryCreate(repoRoot);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(null, RuleStore.DefaultPath, checker, setupError, repoRoot),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
