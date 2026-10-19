using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.Client.Core.Authentication;
using OpenHarness.Client.Core.Http;
using OpenHarness.UI.Avalonia.Localization;
using OpenHarness.UI.Avalonia.Services;

namespace OpenHarness.UI.Avalonia.ViewModels;

/// <summary>
/// Dashboard shown by <c>MainWindow</c> at startup. Populated with sample data until
/// it is wired to the Client.Core API clients.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IChatWindowService? _chatWindowService;
    private readonly ISettingsApiClient? _settingsApiClient;
    private SettingsGeneralViewModel? _settingsGeneral;

    public ObservableCollection<DashboardKpiViewModel> Kpis { get; } = [];
    public ObservableCollection<SessionItemViewModel> RecentSessions { get; } = [];
    public ObservableCollection<NavigationItemViewModel> MenuItems { get; } = [];

    [ObservableProperty]
    private string _userEmail = "developer@openharness.internal";

    [ObservableProperty]
    private bool _isSampleData = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardSelected))]
    [NotifyPropertyChangedFor(nameof(Breadcrumb))]
    private NavigationItemViewModel? _selectedMenuItem;

    /// <summary>Page shown in the center when the dashboard is not selected; resolved to a view by <c>ViewLocator</c>.</summary>
    [ObservableProperty]
    private ViewModelBase? _currentPage;

    /// <summary>Left-hand menu panel; visible by default and collapsible.</summary>
    [ObservableProperty]
    private bool _isMenuPanelVisible = true;

    /// <summary>Right-hand multi-purpose panel; hidden by default.</summary>
    [ObservableProperty]
    private bool _isAuxPanelVisible;

    [ObservableProperty]
    private string _auxPanelTitle = Localizer.Instance["Shell_AuxPanel_Title"];

    /// <summary>
    /// Content hosted by the multi-purpose panel, resolved to a view through <c>ViewLocator</c>.
    /// When null the panel shows a placeholder.
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _auxPanelContent;

    public bool IsDashboardSelected => SelectedMenuItem?.Target == NavigationTarget.Dashboard;

    /// <summary>Settings are global and only administrators may see or change them.</summary>
    public bool IsAdmin { get; }

    /// <summary>"Configuración / General" style path of the selected entry, shown in the header.</summary>
    public string Breadcrumb => SelectedMenuItem is null
        ? string.Empty
        : SelectedMenuItem.Parent is { } parent
            ? $"{parent.Title} / {SelectedMenuItem.Title}"
            : SelectedMenuItem.Title;

    public MainWindowViewModel(
        IChatWindowService? chatWindowService = null,
        ISettingsApiClient? settingsApiClient = null,
        string userRole = ClientRoles.Admin)
    {
        _chatWindowService = chatWindowService;
        _settingsApiClient = settingsApiClient;
        IsAdmin = string.Equals(userRole, ClientRoles.Admin, StringComparison.OrdinalIgnoreCase);

        LoadMenu();
        LoadSampleData();
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Breadcrumb));
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

        // A collapsed group opens its sub-menu and lands on its first entry; an expanded one collapses.
        if (item.HasChildren)
        {
            if (item.IsExpanded)
            {
                item.IsExpanded = false;
                return;
            }

            item.IsExpanded = true;
            if (SelectedMenuItem?.Parent != item)
            {
                Navigate(item.Children[0]);
            }
            return;
        }

        SelectedMenuItem = item;
        CurrentPage = CreatePage(item);
    }

    [RelayCommand]
    public void ToggleMenuPanel() => IsMenuPanelVisible = !IsMenuPanelVisible;

    [RelayCommand]
    public void ToggleAuxPanel() => IsAuxPanelVisible = !IsAuxPanelVisible;

    partial void OnSelectedMenuItemChanged(NavigationItemViewModel? oldValue, NavigationItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
            if (oldValue.Parent is { } oldParent) oldParent.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
            if (newValue.Parent is { } newParent) newParent.IsSelected = true;
        }
    }

    private ViewModelBase? CreatePage(NavigationItemViewModel item)
    {
        switch (item.Target)
        {
            case NavigationTarget.Dashboard:
                return null;
            case NavigationTarget.SettingsGeneral:
                // Kept alive so unsaved edits survive navigating to another page and back.
                if (_settingsGeneral is null)
                {
                    _settingsGeneral = new SettingsGeneralViewModel(_settingsApiClient, canEdit: IsAdmin);
                    _ = _settingsGeneral.ReloadAsync();
                }
                return _settingsGeneral;
            default:
                return new ComingSoonViewModel(item.TitleKey);
        }
    }

    private void LoadMenu()
    {
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Dashboard, TitleKey = "Menu_Dashboard", Icon = "📊" });
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Chat, TitleKey = "Menu_Chat", Icon = "💬" });
        MenuItems.Add(new NavigationItemViewModel { Target = NavigationTarget.Sessions, TitleKey = "Menu_Sessions", Icon = "🗂", IsAvailable = false });

        if (IsAdmin)
        {
            var settings = new NavigationItemViewModel { Target = NavigationTarget.Settings, TitleKey = "Menu_Settings", Icon = "⚙" };
            settings
                .AddChild(new NavigationItemViewModel { Target = NavigationTarget.SettingsGeneral, TitleKey = "Menu_Settings_General", Icon = "⚙" })
                .AddChild(new NavigationItemViewModel { Target = NavigationTarget.SettingsConnections, TitleKey = "Menu_Settings_Connections", Icon = "🔌", IsAvailable = false })
                .AddChild(new NavigationItemViewModel { Target = NavigationTarget.SettingsModels, TitleKey = "Menu_Settings_Models", Icon = "🧠", IsAvailable = false });
            MenuItems.Add(settings);
        }

        SelectedMenuItem = MenuItems[0];
    }

    private void LoadSampleData()
    {
        var t = Localizer.Instance;
        Kpis.Add(new DashboardKpiViewModel { Title = t["Dashboard_Kpi_ActiveSessions"], Value = "3", Caption = t["Dashboard_Kpi_ActiveSessions_Caption"], Kind = KpiKind.Neutral });
        Kpis.Add(new DashboardKpiViewModel { Title = t["Dashboard_Kpi_PendingApprovals"], Value = "2", Caption = t["Dashboard_Kpi_PendingApprovals_Caption"], Kind = KpiKind.Warning });
        Kpis.Add(new DashboardKpiViewModel { Title = t["Dashboard_Kpi_TokensToday"], Value = "48.2K", Caption = t["Dashboard_Kpi_TokensToday_Caption"], Kind = KpiKind.Neutral });
        Kpis.Add(new DashboardKpiViewModel { Title = t["Dashboard_Kpi_ToolExecutions"], Value = "127", Caption = t["Dashboard_Kpi_ToolExecutions_Caption"], Kind = KpiKind.Success });

        var now = DateTimeOffset.UtcNow;
        RecentSessions.Add(new SessionItemViewModel { Title = "Refactor auth slices", Status = "Streaming", UpdatedAt = now.AddMinutes(-12) });
        RecentSessions.Add(new SessionItemViewModel { Title = "Add HNSW integration tests", Status = "WaitingForApproval", UpdatedAt = now.AddHours(-1) });
        RecentSessions.Add(new SessionItemViewModel { Title = "Fix SSE reconnection", Status = "Completed", UpdatedAt = now.AddHours(-5) });
    }
}
