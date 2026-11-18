using CommunityToolkit.Mvvm.ComponentModel;

namespace CytoLex.Desktop.ViewModels;

/// <summary>Placeholder view model; the single-string check comes in T-036.</summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Status { get; set; } = "Validator not implemented yet (T-036).";
}
