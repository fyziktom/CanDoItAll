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

public static class WorkflowDefinitionExchangeFormats
{
    public const string Current = "CanDoItAll.WorkflowDefinition/v1";
}

public sealed record WorkflowDefinitionStatusChangeRequest(
    WorkflowId WorkflowId,
    WorkflowVersionId? ExpectedVersionId,
    WorkflowLifecycleStatus Status);

public sealed record WorkflowDefinitionExportEnvelope(
    string SourceFormat,
    WorkflowDefinition Definition,
    WorkflowValidationResult Validation,
    DateTimeOffset ExportedAtUtc);

public sealed record WorkflowDefinitionImportRequest(
    WorkflowDefinitionExportEnvelope Envelope,
    string? Name,
    WorkflowLifecycleStatus? Status,
    bool PreserveWorkflowId);

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
    WorkflowHumanInLoopPolicy HumanInLoopPolicy,
    AgentVoiceSettings? VoiceSettings = null)
{
    public static WorkflowSettings Default { get; } = new(
        new WorkflowRuntimePolicy(
            WorkflowRuntimeBackendKind.InProcess,
            AllowInProcessPreviewRuns: true,
            RequireDurableProductionRuns: false,
            ExposeAzureFunctionsStatusEndpoint: false,
            ExposeAzureFunctionsMcpTool: false),
        new WorkflowArtifactPolicy(
            CaptureNodeOutputs: true,
            MaxInlinePayloadCharacters: 64_000,
            AllowedArtifactKinds:
            [
                WorkflowArtifactKind.Text,
                WorkflowArtifactKind.Json,
                WorkflowArtifactKind.File,
                WorkflowArtifactKind.ToolReceipt,
                WorkflowArtifactKind.PreviewSimulation
            ]),
        new WorkflowHumanInLoopPolicy(
            AllowHumanInputNodes: true,
            RequireApprovalForToolUse: true,
            DefaultRequestTimeoutMinutes: 240),
        AgentVoiceSettings.Default);

    public AgentVoiceSettings NormalizedVoiceSettings => AgentVoiceSettingsNormalizer.Normalize(VoiceSettings);
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

public sealed record WorkflowProviderOption(
    Guid ProviderProfileId,
    string Name,
    ProviderKind Kind,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    string DefaultModel,
    IReadOnlyList<string> ModelOptions,
    bool IsEnabled,
    bool SupportsStreaming,
    bool SupportsTools,
    bool SupportsStructuredOutput,
    bool SupportsVision,
    bool SupportsBackgroundResponses);

public sealed record WorkflowTestRunRequest(
    WorkflowId? WorkflowId,
    WorkflowVersionId? VersionId,
    WorkflowDefinition? DraftDefinition,
    string InputJson,
    WorkflowRuntimeBackendKind? RequestedBackend,
    bool ValidateOnly)
{
    public WorkflowPreviewSimulationPlan PreviewSimulationPlan { get; init; } = WorkflowPreviewSimulationPlan.Empty;
}

public sealed record WorkflowTestRunResult(
    bool Succeeded,
    WorkflowValidationResult Validation,
    WorkflowRunSnapshot? Run,
    IReadOnlyList<WorkflowEventRecord> Events,
    IReadOnlyList<WorkflowArtifactRecord> Artifacts,
    IReadOnlyList<WorkflowExternalRequestRecord> PendingExternalRequests,
    string ErrorMessage)
{
    public IReadOnlyList<WorkflowCheckpointRecord> Checkpoints { get; init; } = [];
}
