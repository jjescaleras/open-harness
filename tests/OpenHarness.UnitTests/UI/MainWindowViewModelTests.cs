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

    [Fact]
    public void Panels_DefaultToMenuVisibleAndAuxHidden()
    {
        var vm = new MainWindowViewModel();

        vm.IsMenuPanelVisible.Should().BeTrue();
        vm.IsAuxPanelVisible.Should().BeFalse();
    }

    [Fact]
    public void TogglePanelCommands_FlipVisibility()
    {
        // Arrange
        var vm = new MainWindowViewModel();

        // Act
        vm.ToggleMenuPanelCommand.Execute(null);
        vm.ToggleAuxPanelCommand.Execute(null);

        // Assert
        vm.IsMenuPanelVisible.Should().BeFalse();
        vm.IsAuxPanelVisible.Should().BeTrue();
    }

    [Fact]
    public void Menu_StartsWithDashboardSelected()
    {
        var vm = new MainWindowViewModel();

        vm.MenuItems.Select(m => m.Target).Should().Equal(
            NavigationTarget.Dashboard, NavigationTarget.Chat, NavigationTarget.Sessions, NavigationTarget.Settings);
        vm.SelectedMenuItem!.Target.Should().Be(NavigationTarget.Dashboard);
        vm.SelectedMenuItem.IsSelected.Should().BeTrue();
        vm.IsDashboardSelected.Should().BeTrue();
    }

    [Fact]
    public void Navigate_ToChat_OpensChatWindowAndKeepsSelection()
    {
        // Arrange
        var service = new FakeChatWindowService();
        var vm = new MainWindowViewModel(service);
        var chat = vm.MenuItems.Single(m => m.Target == NavigationTarget.Chat);

        // Act
        vm.NavigateCommand.Execute(chat);

        // Assert
        service.OpenCount.Should().Be(1);
        vm.SelectedMenuItem!.Target.Should().Be(NavigationTarget.Dashboard);
        chat.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void Navigate_ToSettings_SelectsItemAndLeavesDashboard()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var dashboard = vm.MenuItems.Single(m => m.Target == NavigationTarget.Dashboard);
        var settings = vm.MenuItems.Single(m => m.Target == NavigationTarget.Settings);

        // Act
        vm.NavigateCommand.Execute(settings);

        // Assert
        vm.SelectedMenuItem.Should().Be(settings);
        settings.IsSelected.Should().BeTrue();
        dashboard.IsSelected.Should().BeFalse();
        vm.IsDashboardSelected.Should().BeFalse();
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
