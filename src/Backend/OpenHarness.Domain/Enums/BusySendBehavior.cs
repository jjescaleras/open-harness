namespace OpenHarness.Domain.Enums;

/// <summary>What Enter / Send does while the agent is running; Ctrl+Enter triggers the other one.</summary>
public enum BusySendBehavior
{
    Queue = 0,
    Interrupt = 1
}
