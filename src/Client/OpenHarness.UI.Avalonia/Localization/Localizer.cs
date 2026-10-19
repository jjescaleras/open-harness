using System.ComponentModel;
using System.Globalization;

namespace OpenHarness.UI.Avalonia.Localization;

/// <summary>
/// Single access point for UI strings. XAML binds to its indexer through <see cref="TrExtension"/>,
/// and view models read it directly, so switching <see cref="Culture"/> refreshes every bound text.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public const string DefaultLanguage = "es";

    /// <summary>Languages that ship a translation. Add a code here together with its <c>Strings.{code}.resx</c>.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = [DefaultLanguage];

    public static Localizer Instance { get; } = new();

    private Localizer()
    {
        Culture = CultureInfo.GetCultureInfo(DefaultLanguage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo Culture { get; private set; }

    public string this[string key] =>
        Strings.ResourceManager.GetString(key, Culture) ?? $"#{key}#";

    public string Format(string key, params object?[] args) =>
        string.Format(Culture, this[key], args);

    /// <summary>Switches the UI language; unsupported codes fall back to <see cref="DefaultLanguage"/>.</summary>
    public void SetLanguage(string? languageCode)
    {
        var code = languageCode is not null && SupportedLanguages.Contains(languageCode, StringComparer.OrdinalIgnoreCase)
            ? languageCode.ToLowerInvariant()
            : DefaultLanguage;

        if (string.Equals(Culture.Name, code, StringComparison.OrdinalIgnoreCase)) return;

        Culture = CultureInfo.GetCultureInfo(code);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        // Avalonia refreshes indexer bindings on this property name.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
