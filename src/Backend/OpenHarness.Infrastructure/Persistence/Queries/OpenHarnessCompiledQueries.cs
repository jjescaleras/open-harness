using Microsoft.EntityFrameworkCore;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence.Queries;

public static class OpenHarnessCompiledQueries
{
    public static readonly Func<OpenHarnessDbContext, string, CancellationToken, Task<User?>> GetUserByEmailAsync =
        EF.CompileAsyncQuery((OpenHarnessDbContext db, string email, CancellationToken ct) =>
            db.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Email == email));

    public static readonly Func<OpenHarnessDbContext, string, CancellationToken, Task<RefreshToken?>> GetRefreshTokenByHashAsync =
        EF.CompileAsyncQuery((OpenHarnessDbContext db, string tokenHash, CancellationToken ct) =>
            db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefault(rt => rt.TokenHash == tokenHash));

    public static readonly Func<OpenHarnessDbContext, Guid, CancellationToken, Task<AgentSession?>> GetSessionWithWorkspaceByIdAsync =
        EF.CompileAsyncQuery((OpenHarnessDbContext db, Guid sessionId, CancellationToken ct) =>
            db.AgentSessions
                .Include(s => s.WorkspacePolicy)
                .FirstOrDefault(s => s.Id == sessionId));

    public static readonly Func<OpenHarnessDbContext, Guid, IAsyncEnumerable<ConversationTurn>> GetSessionTurnsOrderedAsync =
        EF.CompileAsyncQuery<OpenHarnessDbContext, Guid, ConversationTurn>((db, sessionId) =>
            db.ConversationTurns
                .AsNoTracking()
                .Where(t => t.SessionId == sessionId)
                .OrderBy(t => t.SequenceNumber));
}
