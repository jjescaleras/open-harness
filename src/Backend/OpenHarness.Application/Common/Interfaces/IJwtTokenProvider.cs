using OpenHarness.Domain.Entities;

namespace OpenHarness.Application.Common.Interfaces;

public interface IJwtTokenProvider
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string token);
}

