using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Application.Common.Models;
using OpenHarness.Application.Features.Sessions;
using OpenHarness.Infrastructure.Security;
using Xunit;

namespace OpenHarness.UnitTests.Sessions;

public class SessionServiceApprovalTests
{
    private sealed class DummyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }

    private sealed class DummyPiRpcSessionFactory : IPiRpcSessionFactory
    {
        public IPiRpcSessionBridge CreateBridge(string? workingDirectory = null) => throw new NotImplementedException();
    }

    [Fact]
    public async Task SubmitApprovalDecisionAsync_WhenApprovalExists_ShouldResolveSuccessfully()
    {
        // Arrange
        var gate = new InMemoryApprovalGate();
        var toolExecutionId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var waitTask = gate.WaitForDecisionAsync(
            toolExecutionId,
            sessionId,
            "bash",
            "{\"command\":\"git status\"}",
            TimeSpan.FromSeconds(5));

        var service = new SessionService(
            new DummyScopeFactory(),
            new DummyPiRpcSessionFactory(),
            gate,
            TimeProvider.System,
            NullLogger<SessionService>.Instance);

        var request = new ApprovalDecisionRequest(
            ToolExecutionId: toolExecutionId,
            Approved: true,
            Reason: "Authorized by admin",
            RememberForSession: false);

        // Act
        var result = await service.SubmitApprovalDecisionAsync(userId, sessionId, request);
        var decision = await waitTask;

        // Assert
        result.IsSuccess.Should().BeTrue();
        decision.Approved.Should().BeTrue();
        decision.Reason.Should().Be("Authorized by admin");
        decision.DecidedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task SubmitApprovalDecisionAsync_WhenApprovalDoesNotExist_ShouldReturnFailure()
    {
        // Arrange
        var gate = new InMemoryApprovalGate();
        var service = new SessionService(
            new DummyScopeFactory(),
            new DummyPiRpcSessionFactory(),
            gate,
            TimeProvider.System,
            NullLogger<SessionService>.Instance);

        var request = new ApprovalDecisionRequest(
            ToolExecutionId: Guid.CreateVersion7(),
            Approved: true);

        // Act
        var result = await service.SubmitApprovalDecisionAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ApprovalNotFound");
    }
}

