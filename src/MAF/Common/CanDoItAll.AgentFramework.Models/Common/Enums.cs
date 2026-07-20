namespace CanDoItAll.AgentFramework.Models;

public enum AgentLifecycleStatus
{
    Draft,
    Active,
    Suspended,
    Archived
}

public enum ProviderKind
{
    OpenAi,
    AzureOpenAi,
    Ollama,
    ComfyUi
}

public enum ProviderTransportKind
{
    Responses,
    ChatCompletions
}

public enum ProviderProfilePurpose
{
    Chat,
    ImageGeneration
}

public enum CapabilityKind
{
    McpServer,
    Skill,
    Tool,
    Plugin,
    Rag,
    AiContext,
    Memory
}

public enum CapabilityProofStatus
{
    NotRun,
    Verified,
    Failed,
    PendingReview
}

public enum ChatMessageRole
{
    System,
    User,
    Assistant
}

public enum ExecutionState
{
    Idle,
    Preparing,
    Running,
    WaitingOnTool,
    Persisting,
    Completed,
    Failed
}

public enum RunOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public enum ExecutionApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public enum MemoryKind
{
    Fact,
    Preference,
    Context,
    FollowUp,
    Architecture
}

public enum AgentWorkloadKind
{
    General,
    Management,
    Hr,
    Sales,
    Assistant,
    Qa,
    Support,
    Programming,
    Spreadsheet,
    Mail,
    Research
}

public enum AgentChatHistoryMode
{
    ProviderDefault,
    FrameworkManaged,
    ProviderManaged
}

public enum AgentReasoningEffortLevel
{
    None,
    Low,
    Medium,
    High,
    ExtraHigh,
    Max
}
