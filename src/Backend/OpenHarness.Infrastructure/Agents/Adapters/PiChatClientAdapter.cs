using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenHarness.Application.Common.Interfaces;

namespace OpenHarness.Infrastructure.Agents.Adapters;

public sealed class PiChatClientAdapter : IChatClient
{
    private readonly IPiRpcSessionFactory _sessionFactory;
    private readonly string? _workingDirectory;
    private readonly ILogger<PiChatClientAdapter> _logger;
    private bool _disposed;

    public ChatClientMetadata Metadata { get; } = new("PiRpc");

    public PiChatClientAdapter(
        IPiRpcSessionFactory sessionFactory,
        ILogger<PiChatClientAdapter> logger,
        string? workingDirectory = null)
    {
        _sessionFactory = sessionFactory;
        _logger = logger;
        _workingDirectory = workingDirectory;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var fullText = new StringBuilder();
        ChatRole? role = ChatRole.Assistant;

        await foreach (var update in GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullText.Append(update.Text);
            }
            if (update.Role.HasValue)
            {
                role = update.Role.Value;
            }
        }

        var message = new ChatMessage(role ?? ChatRole.Assistant, fullText.ToString());
        return new ChatResponse(message);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var prompt = FormatPrompt(chatMessages);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            yield break;
        }

        await using var bridge = _sessionFactory.CreateBridge(_workingDirectory);
        Exception? startException = null;

        try
        {
            await bridge.StartAsync(cancellationToken);
            await bridge.SendPromptAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Pi RPC bridge execution.");
            startException = ex;
        }

        if (startException is not null)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, $"Error: {startException.Message}")
            {
                FinishReason = ChatFinishReason.ContentFilter
            };
            yield break;
        }

        await foreach (var evt in bridge.ReadEventsAsync(cancellationToken))
        {
            if (evt.Type == "text.delta" && !string.IsNullOrEmpty(evt.Delta))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, evt.Delta);
            }
            else if (evt.Type == "turn.completed")
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, string.Empty)
                {
                    FinishReason = ChatFinishReason.Stop
                };
            }
            else if (evt.Type == "error")
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, $"\n[Error: {evt.Delta}]")
                {
                    FinishReason = ChatFinishReason.ContentFilter
                };
            }
        }
    }

    private static string FormatPrompt(IEnumerable<ChatMessage> messages)
    {
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        if (messageList.Count == 0) return string.Empty;

        // If only one user message, return its text
        if (messageList.Count == 1 && messageList[0].Role == ChatRole.User)
        {
            return messageList[0].Text ?? string.Empty;
        }

        // For conversational history, format multi-turn text
        var sb = new StringBuilder();
        foreach (var msg in messageList)
        {
            var roleName = msg.Role == ChatRole.User ? "User"
                : msg.Role == ChatRole.Assistant ? "Assistant"
                : msg.Role == ChatRole.System ? "System"
                : "User";

            sb.AppendLine($"{roleName}: {msg.Text}");
        }

        return sb.ToString().TrimEnd();
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IPiRpcSessionFactory))
        {
            return _sessionFactory;
        }

        if (serviceType == typeof(PiChatClientAdapter))
        {
            return this;
        }

        return null;
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
