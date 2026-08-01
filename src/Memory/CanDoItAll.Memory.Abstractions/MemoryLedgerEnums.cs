namespace CanDoItAll.Memory.Abstractions;

public enum MemoryLedgerStatus
{
    Pending = 0,
    Accepted = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    TimedOut = 5,
    Cancelled = 6,
    Expired = 7,
    Forgotten = 8
}

public enum MemoryFeedbackStage
{
    ContextDelivered = 0,
    ImmediateToolResult = 1,
    ContextUsed = 2,
    ProcessCompleted = 3,
    CustomerAccepted = 4,
    EconomicImpact = 5,
    LaterCorrection = 6,
    FeedbackClosed = 7,
    ForgetUnpin = 8
}

public enum MemoryFeedbackMatchState
{
    Matched = 0,
    Unmatched = 1
}

public enum MemoryEventPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum MemoryEventOrigin
{
    Host = 0,
    MemoryProvider = 1,
    Agent = 2,
    Workflow = 3
}

public enum MemoryEventAdmissionStatus
{
    Accepted = 0,
    Duplicate = 1,
    LoopRejected = 2,
    Expired = 3
}

public enum MemoryLedgerRetentionDecision
{
    Active = 0,
    Expire = 1,
    Forget = 2
}

public enum MemoryIpfsPinState
{
    NotPinned = 0,
    Pinned = 1,
    UnpinRequested = 2,
    Unpinned = 3,
    UnpinFailed = 4
}
