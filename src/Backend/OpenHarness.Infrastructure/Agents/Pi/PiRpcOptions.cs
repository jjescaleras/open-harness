namespace OpenHarness.Infrastructure.Agents.Pi;

public sealed class PiRpcOptions
{
    public const string SectionName = "PiRpc";

    public string ExecutablePath { get; set; } = "pi";
    public string? DefaultWorkingDirectory { get; set; }
    public int StartupTimeoutSeconds { get; set; } = 5;
    public List<string> ExtraArgs { get; set; } = [];
}
