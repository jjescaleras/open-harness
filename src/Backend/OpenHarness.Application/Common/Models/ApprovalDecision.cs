namespace OpenHarness.Application.Common.Models;

public sealed record ApprovalDecision(
    bool Approved,
    string? Reason = null,
    bool RememberForSession = false,
    string? DecidedBy = null);

public sealed record PendingApproval(
    Guid ToolExecutionId,
    Guid SessionId,
    string ToolName,
    string Arguments,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset ExpiresAtUtc);

