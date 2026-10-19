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
        vm.Kpis.Select(k => k.Title).Should().Contain(["Sesiones activas", "Aprobaciones pendientes"]);
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
    public void Navigate_ToSettings_ExpandsGroupAndOpensGeneral()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var dashboard = vm.MenuItems.Single(m => m.Target == NavigationTarget.Dashboard);
        var settings = vm.MenuItems.Single(m => m.Target == NavigationTarget.Settings);

        // Act
        vm.NavigateCommand.Execute(settings);

        // Assert
        settings.IsExpanded.Should().BeTrue();
        settings.IsSelected.Should().BeTrue();
        settings.Children.Select(c => c.Target).Should().Equal(
            NavigationTarget.SettingsGeneral, NavigationTarget.SettingsConnections, NavigationTarget.SettingsModels);
        vm.SelectedMenuItem!.Target.Should().Be(NavigationTarget.SettingsGeneral);
        vm.SelectedMenuItem.IsSelected.Should().BeTrue();
        vm.CurrentPage.Should().BeOfType<SettingsGeneralViewModel>();
        vm.Breadcrumb.Should().Be("Configuración / General");
        dashboard.IsSelected.Should().BeFalse();
        vm.IsDashboardSelected.Should().BeFalse();
    }

    [Fact]
    public void Navigate_ToExpandedSettings_CollapsesAndKeepsSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var settings = vm.MenuItems.Single(m => m.Target == NavigationTarget.Settings);
        vm.NavigateCommand.Execute(settings);

        // Act
        vm.NavigateCommand.Execute(settings);

        // Assert
        settings.IsExpanded.Should().BeFalse();
        vm.SelectedMenuItem!.Target.Should().Be(NavigationTarget.SettingsGeneral);
    }

    [Fact]
    public void Navigate_BetweenSubMenus_ShowsComingSoonAndReusesGeneralPage()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var settings = vm.MenuItems.Single(m => m.Target == NavigationTarget.Settings);
        vm.NavigateCommand.Execute(settings);
        var general = vm.CurrentPage;
        var connections = settings.Children.Single(c => c.Target == NavigationTarget.SettingsConnections);

        // Act
        vm.NavigateCommand.Execute(connections);

        // Assert
        vm.CurrentPage.Should().BeOfType<ComingSoonViewModel>()
            .Which.Title.Should().Be("Conexiones");
        settings.IsSelected.Should().BeTrue();
        settings.Children[0].IsSelected.Should().BeFalse();

        vm.NavigateCommand.Execute(settings.Children[0]);
        vm.CurrentPage.Should().BeSameAs(general);
    }

    [Fact]
    public void Menu_ForNonAdmin_HidesSettings()
    {
        var vm = new MainWindowViewModel(userRole: "user");

        vm.IsAdmin.Should().BeFalse();
        vm.MenuItems.Should().NotContain(m => m.Target == NavigationTarget.Settings);
    }

    [Fact]
    public void Navigate_ToComingSoonEntry_ShowsPlaceholderPage()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var sessions = vm.MenuItems.Single(m => m.Target == NavigationTarget.Sessions);

        // Act
        vm.NavigateCommand.Execute(sessions);

        // Assert
        vm.SelectedMenuItem.Should().Be(sessions);
        vm.CurrentPage.Should().BeOfType<ComingSoonViewModel>();
        vm.Breadcrumb.Should().Be("Sesiones");
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
