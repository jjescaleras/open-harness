using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenHarness.Client.Core.Http;
using OpenHarness.Client.Core.Models;
using OpenHarness.Client.Core.Streaming;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class ChatStreamViewModel : ViewModelBase, IAsyncDisposable
{
    private static readonly TimeSpan FrameCadence = TimeSpan.FromMilliseconds(16); // ~60 FPS

    private readonly ISessionsApiClient? _sessionsApiClient;
    private readonly ConcurrentQueue<string> _textDeltaQueue = new();
    private readonly ConcurrentQueue<string> _thinkingDeltaQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [ObservableProperty]
    private string _currentPrompt = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private Guid? _currentSessionId;

    [ObservableProperty]
    private ChatMessageViewModel? _currentStreamingMessage;

    public event Action<Guid, JsonDocument?>? ApprovalRequired;
    public event Action<ToolExecutionItemViewModel>? ToolExecutionUpdated;

    public ChatStreamViewModel(ISessionsApiClient? sessionsApiClient = null)
    {
        _sessionsApiClient = sessionsApiClient;
        _drainTask = Task.Run(DrainLoopAsync);
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    public async Task SendMessageAsync()
    {
        var prompt = CurrentPrompt?.Trim();
        if (string.IsNullOrEmpty(prompt) || CurrentSessionId is null || _sessionsApiClient is null)
        {
            return;
        }

        // Add user message
        var userMessage = new ChatMessageViewModel
        {
            Role = "user",
            CreatedAt = DateTimeOffset.UtcNow
        };
        userMessage.SetContent(prompt);
        Messages.Add(userMessage);

        CurrentPrompt = string.Empty;
        IsStreaming = true;
        StatusMessage = "Agent is thinking...";

        // Add placeholder assistant message
        var assistantMessage = new ChatMessageViewModel
        {
            Role = "assistant",
            IsStreaming = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Messages.Add(assistantMessage);
        CurrentStreamingMessage = assistantMessage;

        try
        {
            var accepted = await _sessionsApiClient.PostPromptAsync(
                CurrentSessionId.Value,
                new ClientPromptSessionRequest(prompt),
                _cts.Token);

            if (!accepted)
            {
                assistantMessage.SetContent("Error: The server could not process the prompt.");
                assistantMessage.IsStreaming = false;
                IsStreaming = false;
                StatusMessage = "Error sending prompt";
                return;
            }

            // Stream SSE events
            await foreach (var evt in _sessionsApiClient.StreamSessionEventsAsync(CurrentSessionId.Value, _cts.Token))
            {
                HandleStreamEvent(evt);
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Streaming canceled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Streaming error: {ex.Message}";
            assistantMessage.AppendContent($"\n[Error: {ex.Message}]");
        }
        finally
        {
            FlushBufferImmediately();
            assistantMessage.IsStreaming = false;
            IsStreaming = false;
            CurrentStreamingMessage = null;
            StatusMessage = "Ready";
        }
    }

    private bool CanSendMessage() => !IsStreaming && !string.IsNullOrWhiteSpace(CurrentPrompt) && CurrentSessionId is not null;

    partial void OnCurrentPromptChanged(string value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnIsStreamingChanged(bool value) => SendMessageCommand.NotifyCanExecuteChanged();
    partial void OnCurrentSessionIdChanged(Guid? value) => SendMessageCommand.NotifyCanExecuteChanged();

    public void EnqueueTextDelta(string delta)
    {
        if (!string.IsNullOrEmpty(delta))
        {
            _textDeltaQueue.Enqueue(delta);
        }
    }

    public void EnqueueThinkingDelta(string delta)
    {
        if (!string.IsNullOrEmpty(delta))
        {
            _thinkingDeltaQueue.Enqueue(delta);
        }
    }

    public void HandleStreamEvent(HarnessStreamEvent evt)
    {
        switch (evt.EventType)
        {
            case "text.delta":
                var textDelta = ExtractDeltaString(evt);
                EnqueueTextDelta(textDelta);
                break;

            case "thinking.delta":
                var thinkingDelta = ExtractDeltaString(evt);
                EnqueueThinkingDelta(thinkingDelta);
                break;

            case "tool.started":
                HandleToolStarted(evt);
                break;

            case "tool.completed":
                HandleToolCompleted(evt);
                break;

            case "sandbox.approval_required":
                HandleApprovalRequired(evt);
                break;

            case "turn.completed":
                FlushBufferImmediately();
                if (CurrentStreamingMessage is not null)
                {
                    CurrentStreamingMessage.IsStreaming = false;
                }
                break;
        }
    }

    private static string ExtractDeltaString(HarnessStreamEvent evt)
    {
        if (evt.ParsedData is not null && evt.ParsedData.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (evt.ParsedData.RootElement.TryGetProperty("delta", out var deltaProp))
            {
                return deltaProp.GetString() ?? string.Empty;
            }
            if (evt.ParsedData.RootElement.TryGetProperty("text", out var textProp))
            {
                return textProp.GetString() ?? string.Empty;
            }
        }
        return evt.RawData;
    }

    private void HandleToolStarted(HarnessStreamEvent evt)
    {
        string toolName = "tool";
        Guid? executionId = null;

        if (evt.ParsedData is not null && evt.ParsedData.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (evt.ParsedData.RootElement.TryGetProperty("toolName", out var tn)) toolName = tn.GetString() ?? toolName;
            if (evt.ParsedData.RootElement.TryGetProperty("executionId", out var eid) && eid.TryGetGuid(out var parsedEid)) executionId = parsedEid;
        }

        var toolMsg = new ChatMessageViewModel
        {
            Role = "tool",
            ToolName = toolName,
            ToolExecutionId = executionId,
            ToolStatus = "Running",
            Content = $"Executing tool: {toolName}...",
            CreatedAt = DateTimeOffset.UtcNow
        };

        PostToUi(() => Messages.Add(toolMsg));

        ToolExecutionUpdated?.Invoke(new ToolExecutionItemViewModel
        {
            Id = executionId ?? Guid.NewGuid(),
            ToolName = toolName,
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow,
            Arguments = evt.RawData
        });
    }

    private void HandleToolCompleted(HarnessStreamEvent evt)
    {
        string? result = null;
        Guid? executionId = null;

        if (evt.ParsedData is not null && evt.ParsedData.RootElement.ValueKind == JsonValueKind.Object)
        {
            if (evt.ParsedData.RootElement.TryGetProperty("executionId", out var eid) && eid.TryGetGuid(out var parsedEid)) executionId = parsedEid;
            if (evt.ParsedData.RootElement.TryGetProperty("result", out var res)) result = res.ToString();
        }

        PostToUi(() =>
        {
            var existing = Messages.LastOrDefault(m => m.IsTool && m.ToolExecutionId == executionId);
            if (existing is not null)
            {
                existing.ToolStatus = "Completed";
                if (!string.IsNullOrEmpty(result))
                {
                    existing.SetContent($"Tool completed:\n{result}");
                }
            }
        });

        if (executionId is not null)
        {
            ToolExecutionUpdated?.Invoke(new ToolExecutionItemViewModel
            {
                Id = executionId.Value,
                ToolName = "tool",
                Status = "Completed",
                CompletedAt = DateTimeOffset.UtcNow,
                Result = result
            });
        }
    }

    private void HandleApprovalRequired(HarnessStreamEvent evt)
    {
        if (CurrentSessionId is not null)
        {
            ApprovalRequired?.Invoke(CurrentSessionId.Value, evt.ParsedData);
        }
    }

    private async Task DrainLoopAsync()
    {
        using var timer = new PeriodicTimer(FrameCadence);
        try
        {
            while (!_cts.IsCancellationRequested && await timer.WaitForNextTickAsync(_cts.Token))
            {
                DrainQueues();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            DrainQueues();
        }
    }

    private void DrainQueues()
    {
        string? textBatch = null;
        if (!_textDeltaQueue.IsEmpty)
        {
            var sb = new StringBuilder();
            while (_textDeltaQueue.TryDequeue(out var chunk))
            {
                sb.Append(chunk);
            }
            textBatch = sb.ToString();
        }

        string? thinkingBatch = null;
        if (!_thinkingDeltaQueue.IsEmpty)
        {
            var sb = new StringBuilder();
            while (_thinkingDeltaQueue.TryDequeue(out var chunk))
            {
                sb.Append(chunk);
            }
            thinkingBatch = sb.ToString();
        }

        if (textBatch is not null || thinkingBatch is not null)
        {
            PostToUi(() =>
            {
                var target = CurrentStreamingMessage ?? Messages.LastOrDefault(m => m.IsAssistant && m.IsStreaming);
                if (target is not null)
                {
                    if (thinkingBatch is not null)
                    {
                        target.AppendThinking(thinkingBatch);
                    }
                    if (textBatch is not null)
                    {
                        target.AppendContent(textBatch);
                    }
                }
            });
        }
    }

    public void FlushBufferImmediately()
    {
        DrainQueues();
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
                Dispatcher.UIThread.Post(action, DispatcherPriority.Render);
            }
        }
        catch
        {
            // Fallback for headless unit testing environment
            action();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _drainTask;
        }
        catch
        {
            // Ignore cancellation on dispose
        }
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

