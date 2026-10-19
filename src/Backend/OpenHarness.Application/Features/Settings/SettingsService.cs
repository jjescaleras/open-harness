using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;
using OpenHarness.Domain.Entities;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Application.Features.Settings;

public sealed class SettingsService(
    IOpenHarnessDbContext dbContext,
    TimeProvider timeProvider) : ISettingsService
{
    public async Task<GeneralSettingsResponse> GetGeneralAsync(CancellationToken cancellationToken = default)
    {
        var stored = await dbContext.GlobalSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Scope == GlobalSettings.DefaultScope, cancellationToken);

        // Nothing persisted yet: serve the defaults without writing a row on read.
        return stored is null
            ? new GeneralSettingsResponse(GeneralSettingsMapper.ToDto(new GeneralSettings()), 0, timeProvider.GetUtcNow())
            : ToResponse(stored);
    }

    public async Task<Result<GeneralSettingsResponse>> UpdateGeneralAsync(
        UpdateGeneralSettingsRequest request,
        Guid updatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (!GeneralSettingsMapper.TryToDomain(request.Settings, out var general, out var errors))
        {
            return Result<GeneralSettingsResponse>.Failure(
                "InvalidSettings",
                $"Invalid values for: {string.Join(", ", errors)}.");
        }

        var stored = await dbContext.GlobalSettings
            .FirstOrDefaultAsync(s => s.Scope == GlobalSettings.DefaultScope, cancellationToken);

        var currentVersion = stored?.RowVersion ?? 0;
        if (request.ExpectedVersion is { } expected && expected != currentVersion)
        {
            return Result<GeneralSettingsResponse>.Failure(
                "SettingsConflict",
                "The settings were modified by someone else. Reload them and try again.");
        }

        var now = timeProvider.GetUtcNow();
        if (stored is null)
        {
            stored = new GlobalSettings { CreatedAtUtc = now };
            dbContext.GlobalSettings.Add(stored);
        }

        stored.General = general;
        stored.UpdatedByUserId = updatedByUserId;
        stored.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsUniqueViolation(ex))
        {
            // Another administrator saved (or created the row) between our read and write.
            return Result<GeneralSettingsResponse>.Failure(
                "SettingsConflict",
                "The settings were modified by someone else. Reload them and try again.");
        }

        return Result<GeneralSettingsResponse>.Success(ToResponse(stored));
    }

    private static GeneralSettingsResponse ToResponse(GlobalSettings s) =>
        new(GeneralSettingsMapper.ToDto(s.General), s.RowVersion, s.UpdatedAtUtc);

    // PostgreSQL unique_violation; checked by SQLSTATE so Application stays free of Npgsql.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is DbException { SqlState: "23505" };
}
