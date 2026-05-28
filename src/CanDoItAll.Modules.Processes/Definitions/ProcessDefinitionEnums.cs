namespace CanDoItAll.Modules.Processes;

public enum ProcessDefinitionStatus
{
    Draft,
    Published,
    Archived
}

public enum ProcessVersionStatus
{
    Draft,
    Published,
    Superseded,
    Archived
}

public enum ProcessDefinitionContractMode
{
    Compatibility,
    Strict
}

public enum ProcessCriticality
{
    Low,
    Standard,
    High,
    MissionCritical
}

public enum ProcessAutonomyLevel
{
    Manual,
    Assisted,
    Guarded,
    Delegated
}

public enum ProcessStepKind
{
    Start = 0,
    Work = 1,
    Decision = 2,
    Approval = 3,
    Review = 4,
    Delivery = 5,
    End = 6,
    Subprocess = 7
}

public enum ProcessStepOperation
{
    ReadProcessContext,
    ReadProjectStructure,
    ReadUpstreamArtifacts,
    WriteManagedProcessArtifacts,
    WriteExternalArtifactDestination,
    MutateProductTarget,
    RunValidation,
    LaunchRuntime,
    CaptureRuntimeProof,
    ExecuteExternalAction,
    RecoverArtifactsOnly,
    EscalateOrDecide
}

public enum ProcessStepTargetScope
{
    ManagedProcessArtifactsOnly,
    ManagedOutputProduct,
    ExternalArtifactDestination,
    ExternalProductTargetReadOnly,
    ExternalProductTargetMutable,
    ExternalActionControlled
}

public enum ProcessStepBlockReasonCode
{
    None,
    MissingUpstreamArtifact,
    PolicyDeniedExternalPath,
    ToolUnavailable,
    MissingCredential,
    ValidationFailed,
    NoProgress,
    RuntimeInvariantViolation,
    CapabilityGap,
    ArtifactContractUnsatisfied,
    AgentExecutionFailed,
    ManualRerun,
    Unknown
}

public enum ProcessStepBlockCause
{
    OwnOutput,
    UpstreamInput,
    RuntimeEvidence,
    PolicyDenied
}

public enum ProcessStepRecoveryOption
{
    None,
    WaitForArtifactMaterialization,
    RecoverArtifactsOnly,
    RetryAgent,
    FreshAgentSession,
    ReworkContinuation,
    HumanEscalation,
    RepairImplementation,
    RerunValidation
}

public enum ProcessResponsibilityKind
{
    Responsible = 0,
    Reviewer = 1,
    Approver = 2,
    Backup = 3,
    Accountable = 4
}

public enum ProcessExecutorKind
{
    Human,
    AiAgent,
    Workflow
}

public static class ProcessExecutorKindNames
{
    public const string Human = "Human";
    public const string AiAgent = "AI agent";
    public const string Workflow = "Workflow";

    public static string ToPersistedName(ProcessExecutorKind executorKind)
    {
        return executorKind switch
        {
            ProcessExecutorKind.AiAgent => AiAgent,
            ProcessExecutorKind.Workflow => Workflow,
            _ => Human
        };
    }

    public static string Normalize(string? executorKind)
    {
        return ToPersistedName(Resolve(executorKind));
    }

