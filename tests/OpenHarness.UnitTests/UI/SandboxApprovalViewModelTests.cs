using FluentAssertions;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class SandboxApprovalViewModelTests
{
    [Fact]
    public async Task ApproveOnceAsync_SubmitsDecision_And_ClosesWithTrue()
    {
        // Arrange
        using var vm = new SandboxApprovalViewModel();
        vm.SessionId = Guid.NewGuid();
        vm.ToolExecutionId = Guid.NewGuid();
        vm.ToolName = "rm_file";
        vm.CommandOrTarget = "rm -rf /tmp/data";

        bool? closedResult = null;
        vm.RequestClose += res => closedResult = res;

        // Act
        await vm.ApproveOnceAsync();

        // Assert
        closedResult.Should().BeTrue();
        vm.DecisionResult.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAndRememberAsync_SubmitsDecision_And_ClosesWithTrue()
    {
        // Arrange
        using var vm = new SandboxApprovalViewModel();
        bool? closedResult = null;
        vm.RequestClose += res => closedResult = res;

        // Act
        await vm.ApproveAndRememberAsync();

        // Assert
        closedResult.Should().BeTrue();
        vm.DecisionResult.Should().BeTrue();
    }

    [Fact]
    public async Task DenyAsync_SubmitsDecision_And_ClosesWithFalse()
    {
        // Arrange
        using var vm = new SandboxApprovalViewModel();
        bool? closedResult = null;
        vm.RequestClose += res => closedResult = res;

        // Act
        await vm.DenyAsync();

        // Assert
        closedResult.Should().BeFalse();
        vm.DecisionResult.Should().BeFalse();
    }

    [Fact]
    public void InitialState_HasFormattedRemainingTime()
    {
        // Arrange
        using var vm = new SandboxApprovalViewModel();

        // Assert
        vm.RemainingSeconds.Should().Be(300);
        vm.RemainingTimeFormatted.Should().Be("05:00");
        vm.IsExpired.Should().BeFalse();
    }
}

