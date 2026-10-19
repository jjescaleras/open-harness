using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;
using OpenHarness.Domain.Entities;
using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Application.Features.Sessions;

public sealed class SessionService(
    IServiceScopeFactory scopeFactory,
    IPiRpcSessionFactory piRpcSessionFactory,
    IApprovalGate approvalGate,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    private static readonly ConcurrentDictionary<Guid, ActiveRuntime> ActiveRuntimes = new();

    private sealed class ActiveRuntime(IPiRpcSessionBridge bridge) : IAsyncDisposable
    {
        public IPiRpcSessionBridge Bridge { get; } = bridge;
        private readonly List<Channel<PiSessionEvent>> _subscribers = [];
        private readonly object _lock = new();

        public ChannelReader<PiSessionEvent> Subscribe()
        {
            var channel = Channel.CreateUnbounded<PiSessionEvent>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            lock (_lock)
            {
                _subscribers.Add(channel);
            }

            return channel.Reader;
        }

        public void Unsubscribe(ChannelReader<PiSessionEvent> reader)
        {
            lock (_lock)
            {
                _subscribers.RemoveAll(c => c.Reader == reader);
            }
        }

        public void Broadcast(PiSessionEvent evt)
        {
            lock (_lock)
            {
                foreach (var sub in _subscribers)
                {
                    sub.Writer.TryWrite(evt);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            lock (_lock)
            {
                foreach (var sub in _subscribers)
                {
                    sub.Writer.TryComplete();
                }
                _subscribers.Clear();
            }

            await Bridge.DisposeAsync();
        }
    }

    public async Task<Result<SessionSummaryDto>> CreateSessionAsync(
        Guid userId,
        CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<SessionSummaryDto>.Failure("UserNotFound", "User does not exist.");
        }

        Guid policyId;
        if (request.WorkspacePolicyId.HasValue)
        {
            var policyExists = await db.WorkspacePolicies
                .AnyAsync(p => p.Id == request.WorkspacePolicyId.Value && p.OwnerId == userId, cancellationToken);

            if (!policyExists)
            {
                return Result<SessionSummaryDto>.Failure("PolicyNotFound", "The specified workspace policy does not exist.");
            }

            policyId = request.WorkspacePolicyId.Value;
        }
        else
        {
            var defaultPolicy = await db.WorkspacePolicies
                .FirstOrDefaultAsync(p => p.OwnerId == userId, cancellationToken);

            if (defaultPolicy is null)
            {
                defaultPolicy = new WorkspacePolicy
                {
                    OwnerId = userId,
                    Name = "Default Workspace Policy",
                    RootPath = Directory.GetCurrentDirectory(),
                    SandboxMode = SandboxMode.WorkspaceWrite,
                    ApprovalPolicy = ApprovalPolicy.OnRequest,
                    CreatedAtUtc = timeProvider.GetUtcNow(),
                    UpdatedAtUtc = timeProvider.GetUtcNow()
                };
                db.WorkspacePolicies.Add(defaultPolicy);
                await db.SaveChangesAsync(cancellationToken);
            }

            policyId = defaultPolicy.Id;
        }

        var session = new AgentSession
        {
            UserId = userId,
            WorkspacePolicyId = policyId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New Agent Session" : request.Title.Trim(),
            Status = AgentSessionStatus.Idle,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };

        db.AgentSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return Result<SessionSummaryDto>.Success(new SessionSummaryDto(
            session.Id,
            session.Title,
            session.Status.ToString(),
            session.TotalInputTokens,
            session.TotalOutputTokens,
            session.CreatedAtUtc,
            session.UpdatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<SessionSummaryDto>>> ListSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();

        var sessions = await db.AgentSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Select(s => new SessionSummaryDto(
                s.Id,
                s.Title,
                s.Status.ToString(),
                s.TotalInputTokens,
                s.TotalOutputTokens,
                s.CreatedAtUtc,
                s.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SessionSummaryDto>>.Success(sessions);
    }

    public async Task<Result<SessionDetailDto>> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();

        var session = await db.AgentSessions
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null)
        {
            return Result<SessionDetailDto>.Failure("SessionNotFound", "Session not found.");
        }

        var turns = session.Turns
            .OrderBy(t => t.SequenceNumber)
            .Select(t => new TurnDto(
                t.Id,
                t.SequenceNumber,
                t.Role.ToString(),
                t.Content,
                t.ThinkingContent,
                t.CreatedAtUtc))
            .ToList();

        return Result<SessionDetailDto>.Success(new SessionDetailDto(
            session.Id,
            session.Title,
            session.Status.ToString(),
            session.TotalInputTokens,
            session.TotalOutputTokens,
            session.CreatedAtUtc,
            turns));
    }

    public async Task<Result<bool>> PostPromptAsync(
        Guid userId,
        Guid sessionId,
        PromptSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Result<bool>.Failure("EmptyPrompt", "Prompt message cannot be empty.");
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();

        var session = await db.AgentSessions
            .Include(s => s.WorkspacePolicy)
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session is null)
        {
            return Result<bool>.Failure("SessionNotFound", "Session not found.");
        }

        if (session.Status is AgentSessionStatus.Streaming or AgentSessionStatus.WaitingForApproval)
        {
            return Result<bool>.Failure("SessionBusy", "Session is currently processing another turn or waiting for approval.");
        }

        var nextSeq = (session.Turns.Count > 0 ? session.Turns.Max(t => t.SequenceNumber) : 0) + 1;

        var userTurn = new ConversationTurn
        {
            SessionId = sessionId,
            SequenceNumber = nextSeq,
            Role = ConversationRole.User,
            Content = request.Message.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };

        db.ConversationTurns.Add(userTurn);
        session.Status = AgentSessionStatus.Streaming;
        session.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var runtime = ActiveRuntimes.GetOrAdd(sessionId, id =>
        {
            var bridge = piRpcSessionFactory.CreateBridge(session.WorkspacePolicy.RootPath);
            return new ActiveRuntime(bridge);
        });

        if (!runtime.Bridge.IsRunning)
        {
            await runtime.Bridge.StartAsync(cancellationToken);
            _ = Task.Run(() => RunSessionProcessingLoopAsync(sessionId, userId, nextSeq + 1, runtime));
        }

        await runtime.Bridge.SendPromptAsync(userTurn.Content, cancellationToken);
        return Result<bool>.Success(true);
    }

    public Task<Result<bool>> SubmitApprovalDecisionAsync(
        Guid userId,
        Guid sessionId,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var resolved = approvalGate.TryResolveDecision(request.ToolExecutionId, new ApprovalDecision(
            Approved: request.Approved,
            Reason: request.Reason,
            RememberForSession: request.RememberForSession,
            DecidedBy: userId.ToString()));

        if (!resolved)
        {
            return Task.FromResult(Result<bool>.Failure("ApprovalNotFound", "No active approval request found for the given tool execution ID."));
        }

        return Task.FromResult(Result<bool>.Success(true));
    }

    public async IAsyncEnumerable<PiSessionEvent> StreamSessionEventsAsync(
        Guid userId,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!ActiveRuntimes.TryGetValue(sessionId, out var runtime))
        {
            yield break;
        }

        var reader = runtime.Subscribe();
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var evt))
                {
                    yield return evt;
                    if (evt.Type is "turn.completed" or "error")
                    {
                        yield break;
                    }
                }
            }
        }
        finally
        {
            runtime.Unsubscribe(reader);
        }
    }

    private async Task RunSessionProcessingLoopAsync(
        Guid sessionId,
        Guid userId,
        int assistantTurnSeq,
        ActiveRuntime runtime)
    {
        var assistantContent = new System.Text.StringBuilder();
        var assistantThinking = new System.Text.StringBuilder();
        long inTokens = 0;
        long outTokens = 0;

        try
        {
            await foreach (var evt in runtime.Bridge.ReadEventsAsync())
            {
                runtime.Broadcast(evt);

                switch (evt.Type)
                {
                    case "text.delta" when !string.IsNullOrEmpty(evt.Delta):
                        assistantContent.Append(evt.Delta);
                        break;

                    case "thinking.delta" when !string.IsNullOrEmpty(evt.Delta):
                        assistantThinking.Append(evt.Delta);
                        break;

                    case "sandbox.approval_required" when evt.ToolExecutionId.HasValue:
                        await HandleApprovalRequiredAsync(sessionId, evt, runtime);
                        break;

                    case "turn.completed":
                        inTokens = evt.InputTokens ?? 0;
                        outTokens = evt.OutputTokens ?? 0;
                        break;
                }
            }

            // Consolidate turn in PostgreSQL
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();
            var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session is not null)
            {
                var assistantTurn = new ConversationTurn
                {
                    SessionId = sessionId,
                    SequenceNumber = assistantTurnSeq,
                    Role = ConversationRole.Assistant,
                    Content = assistantContent.ToString(),
                    ThinkingContent = assistantThinking.Length > 0 ? assistantThinking.ToString() : null,
                    TokenUsage = new TurnTokenUsage
                    {
                        InputTokens = (int)inTokens,
                        OutputTokens = (int)outTokens
                    },
                    CreatedAtUtc = timeProvider.GetUtcNow(),
                    UpdatedAtUtc = timeProvider.GetUtcNow()
                };

                db.ConversationTurns.Add(assistantTurn);
                session.Status = AgentSessionStatus.Idle;
                session.TotalInputTokens += inTokens;
                session.TotalOutputTokens += outTokens;
                session.UpdatedAtUtc = timeProvider.GetUtcNow();

                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during agent session {SessionId} execution loop.", sessionId);
            runtime.Broadcast(new PiSessionEvent(Type: "error", Delta: ex.Message));

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();
            var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is not null)
            {
                session.Status = AgentSessionStatus.Failed;
                session.UpdatedAtUtc = timeProvider.GetUtcNow();
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            if (ActiveRuntimes.TryRemove(sessionId, out var r))
            {
                await r.DisposeAsync();
            }
        }
    }

    private async Task HandleApprovalRequiredAsync(Guid sessionId, PiSessionEvent evt, ActiveRuntime runtime)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();
            var session = await db.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is not null)
            {
                session.Status = AgentSessionStatus.WaitingForApproval;
                await db.SaveChangesAsync();
            }

            var decision = await approvalGate.WaitForDecisionAsync(
                evt.ToolExecutionId!.Value,
                sessionId,
                evt.ToolName ?? "sandbox_action",
                evt.Arguments?.ToString() ?? "{}",
                TimeSpan.FromMinutes(5));

            await runtime.Bridge.SendApprovalResponseAsync(evt.ToolExecutionId.Value, decision.Approved);

            using var resumeScope = scopeFactory.CreateScope();
            var resumeDb = resumeScope.ServiceProvider.GetRequiredService<IOpenHarnessDbContext>();
            var resumeSession = await resumeDb.AgentSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (resumeSession is not null)
            {
                resumeSession.Status = AgentSessionStatus.Streaming;
                await resumeDb.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve sandbox approval for session {SessionId}.", sessionId);
        }
    }
}

