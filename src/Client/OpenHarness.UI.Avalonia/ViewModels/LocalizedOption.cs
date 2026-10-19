using CommunityToolkit.Mvvm.ComponentModel;
using OpenHarness.UI.Avalonia.Localization;

namespace OpenHarness.UI.Avalonia.ViewModels;

/// <summary>
/// A selectable value whose label comes from <see cref="Localizer"/> and follows language changes.
/// Deliberately not a <see cref="ViewModelBase"/> so the ViewLocator never tries to resolve a view for it.
/// </summary>
public sealed class LocalizedOption : ObservableObject
{
    public LocalizedOption(string value, string labelKey, string icon = "")
    {
        Value = value;
        LabelKey = labelKey;
        Icon = icon;
        Localizer.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public string Value { get; }
    public string LabelKey { get; }
    public string Icon { get; }
    public string Label => Localizer.Instance[LabelKey];
}
