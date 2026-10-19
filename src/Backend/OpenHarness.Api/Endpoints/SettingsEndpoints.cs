using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenHarness.Application.Features.Settings;

namespace OpenHarness.Api.Endpoints;

public static class SettingsEndpoints
{
    public const string AdminOnlyPolicy = "AdminOnly";

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/settings")
            .WithTags("Settings")
            .RequireAuthorization();

        // Any authenticated user can read: clients need these values to render the UI.
        group.MapGet("/general", async (
            [FromServices] ISettingsService settingsService,
            CancellationToken ct) => Results.Ok(await settingsService.GetGeneralAsync(ct)));

        group.MapPut("/general", async (
            [FromBody] UpdateGeneralSettingsRequest request,
            ClaimsPrincipal user,
            [FromServices] ISettingsService settingsService,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue("sub"), out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await settingsService.UpdateGeneralAsync(request, userId, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.ErrorCode switch
            {
                "SettingsConflict" => Results.Conflict(new { error = result.ErrorCode, message = result.ErrorMessage }),
                _ => Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage })
            };
        }).RequireAuthorization(AdminOnlyPolicy);

        return endpoints;
    }
}
