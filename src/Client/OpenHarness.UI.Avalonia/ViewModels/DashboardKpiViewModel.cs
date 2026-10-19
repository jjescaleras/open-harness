using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public enum KpiKind
{
    Neutral,
    Success,
    Warning,
    Danger
}

public partial class DashboardKpiViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _value = "—";

    [ObservableProperty]
    private string _caption = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccentColor))]
    private KpiKind _kind = KpiKind.Neutral;

    public string AccentColor => Kind switch
    {
        KpiKind.Success => "#10B981",
        KpiKind.Warning => "#F59E0B",
        KpiKind.Danger => "#EF4444",
        _ => "#6366F1"
    };
}
