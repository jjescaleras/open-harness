using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenHarness.Domain.Entities;
using OpenHarness.Infrastructure.Security;
using Xunit;

namespace OpenHarness.UnitTests.Security;

public class JwtTokenProviderTests
{
    private readonly JwtTokenProvider _provider;

    public JwtTokenProviderTests()
    {
        var dict = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SuperSecretKeyForTestingPurposes1234567890!@#$%^&*()",
            ["Jwt:Issuer"] = "OpenHarnessTest",
            ["Jwt:Audience"] = "OpenHarnessTestAudience"
        };

        var config = new TestConfiguration(dict);
        _provider = new JwtTokenProvider(config);
    }

    private sealed class TestConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.GetValueOrDefault(key);
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

    [Fact]
    public void GenerateAccessToken_ShouldProduceValidTokenWithCorrectClaims()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = "developer@openharness.io",
            Role = "developer"
        };

        // Act
        var token = _provider.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Issuer.Should().Be("OpenHarnessTest");
        jwt.Audiences.Should().Contain("OpenHarnessTestAudience");
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "developer@openharness.io");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == "developer");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduce128CharHexStringFrom64Bytes()
    {
        // Act
        var token1 = _provider.GenerateRefreshToken();
        var token2 = _provider.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrWhiteSpace();
        token1.Length.Should().Be(128); // 64 bytes in hex
        token2.Length.Should().Be(128);
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void HashToken_ShouldProduceDeterministic64CharSha256Hex()
    {
        // Arrange
        var raw = "sample_refresh_token_value";

        // Act
        var hash1 = _provider.HashToken(raw);
        var hash2 = _provider.HashToken(raw);

        // Assert
        hash1.Length.Should().Be(64); // 32 bytes in hex
        hash1.Should().Be(hash2);
    }
}

