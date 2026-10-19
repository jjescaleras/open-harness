namespace OpenHarness.Application.Features.Settings;

/// <summary>
/// Wire format of <c>GeneralSettings</c>. Enum values travel as their names
/// (e.g. "WorkspaceWrite") so clients do not depend on the Domain assembly.
/// </summary>
public sealed record GeneralSettingsDto(
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

public sealed record GeneralSettingsResponse(
    GeneralSettingsDto Settings,
    uint Version,
    DateTimeOffset UpdatedAtUtc);

/// <param name="ExpectedVersion">
/// Version the client last read; when it no longer matches the stored one the update is rejected
/// with <c>SettingsConflict</c> instead of silently overwriting another administrator's changes.
/// </param>
public sealed record UpdateGeneralSettingsRequest(
    GeneralSettingsDto Settings,
    uint? ExpectedVersion);
