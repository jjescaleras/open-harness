namespace OpenHarness.Client.Core.Authentication;

public interface ITokenStorage
{
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);
    Task SaveRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task ClearRefreshTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthSessionState
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public event Action? StateChanged;

    public void UpdateTokens(string accessToken, string refreshToken, DateTimeOffset expiresAtUtc, Guid? userId = null, string? email = null, string? role = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
        if (userId.HasValue) UserId = userId.Value;
        if (email != null) Email = email;
        if (role != null) Role = role;

        StateChanged?.Invoke();
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        ExpiresAtUtc = null;
        UserId = null;
        Email = null;
        Role = null;

        StateChanged?.Invoke();
    }
}

