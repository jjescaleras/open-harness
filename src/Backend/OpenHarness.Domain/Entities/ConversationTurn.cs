using OpenHarness.Domain.Common;
using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;
using Pgvector;

namespace OpenHarness.Domain.Entities;

public sealed class ConversationTurn : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public int SequenceNumber { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ThinkingContent { get; set; }
    public TurnTokenUsage TokenUsage { get; set; } = new();
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public AgentSession Session { get; set; } = null!;
    public ICollection<ToolExecution> ToolExecutions { get; set; } = new List<ToolExecution>();
}
