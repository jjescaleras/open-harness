using System.Text.Json;

namespace OpenHarness.Application.Common.Interfaces;

public sealed record PiSessionEvent(
    string Type,
    string? Delta = null,
    string? ToolName = null,
    Guid? ToolExecutionId = null,
    JsonElement? Arguments = null,
    JsonElement? Result = null,
    bool RequiresApproval = false,
    long? InputTokens = null,
    long? OutputTokens = null);

public interface IPiRpcSessionBridge : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
    Task SendApprovalResponseAsync(Guid toolExecutionId, bool confirmed, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PiSessionEvent> ReadEventsAsync(CancellationToken cancellationToken = default);
}

