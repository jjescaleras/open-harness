using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Domain.Entities;

namespace OpenHarness.Infrastructure.Security;

public sealed class JwtTokenProvider : IJwtTokenProvider
{
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _credentials;

    public JwtTokenProvider(IConfiguration configuration)
    {
        var secret = configuration["Jwt:SecretKey"]
            ?? configuration["Jwt:Key"]
            ?? "OpenHarnessSuperSecretDevelopmentKey1234567890!@#$%^&*()_+";

        _issuer = configuration["Jwt:Issuer"] ?? "OpenHarness";
        _audience = configuration["Jwt:Audience"] ?? "OpenHarnessClient";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public string GenerateAccessToken(User user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _issuer,
            Audience = _audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                ["role"] = user.Role
            }
        };

        return _tokenHandler.CreateToken(descriptor);
    }

    public string GenerateRefreshToken()
    {
        Span<byte> randomBytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

