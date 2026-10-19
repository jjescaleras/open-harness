using OpenHarness.Application.Common.Models;

namespace OpenHarness.Application.Common.Interfaces;

public interface IApprovalGate
{
    Task<ApprovalDecision> WaitForDecisionAsync(
        Guid toolExecutionId,
        Guid sessionId,
        string toolName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    bool TryResolveDecision(Guid toolExecutionId, ApprovalDecision decision);

    IReadOnlyCollection<PendingApproval> GetPendingApprovals();
}

