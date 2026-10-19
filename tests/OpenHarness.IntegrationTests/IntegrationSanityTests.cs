using FluentAssertions;
using Xunit;

namespace OpenHarness.IntegrationTests;

public class IntegrationSanityTests
{
    [Fact]
    public void TestEnvironment_ShouldBeFunctional()
    {
        true.Should().BeTrue();
    }
}

