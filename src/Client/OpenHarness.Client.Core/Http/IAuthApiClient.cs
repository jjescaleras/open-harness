namespace OpenHarness.Client.Core.Http;

public interface IAuthApiClient
{
    Task<ClientAuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<ClientAuthResult> RegisterAsync(string email, string password, string role = "user", CancellationToken cancellationToken = default);
    Task<ClientUserResult?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

