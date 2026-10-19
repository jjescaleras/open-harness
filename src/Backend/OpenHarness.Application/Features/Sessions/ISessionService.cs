using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;

namespace OpenHarness.Application.Features.Sessions;

public interface ISessionService
{
    Task<Result<SessionSummaryDto>> CreateSessionAsync(Guid userId, CreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SessionSummaryDto>>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<SessionDetailDto>> GetSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<Result<bool>> PostPromptAsync(Guid userId, Guid sessionId, PromptSessionRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> SubmitApprovalDecisionAsync(Guid userId, Guid sessionId, ApprovalDecisionRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PiSessionEvent> StreamSessionEventsAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
}

