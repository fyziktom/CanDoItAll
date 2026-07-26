using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface ISandboxWorkspaceCatalogStore
{
    Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default);
    Task SaveCatalogAsync(SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceExecutionStore
{
    Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(CancellationToken cancellationToken = default);
    Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken = default);
    Task SaveExecutionAsync(SandboxWorkspaceExecutionState executionState, CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
        Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
        Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceExecutionRunStore
{
    Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        CancellationToken cancellationToken = default);

    Task<ExecutionRunRecord?> GetExecutionRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
        ExecutionRunDetail detail,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceExecutionRunMutationStore
{
    Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceStore : ISandboxWorkspaceCatalogStore, ISandboxWorkspaceExecutionStore
{
    Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SandboxWorkspaceDocument document, CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceChatQueryStore
{
    Task<IReadOnlyList<ChatSessionSummaryRecord>> ListChatSessionSummariesAsync(
        Guid? agentId = null,
        CancellationToken cancellationToken = default);

    Task<ChatSessionRecord?> GetChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatRunSummaryRecord>> ListChatRunSummariesAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default);

    Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceChatProjectionQueryStore
{
    Task<ChatWorkspaceProjectionSnapshot> LoadChatWorkspaceProjectionAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}

public sealed record ChatWorkspaceProjectionSnapshot(
    IReadOnlyList<ChatSessionSummaryRecord> SessionSummaries,
    IReadOnlyList<ChatRunSummaryRecord> RunSummaries);

public interface ISandboxWorkspaceChatSessionStore
{
    Task<ChatSessionRecord> CreateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default);

    Task<ChatSessionRecord> UpdateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default);
}

public sealed record AgentImportResult(
    AgentDefinition Agent,
    IReadOnlyList<ChatSessionRecord> Sessions,
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics,
    IReadOnlyList<AgentMemoryRecord> Memory,
    IReadOnlyList<ProviderProfile> Providers,
    IReadOnlyList<CapabilityCatalogItem> Capabilities)
{
    public IReadOnlyList<ExecutionRunRecord> Runs { get; init; } = [];
    public IReadOnlyList<ExecutionApprovalRecord> Approvals { get; init; } = [];
    public IReadOnlyList<ExecutionArtifactRecord> Artifacts { get; init; } = [];
    public IReadOnlyList<ExecutionWorkflowCheckpointRecord> Checkpoints { get; init; } = [];
    public IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts { get; init; } = [];
    public string PackageSha256 { get; init; } = string.Empty;
    public string PackageSchemaVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class AgentPackageValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IAgentPackageService
{
    Task<AgentExportResult> ExportAsync(
        SandboxWorkspaceDocument document,
        AgentDefinition agent,
        CancellationToken cancellationToken = default);

    Task<AgentImportResult> ImportAsync(string packagePath, CancellationToken cancellationToken = default);

    async Task<AgentImportResult> ImportAsync(
        Stream package,
        AgentPackageReadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(options);

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"agent-package-import-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var target = File.Create(temporaryPath))
            {
                await package.CopyToAsync(target, cancellationToken);
            }

            return await ImportAsync(temporaryPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public sealed record AgentRuntimeResponse(
    string ResponseText,
    int InputTokens,
    int OutputTokens,
    int ToolCalls,
    string RuntimeSessionKey,
    string? SerializedSessionStateJson,
    IReadOnlyList<PendingToolApprovalRecord> PendingApprovals)
{
    public int CachedInputTokens { get; init; }

    public IReadOnlyList<AgentFinalizerInvocation> FinalizerInvocations { get; init; } = [];

    public IReadOnlyList<AgentToolInvocationTrace> ToolInvocationTraces { get; init; } = [];

    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; init; } = [];

    public AgentRuntimeContextAssemblyManifest? ContextAssemblyManifest { get; init; }

    public IReadOnlyList<AgentContextContributionTrace> ContextContributionTraces { get; init; } = [];
}

public sealed class AgentRuntimeUsageException : Exception
{
    public AgentRuntimeUsageException(
        string message,
        Exception innerException,
        IReadOnlyList<ProviderUsageObservation> usageObservations)
        : base(message, innerException)
    {
        UsageObservations = usageObservations;
    }

    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; }
}

public interface IAgentRuntime
{
    Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentRuntimeResponse> RunAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        string prompt,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null);

    Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        ChatSessionRecord session,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        bool approved,
        string? runtimeSessionKey,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken = default,
        bool suppressApprovalRequirements = false,
        AgentStructuredOutputContract? structuredOutput = null,
        AgentRuntimeExecutionOptions? executionOptions = null);
}

public interface ICapabilityProofService
{
    Task<CapabilityVerificationResult> VerifyAsync(
        AgentDefinition agent,
        ProviderProfile? provider,
        CapabilityCatalogItem capability,
        CancellationToken cancellationToken = default);
}

public sealed record ProviderCredentialResolution(
    string ApiKey,
    string ResolutionSource,
    string FailureMessage,
    bool ShouldPromoteToProcessEnvironment = true)
{
    public bool IsResolved => !string.IsNullOrWhiteSpace(ApiKey);
}

public interface IAgentProviderCredentialResolver
{
    // Enterprise hosts should replace this bridge instead of moving secret-store ownership into AgentFramework.
    ProviderCredentialResolution Resolve(ProviderProfile provider);
}

public interface IProviderProfileService
{
    ProviderProfileEditorModel CreateEditor(ProviderProfile? provider = null);
    ProviderProfile CreateProfile(ProviderProfileEditorModel model, ProviderProfile? current = null);
    ProviderProfile NormalizeImportedProfile(ProviderProfile provider);
    string GetIdentityKey(ProviderProfile provider);
    ProviderProfile ApplyHealthResult(ProviderProfile provider, ProviderHealthResult result, DateTimeOffset checkedAtUtc);
    ProviderProfile ApplyProviderModelMaintenanceResult(ProviderProfile provider, ProviderModelMaintenanceEditorResult result, DateTimeOffset checkedAtUtc);
    ProviderFeatureMatrix ResolveFeatureMatrix(ProviderProfile provider);
    ProviderFeatureMatrix ResolveFeatureMatrixForModel(ProviderProfile provider, string? model);
}

public interface IProviderProfileRegistry
{
    Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default);

    Task<ProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> SaveProviderAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default);

    Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    Task<ProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<ProviderProfile, ProviderProfile> update,
        CancellationToken cancellationToken = default);
}

