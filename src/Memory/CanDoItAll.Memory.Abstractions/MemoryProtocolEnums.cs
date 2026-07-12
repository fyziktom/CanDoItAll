namespace CanDoItAll.Memory.Abstractions;

public enum MemoryOperationKind
{
    ContextQuery = 0,
    Ingestion = 1,
    Feedback = 2,
    SourceRequest = 3,
    EventAcknowledge = 4,
    OperationStatus = 5,
    CapabilityExchange = 6,
    Health = 7
}

public enum MemorySensitivity
{
    Public = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum MemoryRetentionPolicy
{
    Default = 0,
    Ephemeral = 1,
    Session = 2,
    Project = 3,
    LegalHold = 4,
    Forgettable = 5
}

public enum MemoryApprovalPosture
{
    AutoApproved = 0,
    RequireApproval = 1,
    Denied = 2
}

public enum MemoryRedactionLevel
{
    None = 0,
    SummaryOnly = 1,
    MetadataOnly = 2,
    Denied = 3
}

public enum MemorySourceScope
{
    Workspace = 0,
    Project = 1,
    Process = 2,
    Workflow = 3,
    Agent = 4,
    Crm = 5,
    Resource = 6,
    Manual = 7
}

public enum MemorySourceKind
{
    Workspace = 0,
    Project = 1,
    Process = 2,
    Workflow = 3,
    AgentSession = 4,
    CrmRecord = 5,
    Resource = 6,
    ManualPayload = 7
}

public enum MemoryFeedbackOutcome
{
    Unknown = 0,
    Useful = 1,
    NotUseful = 2,
    Harmful = 3,
    Corrected = 4,
    EconomicallyPositive = 5,
    EconomicallyNegative = 6
}

public enum MemoryOperationStatus
{
    Accepted = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Canceled = 4,
    TimedOut = 5
}

public enum MemoryProviderEventKind
{
    Hypothesis = 0,
    SourceRequest = 1,
    FeedbackRequest = 2,
    VerificationRequest = 3,
    MaintenanceSignal = 4,
    Health = 5
}

public enum MemoryProviderHealthStatus
{
    Reachable = 0,
    Degraded = 1,
    Unreachable = 2
}

public enum MemoryWarningKind
{
    PolicyLimited = 0,
    ProviderPartial = 1,
    CapabilityUnavailable = 2,
    SourceUnavailable = 3
}

public enum MemoryPayloadKind
{
    Text = 0,
    Json = 1
}
