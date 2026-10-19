namespace OpenHarness.Client.Core.Models;

/// <summary>Mirror of the API's <c>GeneralSettingsDto</c>; enum values are sent by name.</summary>
public sealed record ClientGeneralSettings(
    string DefaultPermissionMode,
    string Language,
    string Appearance,
    int ConversationFontSizePx,
    string WorkDetails,
    string CollapseWorkDetails,
    bool ShowCodingView,
    string SendBehaviorWhileBusy,
    string PerformanceAndUsage,
    bool UploadSessionLog);

public sealed record ClientGeneralSettingsResponse(
    ClientGeneralSettings Settings,
    uint Version,
    DateTimeOffset UpdatedAtUtc);

public enum ClientSettingsSaveStatus
{
    Saved,
    Conflict,
    Forbidden,
    Invalid
}

public sealed record ClientSettingsSaveResult(
    ClientSettingsSaveStatus Status,
    ClientGeneralSettingsResponse? Response = null,
    string? ErrorMessage = null);
