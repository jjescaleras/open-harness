using OpenHarness.Domain.Common;
using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Domain.Entities;

public sealed class WorkspacePolicy : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public SandboxMode SandboxMode { get; set; } = SandboxMode.WorkspaceWrite;
    public ApprovalPolicy ApprovalPolicy { get; set; } = ApprovalPolicy.OnRequest;
    public WorkspaceRulesConfig RulesConfig { get; set; } = new();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public User Owner { get; set; } = null!;
    public ICollection<AgentSession> AgentSessions { get; set; } = new List<AgentSession>();
}
