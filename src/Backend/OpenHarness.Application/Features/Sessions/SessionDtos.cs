namespace OpenHarness.Application.Features.Sessions;

public sealed record CreateSessionRequest(
    string Title,
    Guid? WorkspacePolicyId = null);

public sealed record PromptSessionRequest(
    string Message);

public sealed record ApprovalDecisionRequest(
    Guid ToolExecutionId,
    bool Approved,
    string? Reason = null,
    bool RememberForSession = false);

public sealed record TurnDto(
    Guid Id,
    int SequenceNumber,
    string Role,
    string Content,
    string? ThinkingContent,
    DateTimeOffset CreatedAtUtc);

public sealed record SessionSummaryDto(
    Guid Id,
    string Title,
    string Status,
    long TotalInputTokens,
    long TotalOutputTokens,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record SessionDetailDto(
    Guid Id,
    string Title,
    string Status,
    long TotalInputTokens,
    long TotalOutputTokens,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TurnDto> Turns);

