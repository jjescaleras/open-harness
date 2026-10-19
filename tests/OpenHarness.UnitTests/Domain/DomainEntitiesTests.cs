using FluentAssertions;
using OpenHarness.Domain.Entities;
using Xunit;

namespace OpenHarness.UnitTests.Domain;

public class DomainEntitiesTests
{
    [Fact]
    public void User_ShouldInitializeWithGuidVersion7()
    {
        // Act
        var user = new User
        {
            Email = "dev@openharness.internal",
            PasswordHash = "dummyhash",
            Role = "admin"
        };

        // Assert
        user.Id.Should().NotBeEmpty();
        user.Id.Version.Should().Be(7);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_ShouldInitializeWithGuidVersion7AndFamilyId()
    {
        // Act
        var token = new RefreshToken
        {
            UserId = Guid.CreateVersion7(),
            TokenHash = "abc123hash",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        // Assert
        token.Id.Should().NotBeEmpty();
        token.Id.Version.Should().Be(7);
        token.FamilyId.Should().NotBeEmpty();
        token.FamilyId.Version.Should().Be(7);
    }
}

