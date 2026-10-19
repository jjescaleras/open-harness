using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Features.Settings;
using OpenHarness.Domain.Entities;
using OpenHarness.Domain.ValueObjects;
using OpenHarness.Infrastructure.Persistence.Configurations;
using Xunit;

namespace OpenHarness.UnitTests.Settings;

public class SettingsServiceTests
{
    /// <summary>
    /// Uses the production entity configurations; the InMemory provider cannot map pgvector's
    /// <c>Vector</c>, so the embedding column is the only thing left out.
    /// </summary>
    private sealed class SettingsTestDbContext(DbContextOptions<SettingsTestDbContext> options)
        : DbContext(options), IOpenHarnessDbContext
    {
        public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();

        DbSet<User> IOpenHarnessDbContext.Users => Set<User>();
        DbSet<RefreshToken> IOpenHarnessDbContext.RefreshTokens => Set<RefreshToken>();
        DbSet<WorkspacePolicy> IOpenHarnessDbContext.WorkspacePolicies => Set<WorkspacePolicy>();
        DbSet<AgentSession> IOpenHarnessDbContext.AgentSessions => Set<AgentSession>();
        DbSet<ConversationTurn> IOpenHarnessDbContext.ConversationTurns => Set<ConversationTurn>();
        DbSet<ToolExecution> IOpenHarnessDbContext.ToolExecutions => Set<ToolExecution>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(GlobalSettingsConfiguration).Assembly);
            modelBuilder.Entity<ConversationTurn>().Ignore(t => t.Embedding);
        }
    }

    private static SettingsTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SettingsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GeneralSettingsDto Defaults() => GeneralSettingsMapper.ToDto(new GeneralSettings());

    [Fact]
    public async Task GetGeneralAsync_WithoutStoredRow_ReturnsDefaultsWithoutWriting()
    {
        await using var db = CreateContext();
        var service = new SettingsService(db, TimeProvider.System);

        var response = await service.GetGeneralAsync();

        response.Settings.Should().Be(Defaults());
        response.Version.Should().Be(0);
        (await db.GlobalSettings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateGeneralAsync_CreatesSingleGlobalRowAndRecordsAuthor()
    {
        await using var db = CreateContext();
        var service = new SettingsService(db, TimeProvider.System);
        var adminId = Guid.CreateVersion7();
        var changed = Defaults() with { Appearance = "Dark", ConversationFontSizePx = 18 };

        var result = await service.UpdateGeneralAsync(new UpdateGeneralSettingsRequest(changed, 0), adminId);
        await service.UpdateGeneralAsync(new UpdateGeneralSettingsRequest(changed with { ShowCodingView = false }, null), adminId);

        result.IsSuccess.Should().BeTrue();
        var rows = await db.GlobalSettings.AsNoTracking().ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Scope.Should().Be(GlobalSettings.DefaultScope);
        rows[0].UpdatedByUserId.Should().Be(adminId);
        (await service.GetGeneralAsync()).Settings.Should().Be(changed with { ShowCodingView = false });
    }

    [Fact]
    public async Task UpdateGeneralAsync_WithStaleExpectedVersion_ReturnsConflict()
    {
        await using var db = CreateContext();
        db.GlobalSettings.Add(new GlobalSettings { RowVersion = 7 });
        await db.SaveChangesAsync();
        var service = new SettingsService(db, TimeProvider.System);

        var result = await service.UpdateGeneralAsync(new UpdateGeneralSettingsRequest(Defaults(), 3), Guid.CreateVersion7());

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SettingsConflict");
    }

    [Fact]
    public async Task UpdateGeneralAsync_WithInvalidValues_ReturnsInvalidSettingsAndDoesNotWrite()
    {
        await using var db = CreateContext();
        var service = new SettingsService(db, TimeProvider.System);

        var result = await service.UpdateGeneralAsync(
            new UpdateGeneralSettingsRequest(Defaults() with { WorkDetails = "Everything" }, 0),
            Guid.CreateVersion7());

        result.ErrorCode.Should().Be("InvalidSettings");
        result.ErrorMessage.Should().Contain(nameof(GeneralSettingsDto.WorkDetails));
        (await db.GlobalSettings.CountAsync()).Should().Be(0);
    }
}
