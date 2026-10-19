using Microsoft.EntityFrameworkCore;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Application.Common.Interfaces;

public interface IOpenHarnessDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<WorkspacePolicy> WorkspacePolicies { get; }
    DbSet<AgentSession> AgentSessions { get; }
    DbSet<ConversationTurn> ConversationTurns { get; }
    DbSet<ToolExecution> ToolExecutions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

