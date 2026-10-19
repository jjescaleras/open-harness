using OpenHarness.Domain.Common;

namespace OpenHarness.Domain.Entities;

public sealed class User : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<WorkspacePolicy> WorkspacePolicies { get; set; } = new List<WorkspacePolicy>();
    public ICollection<AgentSession> AgentSessions { get; set; } = new List<AgentSession>();
}
