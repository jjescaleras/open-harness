namespace OpenHarness.Domain.ValueObjects;

public sealed class TurnTokenUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CachedInputTokens { get; set; }
    public int ReasoningTokens { get; set; }
}
