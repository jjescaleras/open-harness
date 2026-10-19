using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Features.Sessions;

namespace OpenHarness.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/sessions")
            .WithTags("Sessions")
            .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateSessionRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await sessionService.CreateSessionAsync(userId.Value, request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/sessions/{result.Value!.Id}", result.Value)
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await sessionService.ListSessionsAsync(userId.Value, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await sessionService.GetSessionAsync(userId.Value, id, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapPost("/{id:guid}/prompt", async (
            Guid id,
            [FromBody] PromptSessionRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await sessionService.PostPromptAsync(userId.Value, id, request, ct);
            return result.IsSuccess
                ? Results.Accepted($"/api/v1/sessions/{id}/stream", new { status = "processing", sessionId = id })
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapPost("/{id:guid}/approvals", async (
            Guid id,
            [FromBody] ApprovalDecisionRequest request,
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var result = await sessionService.SubmitApprovalDecisionAsync(userId.Value, id, request, ct);
            return result.IsSuccess
                ? Results.Ok(new { success = true, toolExecutionId = request.ToolExecutionId })
                : Results.BadRequest(new { error = result.ErrorCode, message = result.ErrorMessage });
        });

        group.MapGet("/{id:guid}/stream", (
            Guid id,
            ClaimsPrincipal principal,
            [FromServices] ISessionService sessionService,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var eventStream = StreamEventsAsSse(sessionService, userId.Value, id, ct);
            return TypedResults.ServerSentEvents(eventStream);
        });

        return endpoints;
    }

    private static async IAsyncEnumerable<SseItem<string>> StreamEventsAsSse(
        ISessionService sessionService,
        Guid userId,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var evt in sessionService.StreamSessionEventsAsync(userId, sessionId, ct))
        {
            var json = JsonSerializer.Serialize(evt);
            yield return new SseItem<string>(json, evt.Type);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

