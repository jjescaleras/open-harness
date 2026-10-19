using Microsoft.EntityFrameworkCore;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Persistence;

public sealed class OpenHarnessDbContext(DbContextOptions<OpenHarnessDbContext> options) : DbContext(options), IOpenHarnessDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<WorkspacePolicy> WorkspacePolicies => Set<WorkspacePolicy>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
    public DbSet<ConversationTurn> ConversationTurns => Set<ConversationTurn>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();
    public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("harness");
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenHarnessDbContext).Assembly);
    }
}
