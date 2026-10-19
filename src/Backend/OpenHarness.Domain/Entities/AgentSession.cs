using OpenHarness.Domain.Common;
using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Domain.Entities;

public sealed class AgentSession : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid WorkspacePolicyId { get; set; }
    public Guid? ParentSessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public AgentSessionStatus Status { get; set; } = AgentSessionStatus.Idle;
    public AgentModelConfig ModelConfig { get; set; } = new();
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public uint RowVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public WorkspacePolicy WorkspacePolicy { get; set; } = null!;
    public AgentSession? ParentSession { get; set; }
    public ICollection<AgentSession> ChildSessions { get; set; } = new List<AgentSession>();
    public ICollection<ConversationTurn> Turns { get; set; } = new List<ConversationTurn>();
    public ICollection<ToolExecution> ToolExecutions { get; set; } = new List<ToolExecution>();
}
