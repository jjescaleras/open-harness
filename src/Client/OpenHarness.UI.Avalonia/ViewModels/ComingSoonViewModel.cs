using OpenHarness.UI.Avalonia.Localization;

namespace OpenHarness.UI.Avalonia.ViewModels;

/// <summary>Placeholder page for menu entries whose screen is not built yet.</summary>
public sealed class ComingSoonViewModel(string titleKey) : ViewModelBase
{
    public string TitleKey { get; } = titleKey;
    public string Title => Localizer.Instance[TitleKey];
}
