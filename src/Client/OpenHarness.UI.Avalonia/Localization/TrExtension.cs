using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace OpenHarness.UI.Avalonia.Localization;

/// <summary>
/// Localized text in XAML: <c>Text="{i18n:Tr Settings_General_Title}"</c>.
/// Returns a one-way binding so the text follows <see cref="Localizer.SetLanguage"/>.
/// </summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay
        };
}
