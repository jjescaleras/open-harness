using System.Text.Json;
using FluentAssertions;
using OpenHarness.Client.Core.Streaming;
using OpenHarness.UI.Avalonia.ViewModels;
using Xunit;

namespace OpenHarness.UnitTests.UI;

public class ChatStreamViewModelTests
{
    [Fact]
    public void EnqueueTextDelta_And_FlushBuffer_UpdatesStreamingMessage()
    {
        // Arrange
        var vm = new ChatStreamViewModel();
        var msg = new ChatMessageViewModel
        {
            Role = "assistant",
            IsStreaming = true
        };
        vm.Messages.Add(msg);
        vm.CurrentStreamingMessage = msg;

        // Act
        vm.EnqueueTextDelta("Hello ");
        vm.EnqueueTextDelta("world!");
        vm.FlushBufferImmediately();

        // Assert
        msg.Content.Should().Be("Hello world!");
    }

    [Fact]
    public void EnqueueThinkingDelta_And_FlushBuffer_UpdatesThinkingContent()
    {
        // Arrange
        var vm = new ChatStreamViewModel();
        var msg = new ChatMessageViewModel
        {
            Role = "assistant",
            IsStreaming = true
        };
        vm.Messages.Add(msg);
        vm.CurrentStreamingMessage = msg;

        // Act
        vm.EnqueueThinkingDelta("Let me think... ");
        vm.EnqueueThinkingDelta("Decided.");
        vm.FlushBufferImmediately();

        // Assert
        msg.HasThinking.Should().BeTrue();
        msg.ThinkingContent.Should().Be("Let me think... Decided.");
    }

    [Fact]
    public void HandleStreamEvent_ProcessesTextAndThinkingDeltas()
    {
        // Arrange
        var vm = new ChatStreamViewModel();
        var msg = new ChatMessageViewModel
        {
            Role = "assistant",
            IsStreaming = true
        };
        vm.Messages.Add(msg);
        vm.CurrentStreamingMessage = msg;

        var textDoc = JsonDocument.Parse("{\"delta\":\"Streaming chunk\"}");
        var thinkDoc = JsonDocument.Parse("{\"delta\":\"Analyzing request\"}");

        // Act
        vm.HandleStreamEvent(new HarnessStreamEvent("text.delta", textDoc.RootElement.ToString(), textDoc));
        vm.HandleStreamEvent(new HarnessStreamEvent("thinking.delta", thinkDoc.RootElement.ToString(), thinkDoc));
        vm.FlushBufferImmediately();

        // Assert
        msg.Content.Should().Be("Streaming chunk");
        msg.ThinkingContent.Should().Be("Analyzing request");
    }

    [Fact]
    public void HandleStreamEvent_ToolStartedAndCompleted_UpdatesMessages()
    {
        // Arrange
        var vm = new ChatStreamViewModel();
        var toolExecutionId = Guid.NewGuid();
        var startDoc = JsonDocument.Parse($"{{\"toolName\":\"run_bash\",\"executionId\":\"{toolExecutionId}\"}}");
        var completeDoc = JsonDocument.Parse($"{{\"executionId\":\"{toolExecutionId}\",\"result\":\"exit 0\"}}");

        // Act
        vm.HandleStreamEvent(new HarnessStreamEvent("tool.started", startDoc.RootElement.ToString(), startDoc));

        // Assert
        vm.Messages.Should().ContainSingle(m => m.IsTool && m.ToolName == "run_bash" && m.ToolStatus == "Running");

        // Act 2: Complete
        vm.HandleStreamEvent(new HarnessStreamEvent("tool.completed", completeDoc.RootElement.ToString(), completeDoc));

        // Assert 2
        var toolMsg = vm.Messages.Single(m => m.IsTool && m.ToolExecutionId == toolExecutionId);
        toolMsg.ToolStatus.Should().Be("Completed");
        toolMsg.Content.Should().Contain("exit 0");
    }

    [Fact]
    public void HandleStreamEvent_ApprovalRequired_FiresApprovalEvent()
    {
        // Arrange
        var vm = new ChatStreamViewModel();
        var sessionId = Guid.NewGuid();
        vm.CurrentSessionId = sessionId;

        Guid? capturedSessionId = null;
        JsonDocument? capturedDoc = null;
        vm.ApprovalRequired += (sid, doc) =>
        {
            capturedSessionId = sid;
            capturedDoc = doc;
        };

        var approvalDoc = JsonDocument.Parse("{\"toolName\":\"bash\",\"command\":\"git push\"}");

        // Act
        vm.HandleStreamEvent(new HarnessStreamEvent("sandbox.approval_required", approvalDoc.RootElement.ToString(), approvalDoc));

        // Assert
        capturedSessionId.Should().Be(sessionId);
        capturedDoc.Should().NotBeNull();
    }
}

