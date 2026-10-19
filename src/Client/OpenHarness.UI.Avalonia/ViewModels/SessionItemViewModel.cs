using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class SessionItemViewModel : ViewModelBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _title = "New Session";

    [ObservableProperty]
    private string _status = "Idle";

    [ObservableProperty]
    private long _totalInputTokens;

    [ObservableProperty]
    private long _totalOutputTokens;

    [ObservableProperty]
    private DateTimeOffset _updatedAt = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private bool _isSelected;

    public long TotalTokens => TotalInputTokens + TotalOutputTokens;

    public string TotalTokensFormatted
    {
        get
        {
            var total = TotalTokens;
            if (total >= 1_000_000) return $"{total / 1_000_000.0:F1}M tok";
            if (total >= 1_000) return $"{total / 1_000.0:F1}k tok";
            return $"{total} tok";
        }
    }

    public string StatusBadgeColor => Status switch
    {
        "Streaming" => "#F59E0B",
        "WaitingForApproval" => "#EF4444",
        "Completed" => "#10B981",
        _ => "#64748B"
    };
}

