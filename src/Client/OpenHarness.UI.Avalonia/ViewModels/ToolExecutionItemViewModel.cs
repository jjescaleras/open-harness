using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class ToolExecutionItemViewModel : ViewModelBase
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private string _status = "Pending";

    [ObservableProperty]
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private DateTimeOffset? _completedAt;

    [ObservableProperty]
    private string? _arguments;

    [ObservableProperty]
    private string? _result;

    [ObservableProperty]
    private bool _isDiff;

    [ObservableProperty]
    private string? _diffContent;

    public string DurationFormatted
    {
        get
        {
            if (CompletedAt is null) return "...";
            var elapsed = CompletedAt.Value - StartedAt;
            return $"{elapsed.TotalMilliseconds:F0} ms";
        }
    }
}

