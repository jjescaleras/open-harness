using OpenHarness.Domain.Common;
using OpenHarness.Domain.Enums;

namespace OpenHarness.Domain.Entities;

public sealed class ToolExecution : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public Guid TurnId { get; set; }
    public string CallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
    public string? Result { get; set; }
    public ToolExecutionStatus Status { get; set; } = ToolExecutionStatus.PendingApproval;
    public bool RequiresApproval { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public long? DurationMs { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AgentSession Session { get; set; } = null!;
    public ConversationTurn Turn { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
