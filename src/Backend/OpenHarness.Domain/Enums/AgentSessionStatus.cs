namespace OpenHarness.Domain.Enums;

public enum AgentSessionStatus
{
    Idle = 0,
    Streaming = 1,
    WaitingForApproval = 2,
    Completed = 3,
    Failed = 4
}
