using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface ISandboxWorkspaceCatalogStore
{
    Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken = default);
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

public interface IAgentProviderUsageEvidenceStore
{
    Task<AgentProviderUsageEvidence> LoadProviderUsageEvidenceAsync(
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

public interface ISandboxWorkspaceChatRunStartStore
{
    Task<ChatBackedRunStartResult> BeginChatBackedRunAsync(
        ChatBackedRunStartRequest request,
        Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create,
        CancellationToken cancellationToken = default);
}

public sealed record ChatBackedRunStartRequest
{
    public ChatBackedRunStartRequest(
        Guid agentId,
        Guid expectedAgentProviderProfileId,
        CatalogDataRevision expectedCatalogRevision,
        Guid? chatSessionId)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException(
                "An agent identifier is required.",
                nameof(agentId));
        }

        if (expectedAgentProviderProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "An expected agent provider profile identifier is required.",
                nameof(expectedAgentProviderProfileId));
        }

        if (!expectedCatalogRevision.IsAssigned)
        {
            throw new ArgumentException(
                "An assigned expected catalog revision is required.",
                nameof(expectedCatalogRevision));
        }

        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A selected chat session identifier cannot be empty.",
                nameof(chatSessionId));
        }

        AgentId = agentId;
        ExpectedAgentProviderProfileId = expectedAgentProviderProfileId;
        ExpectedCatalogRevision = expectedCatalogRevision;
        ChatSessionId = chatSessionId;
    }

    public Guid AgentId { get; }

    public Guid ExpectedAgentProviderProfileId { get; }

    public CatalogDataRevision ExpectedCatalogRevision { get; }

    public Guid? ChatSessionId { get; }
}

public sealed record ChatBackedRunStartContext(
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentDefinition Agent,
    ChatSessionRecord? Session);

public sealed record ChatBackedRunStartMutation(
    ExecutionRunDetail Detail,
    ChatMessageRecord UserMessage);

public abstract record ChatBackedRunStartResult(
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentDefinition Agent);

public sealed record ChatBackedRunStarted(
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentDefinition Agent,
    ExecutionRunDetail Detail,
    ChatMessageRecord UserMessage)
    : ChatBackedRunStartResult(CatalogSnapshot, Agent);

public sealed record ChatBackedRunBlocked(
    SandboxWorkspaceCatalogSnapshot CatalogSnapshot,
    AgentDefinition Agent,
    ChatSessionRecord Session,
    ExecutionRunRecord BlockingRun)
    : ChatBackedRunStartResult(CatalogSnapshot, Agent);

public interface ISandboxWorkspaceExecutionRunMutationStore
{
    Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default);

    Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceStore :
    ISandboxWorkspaceCatalogStore,
    ISandboxWorkspaceExecutionStore,
    ISandboxWorkspaceAgentDeletionStore,
    IAgentExecutionReportReader
{
    Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocument> SaveAsync(
        SandboxWorkspaceDocument document,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        CancellationToken cancellationToken = default);
    Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceAgentDeletionStore
{
    Task<AgentWorkspaceDeletionResult> DeleteAgentWorkspaceDataAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}

public sealed record AgentWorkspaceDeletionResult(
    bool Deleted,
    int DeletedChatSessionCount,
    int DeletedExecutionRunCount);

public enum AgentDeletionConflictKind
{
    ManagedSeedAgent,
    ActiveExecution
}

public sealed class AgentDeletionConflictException(
    Guid agentId,
    AgentDeletionConflictKind kind,
    string message) : InvalidOperationException(message)
{
    public Guid AgentId { get; } = agentId;

    public AgentDeletionConflictKind Kind { get; } = kind;
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

public interface IAgentExecutionReportReader
{
    Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken = default);
}

public interface ISandboxWorkspaceChatProjectionQueryStore :
    IAgentExecutionReportReader
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
    string FailureMessage)
{
    public bool IsResolved => !string.IsNullOrWhiteSpace(ApiKey);
}

public interface IAgentProviderCredentialResolver
{
    // Enterprise hosts should replace this bridge instead of moving secret-store ownership into AgentFramework.
    ProviderCredentialResolution Resolve(ProviderProfile provider);
}

public interface IAgentProviderCredentialDispatchScope : IDisposable
{
    ProviderCredentialResolution Resolve(ProviderProfile provider);
}

public interface IAgentProviderCredentialDispatchScopePreparation : IDisposable
{
    IAgentProviderCredentialDispatchScope BeginScope();
}

public interface IAgentProviderCredentialDispatchScopeFactory
{
    ValueTask<IAgentProviderCredentialDispatchScopePreparation> PrepareAsync(
        IReadOnlyList<ProviderProfile> providers,
        CancellationToken cancellationToken = default);
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

public interface IAgentFrameworkWorkspaceService :
    IAgentExecutionHistoryReader,
    IAgentExecutionReportReader
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
    Task<int> RevokeProjectStructureAccessFromAllAgentsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("This workspace does not support bulk durable project-structure access revocation.");
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
    /// <summary>
    /// Continues an execution run with one decision per currently pending approval. Every
    /// pending approval must receive exactly one decision — a partial, duplicate, or unknown-id
    /// set is rejected with <see cref="AgentApprovalDecisionMismatchException"/> rather than
    /// silently applied.
    /// </summary>
    Task<ExecutionRunResult> ContinueExecutionRunAsync(
        Guid executionRunId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
    Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null);
    /// <summary>Per-proposal decision overload — see <see cref="ContinueExecutionRunAsync(Guid,AgentExecutionOperationId,IReadOnlyList{PendingToolApprovalDecision},bool,CancellationToken)"/>.</summary>
    Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default);
    Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default);
    Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default);
}
