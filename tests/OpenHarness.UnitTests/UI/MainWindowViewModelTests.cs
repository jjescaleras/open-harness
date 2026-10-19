using FluentAssertions;
using OpenHarness.UI.Avalonia.Services;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class MainWindowViewModelTests
{
    private sealed class FakeChatWindowService : IChatWindowService
    {
        public int OpenCount { get; private set; }

        public void OpenChat() => OpenCount++;
    }

    [Fact]
    public void Constructor_LoadsSampleDashboardData()
    {
        // Act
        var vm = new MainWindowViewModel();

        // Assert
        vm.IsSampleData.Should().BeTrue();
        vm.Kpis.Should().HaveCount(4);
        vm.Kpis.Select(k => k.Title).Should().Contain(["Active Sessions", "Pending Approvals"]);
        vm.RecentSessions.Should().NotBeEmpty();
    }

    [Fact]
    public void OpenChatCommand_DelegatesToChatWindowService()
    {
        // Arrange
        var service = new FakeChatWindowService();
        var vm = new MainWindowViewModel(service);

        // Act
        vm.OpenChatCommand.Execute(null);

        // Assert
        service.OpenCount.Should().Be(1);
    }

    [Fact]
    public void OpenChatCommand_WithoutService_DoesNotThrow()
    {
        // Arrange
        var vm = new MainWindowViewModel();

        // Act
        var act = () => vm.OpenChatCommand.Execute(null);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(KpiKind.Neutral, "#6366F1")]
    [InlineData(KpiKind.Success, "#10B981")]
    [InlineData(KpiKind.Warning, "#F59E0B")]
    [InlineData(KpiKind.Danger, "#EF4444")]
    public void DashboardKpi_AccentColor_MatchesArmoniaTokens(KpiKind kind, string expected)
    {
        var kpi = new DashboardKpiViewModel { Kind = kind };

        kpi.AccentColor.Should().Be(expected);
    }
}
