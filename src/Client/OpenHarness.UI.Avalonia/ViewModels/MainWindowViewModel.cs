using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.UI.Avalonia.Services;

namespace OpenHarness.UI.Avalonia.ViewModels;

/// <summary>
/// Dashboard shown by <c>MainWindow</c> at startup. Populated with sample data until
/// it is wired to the Client.Core API clients.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IChatWindowService? _chatWindowService;

    public ObservableCollection<DashboardKpiViewModel> Kpis { get; } = [];
    public ObservableCollection<SessionItemViewModel> RecentSessions { get; } = [];
    public ObservableCollection<NavigationItemViewModel> MenuItems { get; } = [];

    [ObservableProperty]
    private string _userEmail = "developer@openharness.internal";

    [ObservableProperty]
    private bool _isSampleData = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardSelected))]
    private NavigationItemViewModel? _selectedMenuItem;

    /// <summary>Left-hand menu panel; visible by default and collapsible.</summary>
    [ObservableProperty]
    private bool _isMenuPanelVisible = true;

    /// <summary>Right-hand multi-purpose panel; hidden by default.</summary>
    [ObservableProperty]
    private bool _isAuxPanelVisible;

    [ObservableProperty]
    private string _auxPanelTitle = "Panel";

    /// <summary>
    /// Content hosted by the multi-purpose panel, resolved to a view through <c>ViewLocator</c>.
    /// When null the panel shows a placeholder.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _auxPanelContent;

    public bool IsDashboardSelected => SelectedMenuItem?.Target == NavigationTarget.Dashboard;

    public MainWindowViewModel(IChatWindowService? chatWindowService = null)
    {
        _chatWindowService = chatWindowService;
        LoadMenu();
        LoadSampleData();
    }

    [RelayCommand]
    public void OpenChat()
    {
        _chatWindowService?.OpenChat();
    }

    [RelayCommand]
    public void Navigate(NavigationItemViewModel? item)
    {
        if (item is null) return;

        // Chat lives in its own window; the dashboard keeps its current selection.
        if (item.Target == NavigationTarget.Chat)
        {
            OpenChat();
            return;
        }

        SelectedMenuItem = item;
    }

    [RelayCommand]
    public void ToggleMenuPanel() => IsMenuPanelVisible = !IsMenuPanelVisible;

    [RelayCommand]
    public void ToggleAuxPanel() => IsAuxPanelVisible = !IsAuxPanelVisible;

    partial void OnSelectedMenuItemChanged(NavigationItemViewModel? oldValue, NavigationItemViewModel? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
    }

    private void LoadMenu()
    {
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Dashboard, Title = "Dashboard", Icon = "📊" });
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Chat, Title = "Chat", Icon = "💬" });
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Sessions, Title = "Sessions", Icon = "🗂", IsAvailable = false });
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Settings, Title = "Settings", Icon = "⚙", IsAvailable = false });

        SelectedMenuItem = MenuItems[0];
    }

    private void LoadSampleData()
    {
        Kpis.Add(new DashboardKpiViewModel { Title = "Active Sessions", Value = "3", Caption = "12 total this week", Kind = KpiKind.Neutral });
        Kpis.Add(new DashboardKpiViewModel { Title = "Pending Approvals", Value = "2", Caption = "Sandbox requests awaiting review", Kind = KpiKind.Warning });
        Kpis.Add(new DashboardKpiViewModel { Title = "Tokens Today", Value = "48.2K", Caption = "31.5K input · 16.7K output", Kind = KpiKind.Neutral });
        Kpis.Add(new DashboardKpiViewModel { Title = "Tool Executions", Value = "127", Caption = "98% completed successfully", Kind = KpiKind.Success });

        var now = DateTimeOffset.UtcNow;
        RecentSessions.Add(new SessionItemViewModel { Title = "Refactor auth slices", Status = "Streaming", UpdatedAt = now.AddMinutes(-12) });
        RecentSessions.Add(new SessionItemViewModel { Title = "Add HNSW integration tests", Status = "WaitingForApproval", UpdatedAt = now.AddHours(-1) });
        RecentSessions.Add(new SessionItemViewModel { Title = "Fix SSE reconnection", Status = "Completed", UpdatedAt = now.AddHours(-5) });
    }
}
