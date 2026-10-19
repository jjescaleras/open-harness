using OpenHarness.Client.Core.Models;

namespace OpenHarness.Client.Core.Http;

public interface IApprovalsApiClient
{
    Task<HttpResponseMessage> SubmitApprovalAsync(
        Guid sessionId,
        Guid toolExecutionId,
        bool approved,
        string? reason = null,
        bool rememberSession = false,
        CancellationToken cancellationToken = default);

    Task<HttpResponseMessage> SubmitApprovalAsync(
        Guid sessionId,
        ClientApprovalDecisionRequest request,
        CancellationToken cancellationToken = default);
}

