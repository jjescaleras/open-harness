using OpenHarness.Domain.Enums;

namespace OpenHarness.Domain.ValueObjects;

public sealed class GeneralSettings
{
    public const int MinFontSizePx = 10;
    public const int MaxFontSizePx = 24;

    public SandboxMode DefaultPermissionMode { get; set; } = SandboxMode.WorkspaceWrite;
    public string Language { get; set; } = "es";
    public AppearanceMode Appearance { get; set; } = AppearanceMode.System;
    public int ConversationFontSizePx { get; set; } = 14;
    public DetailLevel WorkDetails { get; set; } = DetailLevel.Detailed;
    public CollapseWorkDetailsMode CollapseWorkDetails { get; set; } = CollapseWorkDetailsMode.OnCompletion;
    public bool ShowCodingView { get; set; } = true;
    public BusySendBehavior SendBehaviorWhileBusy { get; set; } = BusySendBehavior.Queue;
    public DetailLevel PerformanceAndUsage { get; set; } = DetailLevel.Detailed;
    public bool UploadSessionLog { get; set; }
}
