using FluentAssertions;
using OpenHarness.Client.Core.Authentication;
using Xunit;

namespace OpenHarness.UnitTests.Auth;

public class PlatformSecureTokenStorageTests : IDisposable
{
    private readonly string _tempFile;
    private readonly PlatformSecureTokenStorage _storage;

    public PlatformSecureTokenStorageTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"openharness_test_token_{Guid.NewGuid():N}.bin");
        _storage = new PlatformSecureTokenStorage(_tempFile);
    }

    [Fact]
    public async Task SaveAndGetRefreshToken_ShouldRoundTripEncryptedToken()
    {
        // Arrange
        var testToken = "secure_refresh_token_64_bytes_test_value_123456789";

        // Act
        await _storage.SaveRefreshTokenAsync(testToken);
        var retrieved = await _storage.GetRefreshTokenAsync();

        // Assert
        retrieved.Should().Be(testToken);
    }

    [Fact]
    public async Task ClearRefreshToken_ShouldRemoveToken()
    {
        // Arrange
        await _storage.SaveRefreshTokenAsync("temporary_token");

        // Act
        await _storage.ClearRefreshTokenAsync();
        var retrieved = await _storage.GetRefreshTokenAsync();

        // Assert
        retrieved.Should().BeNull();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }
}

