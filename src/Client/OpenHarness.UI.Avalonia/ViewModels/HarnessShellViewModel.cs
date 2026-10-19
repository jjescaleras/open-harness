using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.Client.Core.Http;
using OpenHarness.Client.Core.Models;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class HarnessShellViewModel : ViewModelBase
{
    private readonly ISessionsApiClient? _sessionsApiClient;
    private readonly IApprovalsApiClient? _approvalsApiClient;

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];
    public ObservableCollection<ToolExecutionItemViewModel> ToolExecutions { get; } = [];

    [ObservableProperty]
    private SessionItemViewModel? _selectedSession;

    [ObservableProperty]
    private ToolExecutionItemViewModel? _selectedToolExecution;

    [ObservableProperty]
    private ChatStreamViewModel _chatStream;

    [ObservableProperty]
    private DiffEditorViewModel _diffEditor;

    [ObservableProperty]
    private string _userEmail = "developer@openharness.internal";

    [ObservableProperty]
    private string _userRole = "Administrator";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasPendingApproval;

    [ObservableProperty]
    private SandboxApprovalViewModel? _pendingApproval;

    public event Func<SandboxApprovalViewModel, Task<bool>>? ShowApprovalDialogRequested;

    public HarnessShellViewModel(
        ISessionsApiClient? sessionsApiClient = null,
        IApprovalsApiClient? approvalsApiClient = null)
    {
        _sessionsApiClient = sessionsApiClient;
        _approvalsApiClient = approvalsApiClient;

        _chatStream = new ChatStreamViewModel(sessionsApiClient);
        _diffEditor = new DiffEditorViewModel
        {
            FilePath = "No file selected",
            Content = "// Diff and code inspection output will appear here when a tool execution is selected."
        };

        _chatStream.ApprovalRequired += OnApprovalRequired;
        _chatStream.ToolExecutionUpdated += OnToolExecutionUpdated;
    }

    [RelayCommand]
    public async Task RefreshSessionsAsync()
    {
        if (_sessionsApiClient is null) return;

        IsLoading = true;
        try
        {
            var sessionDtos = await _sessionsApiClient.ListSessionsAsync();
            Sessions.Clear();

            foreach (var dto in sessionDtos)
            {
                var item = new SessionItemViewModel
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    Status = dto.Status,
                    TotalInputTokens = dto.TotalInputTokens,
                    TotalOutputTokens = dto.TotalOutputTokens,
                    UpdatedAt = dto.UpdatedAtUtc
                };
                Sessions.Add(item);
            }

            if (SelectedSession is null && Sessions.Count > 0)
            {
                SelectedSession = Sessions[0];
            }
        }
        catch
        {
            // Error handling or offline fallback
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CreateNewSessionAsync()
    {
        var title = $"Session #{Sessions.Count + 1}";
        SessionItemViewModel newItem;

        if (_sessionsApiClient is not null)
        {
            try
            {
                var created = await _sessionsApiClient.CreateSessionAsync(new ClientCreateSessionRequest(title));
                newItem = new SessionItemViewModel
                {
                    Id = created.Id,
                    Title = created.Title,
                    Status = created.Status,
                    TotalInputTokens = created.TotalInputTokens,
                    TotalOutputTokens = created.TotalOutputTokens,
                    UpdatedAt = created.UpdatedAtUtc
                };
            }
            catch
            {
                newItem = new SessionItemViewModel { Title = title };
            }
        }
        else
        {
            newItem = new SessionItemViewModel { Title = title };
        }

        Sessions.Insert(0, newItem);
        SelectedSession = newItem;
    }

    partial void OnSelectedSessionChanged(SessionItemViewModel? value)
    {
        if (value is null) return;

        foreach (var s in Sessions)
        {
            s.IsSelected = s == value;
        }

        ChatStream.CurrentSessionId = value.Id;
        _ = LoadSessionDetailsAsync(value.Id);
    }

    partial void OnSelectedToolExecutionChanged(ToolExecutionItemViewModel? value)
    {
        if (value is null) return;

        if (value.IsDiff && !string.IsNullOrEmpty(value.DiffContent))
        {
            DiffEditor.FilePath = value.ToolName;
            DiffEditor.IsDiffMode = true;
            DiffEditor.Content = value.DiffContent;
        }
        else
        {
            DiffEditor.FilePath = value.ToolName;
            DiffEditor.IsDiffMode = false;
            DiffEditor.Content = $"// Execution ID: {value.Id}\n// Status: {value.Status}\n// Started: {value.StartedAt:O}\n\n// Arguments:\n{value.Arguments}\n\n// Result:\n{value.Result}";
        }
    }

    private async Task LoadSessionDetailsAsync(Guid sessionId)
    {
        if (_sessionsApiClient is null) return;

        try
        {
            var detail = await _sessionsApiClient.GetSessionAsync(sessionId);
            ChatStream.Messages.Clear();
            ToolExecutions.Clear();

            foreach (var turn in detail.Turns)
            {
                var msg = new ChatMessageViewModel
                {
                    Id = turn.Id,
                    Role = turn.Role,
                    CreatedAt = turn.CreatedAtUtc
                };
                msg.SetContent(turn.Content);
                if (!string.IsNullOrEmpty(turn.ThinkingContent))
                {
                    msg.SetThinking(turn.ThinkingContent);
                }
                ChatStream.Messages.Add(msg);
            }
        }
        catch
        {
            // Ignore offline fallback
        }
    }

    private void OnApprovalRequired(Guid sessionId, JsonDocument? payload)
    {
        Guid executionId = Guid.NewGuid();
        string toolName = "Sandbox Operation";
        string command = "Bash command or restricted file change";
        string? argsJson = null;

        if (payload is not null && payload.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (payload.RootElement.TryGetProperty("toolExecutionId", out var tid) && tid.TryGetGuid(out var parsedTid)) executionId = parsedTid;
            if (payload.RootElement.TryGetProperty("toolName", out var tn)) toolName = tn.GetString() ?? toolName;
            if (payload.RootElement.TryGetProperty("command", out var cmd)) command = cmd.GetString() ?? command;
            argsJson = payload.RootElement.ToString();
        }

        var approvalVm = new SandboxApprovalViewModel(_approvalsApiClient)
        {
            SessionId = sessionId,
            ToolExecutionId = executionId,
            ToolName = toolName,
            CommandOrTarget = command,
            ArgumentsJson = argsJson
        };

        PendingApproval = approvalVm;
        HasPendingApproval = true;

        if (ShowApprovalDialogRequested is not null)
        {
            _ = ShowApprovalDialogRequested.Invoke(approvalVm).ContinueWith(_ =>
            {
                HasPendingApproval = false;
                PendingApproval = null;
            }, TaskScheduler.Default);
        }
    }

    private void OnToolExecutionUpdated(ToolExecutionItemViewModel item)
    {
        var existing = ToolExecutions.FirstOrDefault(t => t.Id == item.Id);
        if (existing is not null)
        {
            existing.Status = item.Status;
            existing.CompletedAt = item.CompletedAt;
            if (item.Result is not null) existing.Result = item.Result;
        }
        else
        {
            ToolExecutions.Add(item);
        }
    }
}
