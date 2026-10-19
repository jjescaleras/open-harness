using OpenHarness.UI.Avalonia.ViewModels;
using OpenHarness.UI.Avalonia.Views;

namespace OpenHarness.UI.Avalonia.Services;

public sealed class ChatWindowService : IChatWindowService
{
    private MainChatWindow? _chatWindow;

    public void OpenChat()
    {
        // Reuse the open chat window instead of spawning duplicates.
        if (_chatWindow is not null)
        {
            _chatWindow.Activate();
            return;
        }

        _chatWindow = new MainChatWindow
        {
            DataContext = new HarnessShellViewModel(),
        };
        _chatWindow.Closed += (_, _) => _chatWindow = null;
        _chatWindow.Show();
    }
}
