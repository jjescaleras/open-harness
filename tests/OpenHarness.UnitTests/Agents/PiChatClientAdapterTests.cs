using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenHarness.Application.Common.Interfaces;
using OpenHarness.Infrastructure.Agents.Adapters;
using Xunit;

namespace OpenHarness.UnitTests.Agents;

public class PiChatClientAdapterTests
{
    private sealed class MockBridge : IPiRpcSessionBridge
    {
        public bool IsRunning { get; set; } = true;
        public string? SentPrompt { get; private set; }
        public List<PiSessionEvent> EventsToYield { get; } = [];

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
        {
            SentPrompt = prompt;
            return Task.CompletedTask;
        }

        public Task SendApprovalResponseAsync(Guid toolExecutionId, bool confirmed, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<PiSessionEvent> ReadEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var evt in EventsToYield)
            {
                yield return evt;
            }
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MockSessionFactory(MockBridge bridge) : IPiRpcSessionFactory
    {
        public IPiRpcSessionBridge CreateBridge(string? workingDirectory = null) => bridge;
    }

    [Fact]
    public void Metadata_ShouldReturnPiRpcProviderAndAgentModel()
    {
        var bridge = new MockBridge();
        var factory = new MockSessionFactory(bridge);
        var adapter = new PiChatClientAdapter(factory, NullLogger<PiChatClientAdapter>.Instance);

        adapter.Metadata.ProviderName.Should().Be("PiRpc");
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ShouldYieldTextDeltasFromPiEvents()
    {
        // Arrange
        var bridge = new MockBridge();
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "text.delta", Delta: "Hello "));
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "text.delta", Delta: "World!"));
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "turn.completed", InputTokens: 10, OutputTokens: 5));

        var factory = new MockSessionFactory(bridge);
        var adapter = new PiChatClientAdapter(factory, NullLogger<PiChatClientAdapter>.Instance);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Greet me")
        };

        // Act
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in adapter.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        // Assert
        bridge.SentPrompt.Should().Be("Greet me");
        updates.Should().HaveCount(3);
        updates[0].Text.Should().Be("Hello ");
        updates[1].Text.Should().Be("World!");
        updates[2].FinishReason.Should().Be(ChatFinishReason.Stop);
    }

    [Fact]
    public async Task GetResponseAsync_ShouldAggregateDeltasIntoFullChatMessage()
    {
        // Arrange
        var bridge = new MockBridge();
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "text.delta", Delta: "Calculated: "));
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "text.delta", Delta: "42"));
        bridge.EventsToYield.Add(new PiSessionEvent(Type: "turn.completed"));

        var factory = new MockSessionFactory(bridge);
        var adapter = new PiChatClientAdapter(factory, NullLogger<PiChatClientAdapter>.Instance);

        var messages = new List<ChatMessage> { new(ChatRole.User, "21 * 2") };

        // Act
        var response = await adapter.GetResponseAsync(messages);

        response.Messages.Should().ContainSingle();
        response.Messages[0].Role.Should().Be(ChatRole.Assistant);
        response.Messages[0].Text.Should().Be("Calculated: 42");
    }
}
