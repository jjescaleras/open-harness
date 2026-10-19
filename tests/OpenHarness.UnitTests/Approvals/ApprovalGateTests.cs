using FluentAssertions;
using OpenHarness.Application.Common.Models;
using OpenHarness.Infrastructure.Security;
using Xunit;

namespace OpenHarness.UnitTests.Approvals;

public class ApprovalGateTests
{
    private readonly InMemoryApprovalGate _gate = new();

    [Fact]
    public async Task WaitForDecisionAsync_WhenApproved_ShouldReturnApprovedDecision()
    {
        // Arrange
        var executionId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();

        // Act
        var waitTask = _gate.WaitForDecisionAsync(executionId, sessionId, "bash", "{\"command\": \"ls\"}", TimeSpan.FromSeconds(5));

        _gate.GetPendingApprovals().Should().ContainSingle(p => p.ToolExecutionId == executionId);

        var resolved = _gate.TryResolveDecision(executionId, new ApprovalDecision(Approved: true, DecidedBy: "operator"));
        resolved.Should().BeTrue();

        var decision = await waitTask;

        // Assert
        decision.Approved.Should().BeTrue();
        decision.DecidedBy.Should().Be("operator");
        _gate.GetPendingApprovals().Should().BeEmpty();
    }

    [Fact]
    public async Task WaitForDecisionAsync_WhenTimesOut_ShouldReturnDeniedDueToTimeout()
    {
        // Arrange
        var executionId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();

        // Act
        var decision = await _gate.WaitForDecisionAsync(
            executionId,
            sessionId,
            "bash",
            "{\"command\": \"rm -rf /\"}",
            TimeSpan.FromMilliseconds(50));

        // Assert
        decision.Approved.Should().BeFalse();
        decision.Reason.Should().Contain("timed out");
        _gate.GetPendingApprovals().Should().BeEmpty();
    }
}

