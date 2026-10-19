using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.Client.Core.Http;
using OpenHarness.Client.Core.Models;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class SandboxApprovalViewModel : ViewModelBase, IDisposable
{
    private readonly IApprovalsApiClient? _approvalsApiClient;
    private readonly System.Timers.Timer? _countdownTimer;

    [ObservableProperty]
    private Guid _sessionId;

    [ObservableProperty]
    private Guid _toolExecutionId;

    [ObservableProperty]
    private string _toolName = "Bash Command";

    [ObservableProperty]
    private string _commandOrTarget = string.Empty;

    [ObservableProperty]
    private string? _argumentsJson;

    [ObservableProperty]
    private string _riskLevel = "High";

    [ObservableProperty]
    private int _remainingSeconds = 300; // 5 minutes default timeout

    [ObservableProperty]
    private string _remainingTimeFormatted = "05:00";

    [ObservableProperty]
    private string? _reason;

    [ObservableProperty]
    private bool _isSubmitting;

    [ObservableProperty]
    private bool _isExpired;

    [ObservableProperty]
    private bool? _decisionResult;

    public event Action<bool>? RequestClose;

    public SandboxApprovalViewModel(IApprovalsApiClient? approvalsApiClient = null)
    {
        _approvalsApiClient = approvalsApiClient;

        _countdownTimer = new System.Timers.Timer(1000);
        _countdownTimer.Elapsed += OnTimerTick;
        _countdownTimer.AutoReset = true;
        _countdownTimer.Start();
    }

    private void OnTimerTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (RemainingSeconds > 0)
        {
            RemainingSeconds--;
            var minutes = RemainingSeconds / 60;
            var seconds = RemainingSeconds % 60;
            RemainingTimeFormatted = $"{minutes:D2}:{seconds:D2}";
        }
        else
        {
            _countdownTimer?.Stop();
            IsExpired = true;
            RemainingTimeFormatted = "Expired";
            // Auto deny on expiration
            PostToUi(() =>
            {
                if (!IsSubmitting && DecisionResult is null)
                {
                    _ = DenyAsync();
                }
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    public async Task ApproveOnceAsync()
    {
        await SubmitDecisionAsync(approved: true, rememberForSession: false);
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    public async Task ApproveAndRememberAsync()
    {
        await SubmitDecisionAsync(approved: true, rememberForSession: true);
    }

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    public async Task DenyAsync()
    {
        await SubmitDecisionAsync(approved: false, rememberForSession: false);
    }

    private bool CanSubmit() => !IsSubmitting && !IsExpired;

    private async Task SubmitDecisionAsync(bool approved, bool rememberForSession)
    {
        if (IsSubmitting) return;

        IsSubmitting = true;
        _countdownTimer?.Stop();

        try
        {
            if (_approvalsApiClient is not null)
            {
                var request = new ClientApprovalDecisionRequest(
                    ToolExecutionId,
                    approved,
                    Reason ?? (approved ? "Approved by user" : "Denied by user"),
                    rememberForSession);

                await _approvalsApiClient.SubmitApprovalAsync(SessionId, request);
            }

            DecisionResult = approved;
            RequestClose?.Invoke(approved);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private static void PostToUi(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }
        catch
        {
            action();
        }
    }

    public void Dispose()
    {
        _countdownTimer?.Stop();
        _countdownTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

