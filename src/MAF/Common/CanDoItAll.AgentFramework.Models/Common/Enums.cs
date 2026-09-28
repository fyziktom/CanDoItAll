namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Lifecycle status of a technical agent definition, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived.
/// Many runtime features, such as the first-party runtime tools, run recovery and process steps, require an Active
/// agent that is not a template, and Suspended or Archived agents cannot take part in handoffs.
/// </summary>
public enum AgentLifecycleStatus
{
    Draft,
    Active,
    Suspended,
    Archived
}

/// <summary>
/// Kind of model provider behind a provider profile. Agent, provider and workflow operations use a JSON integer:
/// 0 OpenAi (the OpenAI API or any OpenAI-compatible endpoint, including imported shared providers), 1 AzureOpenAi,
/// 2 Ollama, 3 ComfyUi (an image-generation server). LLM Chats definitions, editors and provider options write it as
/// camel-case text instead (<c>openAi</c>, <c>azureOpenAi</c>, <c>ollama</c>, <c>comfyUi</c>), while LLM Chats
/// invocation attempts use the integer form.
/// </summary>
public enum ProviderKind
{
    OpenAi,
    AzureOpenAi,
    Ollama,
    ComfyUi
}

/// <summary>
/// API style used to call a provider profile's models, as a JSON integer: 0 Responses (the OpenAI Responses API),
/// 1 ChatCompletions (the Chat Completions API).
/// </summary>
public enum ProviderTransportKind
{
    Responses,
    ChatCompletions
}

/// <summary>
/// What a provider profile is used for, as a JSON integer: 0 Chat (text generation for agents and chats),
/// 1 ImageGeneration (image generation tools and workflow steps).
/// </summary>
public enum ProviderProfilePurpose
{
    Chat,
    ImageGeneration
}

/// <summary>
/// Kind of a catalog capability, as a JSON integer: 0 McpServer (an MCP server whose tools a run can use), 1 Skill (a
/// skill or inline skill instructions), 2 Tool (for example an external process or HTTP tool), 3 Plugin (plugin
/// tools), 4 Rag (retrieval context), 5 AiContext (a context message), 6 Memory (retired; rejected when saved). Used by
/// catalog capabilities, capability editors and agent capability assignments. The schema name <c>CapabilityKind</c> is
/// shared with the capability identity kind of setup tests and access previews, which numbers kinds differently
/// (0 Skill, 1 Tool, 2 McpServer, 3 McpTool, 4 Plugin, 5 Rag, 6 AiContext, 7 Memory); each member's description
/// states the numbering it uses.
/// </summary>
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

/// <summary>
/// Result of a capability verification, as a JSON integer: 0 NotRun (never verified), 1 Verified (the verification's
/// checks passed), 2 Failed (a blocking problem was found, for example a missing file, an unreachable endpoint or raw
/// secrets in the configuration), 3 PendingReview (recorded, but it cannot be proven by a local check). It is
/// informational: the runtime does not use it to decide whether a capability is attached to a run.
/// </summary>
public enum CapabilityProofStatus
{
    NotRun,
    Verified,
    Failed,
    PendingReview
}

/// <summary>
/// Author of a chat message, as a JSON integer: 0 System (system instructions or messages written by the product),
/// 1 User, 2 Assistant. Server-sent event streams write it as camel-case text: <c>system</c>, <c>user</c>,
/// <c>assistant</c>.
/// </summary>
public enum ChatMessageRole
{
    System,
    User,
    Assistant
}

/// <summary>
/// State of an agent execution run, as a JSON integer: 0 Idle (no state; a log entry with it does not change the
/// run), 1 Preparing, 2 Running, 3 WaitingOnTool (stopped until the pending tool approvals are decided), 4 Persisting
/// (storing the result), 5 Completed, 6 Failed (also used for cancelled runs, whose outcome is Cancelled). Query
/// parameters accept the member name or the integer; server-sent event streams write camel-case text such as
/// <c>waitingOnTool</c>.
/// </summary>
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

/// <summary>
/// Final outcome of an agent execution run or run segment, as a JSON integer: 0 Succeeded, 1 Failed, 2 Cancelled.
/// Query parameters accept the member name or the integer; server-sent event streams write camel-case text such as
/// <c>cancelled</c>.
/// </summary>
public enum RunOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// Status of a tool approval request of an agent execution run, as a JSON integer: 0 Pending, 1 Approved, 2 Rejected.
/// Query parameters accept the member name or the integer; server-sent event streams write camel-case text such as
/// <c>pending</c>.
/// </summary>
public enum ExecutionApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Category label of a workspace memory note, as a JSON integer: 0 Fact, 1 Preference, 2 Context, 3 FollowUp,
/// 4 Architecture. It only labels the note; no runtime behavior depends on it, and undefined integers are not
/// rejected.
/// </summary>
public enum MemoryKind
{
    Fact,
    Preference,
    Context,
    FollowUp,
    Architecture
}

/// <summary>
/// Kind of work an agent does, as a JSON integer: 0 General, 1 Management, 2 Hr, 3 Sales, 4 Assistant, 5 Qa,
/// 6 Support, 7 Programming, 8 Spreadsheet, 9 Mail, 10 Research. It labels the agent, is the key of AgentRole memory
/// provider assignments and feeds process-readiness scoring; Programming and Research also enable conversation
/// compaction under the default policy.
/// </summary>
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

/// <summary>
/// Where an agent's chat history is kept between turns, as a JSON integer: 0 ProviderDefault (kept by the product,
/// except for OpenAI and Azure OpenAI provider profiles on the Responses transport that do not prefer product-managed
/// history, where the provider keeps it), 1 FrameworkManaged (kept and resent by the product), 2 ProviderManaged (kept
/// by the model provider).
/// </summary>
public enum AgentChatHistoryMode
{
    ProviderDefault,
    FrameworkManaged,
    ProviderManaged
}

/// <summary>
/// Reasoning (thinking) effort requested from a model. In the agent and provider operations it is a JSON integer:
/// 0 None (thinking disabled), 1 Low, 2 Medium, 3 High, 4 ExtraHigh, 5 Max, 6 Minimal; by effort the order is None,
/// Minimal, Low, Medium, High, ExtraHigh, Max. LLM Chats definitions, editors, mutation requests and model options use
/// camel-case text instead (<c>none</c>, <c>low</c>, <c>medium</c>, <c>high</c>, <c>extraHigh</c>, <c>max</c>,
/// <c>minimal</c>; read ignoring case), while LLM Chats invocation attempts use the integer form. Which levels a model
/// accepts is given by its thinking-effort capability; for models with an on/off thinking switch, None turns thinking
/// off and Medium turns it on.
/// </summary>
public enum AgentReasoningEffortLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    ExtraHigh = 4,
    Max = 5,
    Minimal = 6
}
