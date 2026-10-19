namespace OpenHarness.Client.Core.Models;

public sealed record ClientSessionSummary(
    Guid Id,
    string Title,
    string Status,
    long TotalInputTokens,
    long TotalOutputTokens,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClientTurnDto(
    Guid Id,
    int SequenceNumber,
    string Role,
    string Content,
    string? ThinkingContent,
    DateTimeOffset CreatedAtUtc);

public sealed record ClientSessionDetail(
    Guid Id,
    string Title,
    string Status,
    long TotalInputTokens,
    long TotalOutputTokens,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ClientTurnDto> Turns);

public sealed record ClientCreateSessionRequest(
    string Title,
    Guid? WorkspacePolicyId = null);

public sealed record ClientPromptSessionRequest(
    string Message);

public sealed record ClientApprovalDecisionRequest(
    Guid ToolExecutionId,
    bool Approved,
    string? Reason = null,
    bool RememberForSession = false);

