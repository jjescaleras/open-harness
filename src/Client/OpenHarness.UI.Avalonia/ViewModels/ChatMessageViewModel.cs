using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenHarness.UI.Avalonia.ViewModels;

public partial class ChatMessageViewModel : ViewModelBase
{
    private readonly StringBuilder _contentBuilder = new();
    private readonly StringBuilder _thinkingBuilder = new();

    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _role = "assistant";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string? _thinkingContent;

    [ObservableProperty]
    private bool _hasThinking;

    [ObservableProperty]
    private bool _isThinkingExpanded;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string? _toolName;

    [ObservableProperty]
    private Guid? _toolExecutionId;

    [ObservableProperty]
    private string? _toolStatus;

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsTool => string.Equals(Role, "tool", StringComparison.OrdinalIgnoreCase);
    public bool IsSystem => string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase);

    public void AppendContent(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;
        _contentBuilder.Append(delta);
        Content = _contentBuilder.ToString();
    }

    public void AppendThinking(string delta)
    {
        if (string.IsNullOrEmpty(delta)) return;
        _thinkingBuilder.Append(delta);
        ThinkingContent = _thinkingBuilder.ToString();
        HasThinking = true;
    }

    public void SetContent(string content)
    {
        _contentBuilder.Clear();
        _contentBuilder.Append(content);
        Content = content;
    }

    public void SetThinking(string thinking)
    {
        _thinkingBuilder.Clear();
        if (!string.IsNullOrEmpty(thinking))
        {
            _thinkingBuilder.Append(thinking);
            ThinkingContent = thinking;
            HasThinking = true;
        }
        else
        {
            ThinkingContent = null;
            HasThinking = false;
        }
    }
}