public interface IProviderDiagnosticsService
{
    Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default);

    Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default);

    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAgentExecutionGovernanceBridge
{
    Task OnApprovalsRequestedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default);

    Task OnApprovalsDecidedAsync(
        ExecutionRunRecord run,
        IReadOnlyList<ExecutionApprovalRecord> approvals,
        CancellationToken cancellationToken = default);
}

public interface IAgentExecutionEventSink
{
    Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default);
}

public interface IAgentExecutionCheckpointBridge
{
    Task<ExecutionWorkflowCheckpointRecord?> CapturePendingApprovalCheckpointAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default);

    Task<ExecutionWorkflowCheckpointRecord?> ValidatePendingApprovalResumeAsync(
        ExecutionRunRecord run,
        CancellationToken cancellationToken = default);

    Task MarkCheckpointResumedAsync(
        Guid executionRunId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IAgentExecutionHistoryReader
{
    Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default);
    Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default);
}

public interface IAgentFrameworkWorkspaceService : IAgentExecutionHistoryReader
{
    event EventHandler<ExecutionLogEntry>? ExecutionUpdated;

    Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default);
    Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default);
    Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default);
    Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default);
    Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default);
    Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default);
    Task GrantAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This workspace does not support durable project-structure access grants.");
    }
    Task RevokeAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This workspace does not support durable project-structure access revocation.");
    }
    Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default);
    Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default);
    Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default);
    Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default);
    Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default);
    Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default);
    Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default);

    Task<AgentPackageImportReceipt> ImportAgentPackageAsync(
        Stream package,
        AgentPackageImportCommand command,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Remote agent package import is not supported by this workspace service.");
    Task<AgentExternalProvisioningResource> GetAgentByExternalKeyAsync(
        string externalNamespace,
        string key,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("External-key agent lookup is not supported by this workspace service.");
    Task<AgentExternalProvisioningReceipt> ProvisionAgentByExternalKeyAsync(
        AgentExternalProvisioningCommand command,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("External-key agent provisioning is not supported by this workspace service.");
    Task<AgentExternalProvisioningReceipt> ArchiveAgentByExternalKeyAsync(
        AgentExternalArchiveCommand command,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("External-key agent archive is not supported by this workspace service.");
    Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default);
    Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default);
    Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default);
    Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default);
    Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default);
    Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default);
    Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default);
    Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default);
    Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default);
    Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default);
    Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default);
    Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default);
    Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "This workspace does not support atomic same-source execution reservation.");
    }
    Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default);
    Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null,
        AgentChatRunOptions? options = null);
    Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        bool approved,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default);
    Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default);
    Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default);
}
