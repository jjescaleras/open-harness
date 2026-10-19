using System.Collections.Concurrent;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;

namespace OpenHarness.Infrastructure.Security;

public sealed class InMemoryApprovalGate : IApprovalGate
{
    private readonly ConcurrentDictionary<Guid, PendingEntry> _pendingApprovals = new();

    private sealed record PendingEntry(
        PendingApproval Metadata,
        TaskCompletionSource<ApprovalDecision> Tcs,
        CancellationTokenRegistration TimeoutRegistration);

    public async Task<ApprovalDecision> WaitForDecisionAsync(
        Guid toolExecutionId,
        Guid sessionId,
        string toolName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<ApprovalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(timeout);

        var metadata = new PendingApproval(
            toolExecutionId,
            sessionId,
            toolName,
            arguments,
            now,
            expiresAt);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var registration = cts.Token.Register(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            else
            {
                tcs.TrySetResult(new ApprovalDecision(
                    Approved: false,
                    Reason: "Approval request timed out waiting for human operator"));
            }
        });

        var entry = new PendingEntry(metadata, tcs, registration);
        _pendingApprovals[toolExecutionId] = entry;

        try
        {
            return await tcs.Task;
        }
        finally
        {
            _pendingApprovals.TryRemove(toolExecutionId, out var removed);
            removed?.TimeoutRegistration.Dispose();
        }
    }

    public bool TryResolveDecision(Guid toolExecutionId, ApprovalDecision decision)
    {
        if (_pendingApprovals.TryGetValue(toolExecutionId, out var entry))
        {
            return entry.Tcs.TrySetResult(decision);
        }

        return false;
    }

    public IReadOnlyCollection<PendingApproval> GetPendingApprovals()
    {
        return _pendingApprovals.Values
            .Select(v => v.Metadata)
            .ToList()
            .AsReadOnly();
    }
}

