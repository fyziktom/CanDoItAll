namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkflowCatalogItem(
    WorkflowId Id,
    WorkflowVersionId VersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowRuntimeBackendKind PreferredBackend,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowDefinitionSaveRequest(
    WorkflowId? Id,
    WorkflowVersionId? ExpectedVersionId,
    string Name,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowGraph Graph,
    WorkflowRuntimePolicy RuntimePolicy);

public sealed record WorkflowDefinitionDetail(
    WorkflowDefinition Definition,
    WorkflowValidationResult Validation);

public sealed record WorkflowArtifactPolicy(
    bool CaptureNodeOutputs,
    int MaxInlinePayloadCharacters,
    IReadOnlyList<WorkflowArtifactKind> AllowedArtifactKinds);

public sealed record WorkflowHumanInLoopPolicy(
    bool AllowHumanInputNodes,
    bool RequireApprovalForToolUse,
    int DefaultRequestTimeoutMinutes);

public sealed record WorkflowSettings(
    WorkflowRuntimePolicy DefaultRuntimePolicy,
    WorkflowArtifactPolicy ArtifactPolicy,
    WorkflowHumanInLoopPolicy HumanInLoopPolicy)
{
    public static WorkflowSettings Default { get; } = new(
        new WorkflowRuntimePolicy(
            WorkflowRuntimeBackendKind.DurableTask,
            AllowInProcessPreviewRuns: true,
            RequireDurableProductionRuns: true,
            ExposeAzureFunctionsStatusEndpoint: false,
            ExposeAzureFunctionsMcpTool: false),
        new WorkflowArtifactPolicy(
            CaptureNodeOutputs: true,
            MaxInlinePayloadCharacters: 64_000,
            AllowedArtifactKinds:
            [
                WorkflowArtifactKind.Text,
                WorkflowArtifactKind.Json,
                WorkflowArtifactKind.File
            ]),
        new WorkflowHumanInLoopPolicy(
            AllowHumanInputNodes: true,
            RequireApprovalForToolUse: true,
            DefaultRequestTimeoutMinutes: 240));
}

public sealed record LlmCallComponentSaveRequest(
    WorkflowComponentId? Id,
    string Name,
    Guid? ProviderProfileId,
    string Model,
    WorkflowModality Modality,
    WorkflowModelSettings ModelSettings,
    string Instructions,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    AgentPermissionsPolicy Permissions);

public sealed record WorkflowTestRunRequest(
    WorkflowId? WorkflowId,
    WorkflowVersionId? VersionId,
    WorkflowDefinition? DraftDefinition,
    string InputJson,
    WorkflowRuntimeBackendKind? RequestedBackend,
    bool ValidateOnly);

public sealed record WorkflowTestRunResult(
    bool Succeeded,
    WorkflowValidationResult Validation,
    WorkflowRunSnapshot? Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests,
    string ErrorMessage);
