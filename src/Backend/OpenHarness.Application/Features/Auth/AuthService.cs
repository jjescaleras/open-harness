using Microsoft.EntityFrameworkCore;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Application.Features.Auth;

public sealed class AuthService(
    IOpenHarnessDbContext dbContext,
    IJwtTokenProvider jwtTokenProvider,
    TimeProvider timeProvider) : IAuthService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            return Result<AuthResponse>.Failure("UserAlreadyExists", "A user with the specified email address already exists.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = string.IsNullOrWhiteSpace(request.Role) ? "user" : request.Role,
            IsActive = true,
            CreatedAtUtc = timeProvider.GetUtcNow(),
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };

        dbContext.Users.Add(user);

        var rawRefreshToken = jwtTokenProvider.GenerateRefreshToken();
        var refreshTokenHash = jwtTokenProvider.HashToken(rawRefreshToken);
        var now = timeProvider.GetUtcNow();
        var familyId = Guid.CreateVersion7();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = familyId,
            ExpiresAtUtc = now.Add(RefreshTokenLifetime),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenProvider.GenerateAccessToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawRefreshToken,
            now.Add(AccessTokenLifetime),
            user.Id,
            user.Email,
            user.Role));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Result<AuthResponse>.Failure("InvalidCredentials", "Invalid email or password.");
        }

        var rawRefreshToken = jwtTokenProvider.GenerateRefreshToken();
        var refreshTokenHash = jwtTokenProvider.HashToken(rawRefreshToken);
        var now = timeProvider.GetUtcNow();
        var familyId = Guid.CreateVersion7();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = familyId,
            ExpiresAtUtc = now.Add(RefreshTokenLifetime),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenProvider.GenerateAccessToken(user);

        return Result<AuthResponse>.Success(new AuthResponse(
            accessToken,
            rawRefreshToken,
            now.Add(AccessTokenLifetime),
            user.Id,
            user.Email,
            user.Role));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<AuthResponse>.Failure("InvalidToken", "Refresh token cannot be empty.");
        }

        var presentedHash = jwtTokenProvider.HashToken(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

        if (token is null)
        {
            return Result<AuthResponse>.Failure("InvalidToken", "The provided refresh token is unrecognized.");
        }

        var now = timeProvider.GetUtcNow();

        // Detection of reuse on revoked/replaced token (Theft protection)
        if (token.RevokedAtUtc != null || token.ReplacedByTokenHash != null)
        {
            await dbContext.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAtUtc, now)
                    .SetProperty(t => t.UpdatedAtUtc, now),
                    cancellationToken);

            return Result<AuthResponse>.Failure("SessionCompromised", "Compromised token reuse detected; all family sessions invalidated.");
        }

        if (token.ExpiresAtUtc <= now)
        {
            return Result<AuthResponse>.Failure("TokenExpired", "The refresh token has expired.");
        }

        if (!token.User.IsActive)
        {
            return Result<AuthResponse>.Failure("UserInactive", "The associated user account is inactive.");
        }

        // Token rotation: mark old as replaced and generate new with the same FamilyId
        var newRawRefreshToken = jwtTokenProvider.GenerateRefreshToken();
        var newHash = jwtTokenProvider.HashToken(newRawRefreshToken);

        token.RevokedAtUtc = now;
        token.ReplacedByTokenHash = newHash;
        token.UpdatedAtUtc = now;

        var newRefreshToken = new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = newHash,
            FamilyId = token.FamilyId,
            ExpiresAtUtc = now.Add(RefreshTokenLifetime),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.RefreshTokens.Add(newRefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var newAccessToken = jwtTokenProvider.GenerateAccessToken(token.User);

        return Result<AuthResponse>.Success(new AuthResponse(
            newAccessToken,
            newRawRefreshToken,
            now.Add(AccessTokenLifetime),
            token.User.Id,
            token.User.Email,
            token.User.Role));
    }

    public async Task<Result<bool>> RevokeTokenAsync(RevokeTokenRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result<bool>.Failure("InvalidToken", "Refresh token cannot be empty.");
        }

        var presentedHash = jwtTokenProvider.HashToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var affected = await dbContext.RefreshTokens
            .Where(t => t.TokenHash == presentedHash && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAtUtc, now)
                .SetProperty(t => t.UpdatedAtUtc, now),
                cancellationToken);

        return Result<bool>.Success(affected > 0);
    }
}

