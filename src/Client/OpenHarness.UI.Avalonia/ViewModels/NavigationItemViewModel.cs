using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenHarness.UI.Avalonia.Localization;

namespace OpenHarness.UI.Avalonia.ViewModels;

public enum NavigationTarget
{
    Dashboard,
    Chat,
    Sessions,
    Settings,
    SettingsGeneral,
    SettingsConnections,
    SettingsModels
}

public partial class NavigationItemViewModel : ViewModelBase
{
    public NavigationItemViewModel()
    {
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    public required NavigationTarget Target { get; init; }

    /// <summary>Resource key of the label in <c>Strings.resx</c>.</summary>
    public required string TitleKey { get; init; }
    public string Icon { get; init; } = string.Empty;

    /// <summary>False for menu entries whose screen is not built yet.</summary>
    public bool IsAvailable { get; init; } = true;

    public NavigationItemViewModel? Parent { get; private set; }
    public ObservableCollection<NavigationItemViewModel> Children { get; } = [];
    public bool HasChildren => Children.Count > 0;

    public string Title => Localizer.Instance[TitleKey];

    /// <summary>True for the selected entry and for the parent of a selected sub-entry.</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    public NavigationItemViewModel AddChild(NavigationItemViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
        OnPropertyChanged(nameof(HasChildren));
        return this;
    }
}
