namespace OpenHarness.Application.Features.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string Role = "user");

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    string RefreshToken);

public sealed record RevokeTokenRequest(
    string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    string Email,
    string Role);

