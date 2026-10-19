using System.Net.Http.Json;
using System.Text.Json;
using OpenHarness.Client.Core.Authentication;
using OpenHarness.Client.Core.Models;
using OpenHarness.Client.Core.Streaming;

namespace OpenHarness.Client.Core.Http;

public sealed record ClientAuthResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string Email,
    string Role);

public sealed record ClientUserResult(
    string UserId,
    string Email,
    string Role);

public sealed class HarnessApiClient : IAuthApiClient, ISessionsApiClient, IApprovalsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AuthSessionState _sessionState;
    private readonly ITokenStorage _tokenStorage;
    private readonly HarnessSseClient _sseClient;

    public HarnessApiClient(
        HttpClient httpClient,
        AuthSessionState sessionState,
        ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _sessionState = sessionState;
        _tokenStorage = tokenStorage;
        _sseClient = new HarnessSseClient(httpClient);
    }

    public async Task<ClientAuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClientAuthResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty auth response received.");

        _sessionState.UpdateTokens(result.AccessToken, result.RefreshToken, result.ExpiresAtUtc, result.UserId, result.Email, result.Role);
        await _tokenStorage.SaveRefreshTokenAsync(result.RefreshToken, cancellationToken);

        return result;
    }

    public async Task<ClientAuthResult> RegisterAsync(string email, string password, string role = "user", CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/register", new { Email = email, Password = password, Role = role }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClientAuthResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty auth response received.");

        _sessionState.UpdateTokens(result.AccessToken, result.RefreshToken, result.ExpiresAtUtc, result.UserId, result.Email, result.Role);
        await _tokenStorage.SaveRefreshTokenAsync(result.RefreshToken, cancellationToken);

        return result;
    }

    public async Task<ClientUserResult?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/auth/me", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ClientUserResult>(JsonOptions, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var token = _sessionState.RefreshToken;
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                await _httpClient.PostAsJsonAsync("/api/v1/auth/revoke", new { RefreshToken = token }, cancellationToken);
            }
            catch
            {
                // Ignore network errors on logout
            }
        }

        _sessionState.Clear();
        await _tokenStorage.ClearRefreshTokenAsync(cancellationToken);
    }

    public async Task<ClientSessionSummary> CreateSessionAsync(ClientCreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/sessions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ClientSessionSummary>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty response received when creating session.");
    }

    public async Task<IReadOnlyList<ClientSessionSummary>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/sessions", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ClientSessionSummary>>(JsonOptions, cancellationToken)
            ?? [];
    }

    public async Task<ClientSessionDetail> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/sessions/{sessionId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ClientSessionDetail>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Empty session detail response for {sessionId}.");
    }

    public async Task<bool> PostPromptAsync(Guid sessionId, ClientPromptSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/prompt", request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public IAsyncEnumerable<HarnessStreamEvent> StreamSessionEventsAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return _sseClient.StreamSessionEventsAsync(sessionId, cancellationToken);
    }

    public async Task<HttpResponseMessage> SubmitApprovalAsync(
        Guid sessionId,
        Guid toolExecutionId,
        bool approved,
        string? reason = null,
        bool rememberSession = false,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            ToolExecutionId = toolExecutionId,
            Approved = approved,
            Reason = reason,
            RememberForSession = rememberSession
        };

        return await _httpClient.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/approvals", payload, cancellationToken);
    }

    public Task<HttpResponseMessage> SubmitApprovalAsync(
        Guid sessionId,
        ClientApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SubmitApprovalAsync(
            sessionId,
            request.ToolExecutionId,
            request.Approved,
            request.Reason,
            request.RememberForSession,
            cancellationToken);
    }
}
