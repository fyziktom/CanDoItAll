namespace CanDoItAll.AgentFramework.Models;

public static class ProcessOperationContractNames
{
    public const string ReadProcessContext = "ReadProcessContext";
    public const string ReadProjectStructure = "ReadProjectStructure";
    public const string ReadUpstreamArtifacts = "ReadUpstreamArtifacts";
    public const string WriteManagedProcessArtifacts = "WriteManagedProcessArtifacts";
    public const string WriteExternalArtifactDestination = "WriteExternalArtifactDestination";
    public const string MutateProductTarget = "MutateProductTarget";
    public const string RunValidation = "RunValidation";
    public const string LaunchRuntime = "LaunchRuntime";
    public const string CaptureRuntimeProof = "CaptureRuntimeProof";
    public const string ExecuteExternalAction = "ExecuteExternalAction";
    public const string RecoverArtifactsOnly = "RecoverArtifactsOnly";
    public const string EscalateOrDecide = "EscalateOrDecide";

    public const string ManagedProcessArtifactsOnly = "ManagedProcessArtifactsOnly";
    public const string ManagedOutputProduct = "ManagedOutputProduct";
    public const string ExternalArtifactDestination = "ExternalArtifactDestination";
    public const string ExternalProductTargetReadOnly = "ExternalProductTargetReadOnly";
    public const string ExternalProductTargetMutable = "ExternalProductTargetMutable";
    public const string ExternalActionControlled = "ExternalActionControlled";

    public static IReadOnlyList<string> AllOperations { get; } =
    [
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
    ];

    public static IReadOnlyList<string> AllTargetScopes { get; } =
    [
        ManagedProcessArtifactsOnly,
        ManagedOutputProduct,
        ExternalArtifactDestination,
        ExternalProductTargetReadOnly,
        ExternalProductTargetMutable,
        ExternalActionControlled
    ];

    public static bool IsOperationName(string? value)
        => Contains(AllOperations, value);

    public static bool IsTargetScopeName(string? value)
        => Contains(AllTargetScopes, value);

    private static bool Contains(IReadOnlyList<string> values, string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           values.Contains(value.Trim(), StringComparer.Ordinal);
}

public static class ProviderUsagePhaseContractNames
{
    public const string NormalRun = "normal-run";
    public const string Continuation = "continuation";
    public const string BackgroundPoll = "background-poll";
    public const string StructuredOutputRepair = "structured-output-repair";
    public const string FinalizerShortCircuit = "finalizer-short-circuit";
    public const string FailedAfterProviderCall = "failed-after-provider-call";
    public const string CancelledWithUsage = "cancelled-with-usage";
    public const string WorkflowSummarization = "workflow-summarization";

    public static IReadOnlyList<string> All { get; } =
    [
        NormalRun,
        Continuation,
        BackgroundPoll,
        StructuredOutputRepair,
        FinalizerShortCircuit,
        FailedAfterProviderCall,
        CancelledWithUsage,
        WorkflowSummarization
    ];

    public static bool IsKnownPhase(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           All.Contains(value.Trim(), StringComparer.Ordinal);
}

public static class WorkflowJsonPathContractNames
{
    public const string Status = "$.status";
    public const string Route = "$.route";
    public const string ProjectId = "$.projectId";
    public const string ProjectIdNested = "$.project.id";
    public const string NodeId = "$.nodeId";
    public const string WorkflowNodeId = "$.runContext.workflowNodeId";
    public const string Category = "$.category";
    public const string Targets = "$.targets";
    public const string BranchOutcomeKey = "$.branchOutcomeKey";
    public const string EvidenceRefs = "$.evidenceRefs";
    public const string Reason = "$.reason";
    public const string ToolName = "$.toolName";
    public const string Tasks = "$.tasks";
    public const string Office365ProcessingFirstMessageId = "$.inputPayload.runContext.office365Processing.messageIds[0]";
    public const string GmailProcessingFirstMessageId = "$.inputPayload.runContext.gmailProcessing.messageIds[0]";

    public static IReadOnlyList<string> All { get; } =
    [
        Status,
        Route,
        ProjectId,
        ProjectIdNested,
        NodeId,
        WorkflowNodeId,
        Category,
        Targets,
        BranchOutcomeKey,
        EvidenceRefs,
        Reason,
        ToolName,
        Tasks,
        Office365ProcessingFirstMessageId,
        GmailProcessingFirstMessageId
    ];

    public static bool IsKnownPath(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           All.Contains(value.Trim(), StringComparer.Ordinal);
}
