using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenHarness.Application.Features.Auth;

namespace OpenHarness.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            [FromServices] IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.RegisterAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            [FromServices] IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        });

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            [FromServices] IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.RefreshTokenAsync(request, ct);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            return result.ErrorCode switch
            {
                "SessionCompromised" => Results.Json(
                    new { error = "SessionCompromised", message = result.ErrorMessage },
                    statusCode: StatusCodes.Status401Unauthorized),
                "TokenExpired" => Results.Json(
                    new { error = "TokenExpired", message = result.ErrorMessage },
                    statusCode: StatusCodes.Status401Unauthorized),
                _ => Results.Unauthorized()
            };
        });

        group.MapPost("/revoke", async (
            [FromBody] RevokeTokenRequest request,
            [FromServices] IAuthService authService,
            CancellationToken ct) =>
        {
            var result = await authService.RevokeTokenAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(new { success = true })
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
            var role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role");

            if (sub is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                userId = sub,
                email,
                role
            });
        }).RequireAuthorization();

        return endpoints;
    }
}

