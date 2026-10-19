using OpenHarness.Client.Core.Models;
using OpenHarness.Client.Core.Streaming;

namespace OpenHarness.Client.Core.Http;

public interface ISessionsApiClient
{
    Task<ClientSessionSummary> CreateSessionAsync(ClientCreateSessionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default);
    Task<ClientSessionDetail> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<bool> PostPromptAsync(Guid sessionId, ClientPromptSessionRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<HarnessStreamEvent> StreamSessionEventsAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

