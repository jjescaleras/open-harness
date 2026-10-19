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

    [ObservableProperty]
    private string _userEmail = "developer@openharness.internal";

    [ObservableProperty]
    private bool _isSampleData = true;

    public MainWindowViewModel(IChatWindowService? chatWindowService = null)
    {
        _chatWindowService = chatWindowService;
        LoadSampleData();
    }

    [RelayCommand]
    public void OpenChat()
    {
        _chatWindowService?.OpenChat();
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
