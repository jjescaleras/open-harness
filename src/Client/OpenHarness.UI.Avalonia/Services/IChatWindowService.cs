namespace OpenHarness.UI.Avalonia.Services;

/// <summary>
/// Opens the agentic chat workspace (<c>MainChatWindow</c>) from other windows
/// without coupling view models to Avalonia window types.
/// </summary>
public interface IChatWindowService
{
    void OpenChat();
}
