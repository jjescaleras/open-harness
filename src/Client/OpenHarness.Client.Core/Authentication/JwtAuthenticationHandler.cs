using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace OpenHarness.Client.Core.Authentication;

public sealed class JwtAuthenticationHandler : DelegatingHandler
{
    private readonly AuthSessionState _sessionState;
    private readonly ITokenStorage _tokenStorage;
    private readonly Uri _baseApiUri;
    private readonly SemaphoreSlim _refreshSemaphore = new(1, 1);

    public JwtAuthenticationHandler(
        AuthSessionState sessionState,
        ITokenStorage tokenStorage,
        Uri baseApiUri)
    {
        _sessionState = sessionState;
        _tokenStorage = tokenStorage;
        _baseApiUri = baseApiUri;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var initialToken = _sessionState.AccessToken;
        if (!string.IsNullOrEmpty(initialToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", initialToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Avoid infinite loop if the 401 was from the refresh endpoint itself
        if (request.RequestUri != null && request.RequestUri.AbsolutePath.Contains("/auth/refresh"))
        {
            return response;
        }

        await _refreshSemaphore.WaitAsync(cancellationToken);
        try
        {
            // If another concurrent thread already refreshed the token, retry immediately
            if (_sessionState.AccessToken != initialToken && !string.IsNullOrEmpty(_sessionState.AccessToken))
            {
                var retriedRequest = await CloneHttpRequestMessageAsync(request);
                retriedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionState.AccessToken);
                return await base.SendAsync(retriedRequest, cancellationToken);
            }

            // Attempt to refresh
            var currentRefreshToken = _sessionState.RefreshToken
                ?? await _tokenStorage.GetRefreshTokenAsync(cancellationToken);

            if (string.IsNullOrEmpty(currentRefreshToken))
            {
                _sessionState.Clear();
                await _tokenStorage.ClearRefreshTokenAsync(cancellationToken);
                return response;
            }

            var refreshed = await RefreshTokensAsync(currentRefreshToken, cancellationToken);
            if (!refreshed)
            {
                _sessionState.Clear();
                await _tokenStorage.ClearRefreshTokenAsync(cancellationToken);
                return response;
            }

            // Retry original request with freshly acquired token
            var finalRequest = await CloneHttpRequestMessageAsync(request);
            finalRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionState.AccessToken);
            return await base.SendAsync(finalRequest, cancellationToken);
        }
        finally
        {
            _refreshSemaphore.Release();
        }
    }

    private async Task<bool> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var refreshClient = new HttpClient { BaseAddress = _baseApiUri };
        var payload = new { RefreshToken = refreshToken };

        var refreshResponse = await refreshClient.PostAsJsonAsync("/api/v1/auth/refresh", payload, cancellationToken);
        if (!refreshResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var content = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (content.TryGetProperty("accessToken", out var atElem) &&
            content.TryGetProperty("refreshToken", out var rtElem))
        {
            var newAccessToken = atElem.GetString() ?? string.Empty;
            var newRefreshToken = rtElem.GetString() ?? string.Empty;
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

            _sessionState.UpdateTokens(newAccessToken, newRefreshToken, expiresAt);
            await _tokenStorage.SaveRefreshTokenAsync(newRefreshToken, cancellationToken);
            return true;
        }

        return false;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        if (req.Content != null)
        {
            var ms = new MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var h in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        foreach (var header in req.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

