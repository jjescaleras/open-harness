using OpenHarness.Domain.Enums;
using OpenHarness.Domain.ValueObjects;

namespace OpenHarness.Application.Features.Settings;

public static class GeneralSettingsMapper
{
    /// <summary>Languages with UI translations. Add a culture here when its resources ship.</summary>
    public static readonly IReadOnlySet<string> SupportedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "es" };

    public static GeneralSettingsDto ToDto(GeneralSettings s) => new(
        s.DefaultPermissionMode.ToString(),
        s.Language,
        s.Appearance.ToString(),
        s.ConversationFontSizePx,
        s.WorkDetails.ToString(),
        s.CollapseWorkDetails.ToString(),
        s.ShowCodingView,
        s.SendBehaviorWhileBusy.ToString(),
        s.PerformanceAndUsage.ToString(),
        s.UploadSessionLog);

    /// <summary>Validates the DTO and maps it to the domain value object; returns the list of invalid fields on failure.</summary>
    public static bool TryToDomain(GeneralSettingsDto dto, out GeneralSettings settings, out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();
        settings = new GeneralSettings
        {
            DefaultPermissionMode = Parse<SandboxMode>(dto.DefaultPermissionMode, nameof(dto.DefaultPermissionMode), problems),
            Appearance = Parse<AppearanceMode>(dto.Appearance, nameof(dto.Appearance), problems),
            WorkDetails = Parse<DetailLevel>(dto.WorkDetails, nameof(dto.WorkDetails), problems),
            CollapseWorkDetails = Parse<CollapseWorkDetailsMode>(dto.CollapseWorkDetails, nameof(dto.CollapseWorkDetails), problems),
            SendBehaviorWhileBusy = Parse<BusySendBehavior>(dto.SendBehaviorWhileBusy, nameof(dto.SendBehaviorWhileBusy), problems),
            PerformanceAndUsage = Parse<DetailLevel>(dto.PerformanceAndUsage, nameof(dto.PerformanceAndUsage), problems),
            ConversationFontSizePx = dto.ConversationFontSizePx,
            ShowCodingView = dto.ShowCodingView,
            UploadSessionLog = dto.UploadSessionLog
        };

        if (string.IsNullOrWhiteSpace(dto.Language) || !SupportedLanguages.Contains(dto.Language))
        {
            problems.Add(nameof(dto.Language));
        }
        else
        {
            settings.Language = dto.Language.ToLowerInvariant();
        }

        if (dto.ConversationFontSizePx is < GeneralSettings.MinFontSizePx or > GeneralSettings.MaxFontSizePx)
        {
            problems.Add(nameof(dto.ConversationFontSizePx));
        }

        errors = problems;
        return problems.Count == 0;
    }

    private static TEnum Parse<TEnum>(string? value, string field, List<string> problems) where TEnum : struct, Enum
    {
        // Reject numeric strings: Enum.TryParse would accept "42" as an undefined value.
        if (value is not null
            && !value.Any(char.IsDigit)
            && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        problems.Add(field);
        return default;
    }
}
