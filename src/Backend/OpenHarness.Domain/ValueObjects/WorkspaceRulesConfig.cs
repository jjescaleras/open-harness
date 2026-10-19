namespace OpenHarness.Domain.ValueObjects;

public sealed class WorkspaceRulesConfig
{
    public List<string> AllowedPaths { get; set; } = [];
    public List<string> DeniedPaths { get; set; } = [];
    public List<string> AllowedCommands { get; set; } = [];
    public List<string> DeniedCommands { get; set; } = [];
    public int MaxExecutionTimeoutSeconds { get; set; } = 120;
}