    public static ProcessExecutorKind Resolve(string? executorKind)
    {
        if (string.IsNullOrWhiteSpace(executorKind))
        {
            return ProcessExecutorKind.Human;
        }

        var normalized = new string(executorKind
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalized switch
        {
            "workflow" or "workflows" => ProcessExecutorKind.Workflow,
            "ai" or "aiagent" or "agent" or "artificialintelligence" => ProcessExecutorKind.AiAgent,
            "human" or "person" or "people" or "team" or "teammember" or "workforce" or "manual" => ProcessExecutorKind.Human,
            _ when normalized.Contains("workflow", StringComparison.Ordinal) => ProcessExecutorKind.Workflow,
            _ when normalized.Contains("ai", StringComparison.Ordinal) || normalized.Contains("agent", StringComparison.Ordinal) => ProcessExecutorKind.AiAgent,
            _ => ProcessExecutorKind.Human
        };
    }

    public static bool IsWorkflow(string? executorKind)
    {
        return Resolve(executorKind) == ProcessExecutorKind.Workflow;
    }

    public static bool IsAiAgent(string? executorKind)
    {
        return Resolve(executorKind) == ProcessExecutorKind.AiAgent;
    }
}

public enum ProcessArtifactKind
{
    Brief = 0,
    Evidence = 1,
    Decision = 2,
    Deliverable = 3,
    Transcript = 4,
    Checklist = 5,
    Prompt = 6,
    Dataset = 7,
    Other = 8,
    DecisionRecord = 9
}

public enum ProcessArtifactTrustRequirement
{
    None = 0,
    ReviewRequired = 1,
    HumanApproved = 2,
    TrustedSource = 3,
    ApprovalRequired = 4
}

public enum ProcessSensitivityLevel
{
    Public,
    Internal,
    Confidential,
    Restricted
}

public sealed record ProcessRoleExecutorKindOption(
    string Value,
    string Label,
    string Description);

public static class ProcessRoleExecutorKindOptions
{
    public const string Person = "person";
    public const string Agent = "agent";
    public const string PersonOrAgent = "person-or-agent";

    public static IReadOnlyList<ProcessRoleExecutorKindOption> Options { get; } =
    [
        new(Person, "Person", "Human project member or stakeholder."),
        new(Agent, "Agent", "AI agent or technical agent resource."),
        new(PersonOrAgent, "Person or agent", "Either a human or an AI agent can satisfy the role."),
        new(ProcessExecutorKindNames.Human, "Human", "Legacy human executor value."),
        new(ProcessExecutorKindNames.AiAgent, "AI agent", "Legacy AI agent executor value."),
        new(ProcessExecutorKindNames.Workflow, "Workflow", "Workflow-backed executor.")
    ];

    public static string NormalizeForSelection(string? executorKind)
    {
        if (string.IsNullOrWhiteSpace(executorKind))
        {
            return Person;
        }

        var trimmed = executorKind.Trim();
        var exactOption = Options.FirstOrDefault(option =>
            string.Equals(option.Value, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exactOption is not null)
        {
            return exactOption.Value;
        }

        var normalized = NormalizeToken(trimmed);
        return normalized switch
        {
            "personoragent" or "humanoragent" or "personaiagent" or "humanaiagent" => PersonOrAgent,
            "person" or "people" or "teammember" or "team" or "manual" => Person,
            "agent" or "ai" or "aiagent" or "artificialintelligence" => Agent,
            "workflow" or "workflows" => ProcessExecutorKindNames.Workflow,
            "human" => ProcessExecutorKindNames.Human,
            _ when normalized.Contains("workflow", StringComparison.Ordinal) => ProcessExecutorKindNames.Workflow,
            _ when normalized.Contains("person", StringComparison.Ordinal) && normalized.Contains("agent", StringComparison.Ordinal) => PersonOrAgent,
            _ when normalized.Contains("human", StringComparison.Ordinal) && normalized.Contains("agent", StringComparison.Ordinal) => PersonOrAgent,
            _ when normalized.Contains("agent", StringComparison.Ordinal) || normalized.Contains("ai", StringComparison.Ordinal) => Agent,
            _ when normalized.Contains("human", StringComparison.Ordinal) || normalized.Contains("person", StringComparison.Ordinal) => Person,
            _ => trimmed
        };
    }

    public static bool IsWorkflow(string? executorKind)
    {
        return string.Equals(
            NormalizeForSelection(executorKind),
            ProcessExecutorKindNames.Workflow,
            StringComparison.Ordinal);
    }

    private static string NormalizeToken(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
