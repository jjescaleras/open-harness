using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public enum NavigationTarget
{
    Dashboard,
    Chat,
    Sessions,
    Settings
}

public partial class NavigationItemViewModel : ViewModelBase
{
    public required NavigationTarget Target { get; init; }
    public required string Title { get; init; }
    public string Icon { get; init; } = string.Empty;

    /// <summary>False for menu entries whose screen is not built yet.</summary>
    public bool IsAvailable { get; init; } = true;

    [ObservableProperty]
    private bool _isSelected;
}
