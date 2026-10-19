namespace OpenHarness.Domain.ValueObjects;

public sealed class AgentModelConfig
{
    public string Provider { get; set; } = "anthropic";
    public string ModelId { get; set; } = "claude-sonnet-4-5";
    public string ReasoningEffort { get; set; } = "medium";
    public bool UsePiRpcMode { get; set; } = true;
    public double Temperature { get; set; } = 0.2;
    public int MaxOutputTokens { get; set; } = 8192;
}
