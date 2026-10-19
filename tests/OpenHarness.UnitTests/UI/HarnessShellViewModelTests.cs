using FluentAssertions;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class HarnessShellViewModelTests
{
    [Fact]
    public async Task CreateNewSession_InsertsAndSelectsSession()
    {
        // Arrange
        var vm = new HarnessShellViewModel();

        // Act
        await vm.CreateNewSessionAsync();

        // Assert
        vm.Sessions.Should().HaveCount(1);
        vm.SelectedSession.Should().NotBeNull();
        vm.SelectedSession!.Title.Should().Be("Session #1");
        vm.ChatStream.CurrentSessionId.Should().Be(vm.SelectedSession.Id);
    }

    [Fact]
    public void SelectingSession_MarksSessionSelected()
    {
        // Arrange
        var vm = new HarnessShellViewModel();
        var s1 = new SessionItemViewModel { Title = "Session 1" };
        var s2 = new SessionItemViewModel { Title = "Session 2" };
        vm.Sessions.Add(s1);
        vm.Sessions.Add(s2);

        // Act
        vm.SelectedSession = s2;

        // Assert
        s2.IsSelected.Should().BeTrue();
        s1.IsSelected.Should().BeFalse();
        vm.ChatStream.CurrentSessionId.Should().Be(s2.Id);
    }

    [Fact]
    public void SelectingToolExecution_UpdatesDiffEditor()
    {
        // Arrange
        var vm = new HarnessShellViewModel();
        var toolItem = new ToolExecutionItemViewModel
        {
            ToolName = "src/Models.cs",
            IsDiff = true,
            DiffContent = "+ public class NewModel {}"
        };
        vm.ToolExecutions.Add(toolItem);

        // Act
        vm.SelectedToolExecution = toolItem;

        // Assert
        vm.DiffEditor.FilePath.Should().Be("src/Models.cs");
        vm.DiffEditor.IsDiffMode.Should().BeTrue();
        vm.DiffEditor.Content.Should().Be("+ public class NewModel {}");
    }
}

