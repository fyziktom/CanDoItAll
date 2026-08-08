using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class CurrentProfileAgentFrameworkWorkspaceService :
    IAgentFrameworkWorkspaceService,
    IAgentFrameworkWorkspaceActivityExecutionService,
    IDisposable
{
    private readonly ICanDoItAllAgentWorkspaceFactory workspaceFactory;
    private readonly IAiTechnicalAgentBridge technicalAgentBridge;
    private readonly IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator;
    private readonly IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor;
    private readonly IDatabaseSwitchNotificationService databaseSwitchNotificationService;
    private readonly IAgentExecutionActivityCoordinator activityCoordinator;
    private readonly IAgentExecutionProfileGenerationSource executionProfileGenerationSource;
    private readonly ILogger<CurrentProfileAgentFrameworkWorkspaceService> logger;
    private readonly IsolatedCompatibilityEventDispatcher<ExecutionLogEntry>
        executionUpdatedDispatcher;
    private readonly Lock executionSubscriptionGate = new();
    private IAgentFrameworkWorkspaceService? subscribedService;
    private EventHandler<ExecutionLogEntry>? executionUpdated;
    private bool disposed;

    public CurrentProfileAgentFrameworkWorkspaceService(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator,
        IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
        IDatabaseSwitchNotificationService databaseSwitchNotificationService,
        IAgentExecutionActivityCoordinator activityCoordinator,
        IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
        ILogger<CurrentProfileAgentFrameworkWorkspaceService> logger)
    {
        this.workspaceFactory = workspaceFactory;
        this.technicalAgentBridge = technicalAgentBridge;
        this.referenceDataCacheInvalidator = referenceDataCacheInvalidator;
        this.databaseProfileRuntimeAccessor = databaseProfileRuntimeAccessor;
        this.databaseSwitchNotificationService = databaseSwitchNotificationService;
        this.activityCoordinator = activityCoordinator;
        this.executionProfileGenerationSource = executionProfileGenerationSource;
        this.logger = logger;
        executionUpdatedDispatcher = CreateExecutionUpdatedDispatcher(logger);
        databaseSwitchNotificationService.Changed += HandleDatabaseProfileChanged;
    }

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (executionSubscriptionGate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                var service = ResolveCurrentWorkspaceService();
                executionUpdatedDispatcher.Subscribe(value);
                executionUpdated += value;
                UpdateExecutionSubscription(service);
            }
        }
        remove
        {
            if (value is null)
            {
                return;
            }

            lock (executionSubscriptionGate)
            {
                if (disposed)
                {
                    return;
                }

                executionUpdated -= value;
                executionUpdatedDispatcher.Unsubscribe(value);
                if (executionUpdated is null)
                {
                    DetachExecutionSubscription();
                }
            }
        }
    }

    public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().GetDashboardAsync(cancellationToken);
    }

    public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().GetAgentOverviewAsync(cancellationToken);
    }

    public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().GetAgentUsageDetailsAsync(cancellationToken);
    }

    public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().GetProviderUsageDetailsAsync(cancellationToken);
    }

    public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().GetModelUsageDetailsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListAgentsAsync(includeTemplates, cancellationToken);
    }

    public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetAgentEditorAsync(agentId, cancellationToken);
    }

    public async Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default)
    {
        var agentId = await ResolveService().SaveAgentAsync(model, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(
            cancellationToken,
            exception => new AgentDirectoryProjectionSynchronizationException(agentId, exception));

        return agentId;
    }

    public async Task GrantAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await ResolveService().GrantAgentProjectStructureAccessAsync(agentId, projectId, cancellationToken);
        await RefreshProjectStructureAccessProjectionsAsync(
            agentId,
            projectId,
            ProjectStructureAccessChange.Granted,
            cancellationToken);
    }

    public async Task RevokeAgentProjectStructureAccessAsync(
        Guid agentId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        await ResolveService().RevokeAgentProjectStructureAccessAsync(agentId, projectId, cancellationToken);
        await RefreshProjectStructureAccessProjectionsAsync(
            agentId,
            projectId,
            ProjectStructureAccessChange.Revoked,
            cancellationToken);
    }

    public async Task<int> RevokeProjectStructureAccessFromAllAgentsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var changedAgentCount = await ResolveService()
            .RevokeProjectStructureAccessFromAllAgentsAsync(projectId, cancellationToken);
        await RefreshBulkProjectStructureAccessProjectionsAsync(
            projectId,
            changedAgentCount,
            cancellationToken);
        return changedAgentCount;
    }

    public async Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await ResolveService().DeleteAgentAsync(agentId, cancellationToken);
        await RefreshDirectoryProjectionAfterCommittedAgentDeletionAsync(
            agentId,
            cancellationToken);
    }

    public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().ListAgentTeamsAsync(cancellationToken);
    }

    public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetAgentTeamEditorAsync(teamId, cancellationToken);
    }

    public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default)
    {
        return ResolveService().SaveAgentTeamAsync(model, cancellationToken);
    }

    public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default)
    {
        return ResolveService().UpdateAgentTeamMembersAsync(teamId, agentIds, cancellationToken);
    }

    public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        return ResolveService().DeleteAgentTeamAsync(teamId, cancellationToken);
    }

    public async Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default)
    {
        var cloneId = await ResolveService().CloneAgentAsync(agentId, cloneName, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        return cloneId;
    }

    public async Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default)
    {
        var templateId = await ResolveService().ConvertToTemplateAsync(agentId, templateKey, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        return templateId;
    }

    public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ExportAgentAsync(agentId, cancellationToken);
    }

    public async Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        var agentId = await ResolveService().ImportAgentAsync(packagePath, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        return agentId;
    }

    public async Task<AgentPackageImportReceipt> ImportAgentPackageAsync(
        Stream package,
        AgentPackageImportCommand command,
        CancellationToken cancellationToken = default)
    {
        var receipt = await ResolveService().ImportAgentPackageAsync(package, command, cancellationToken);
        if (!receipt.Replayed)
        {
            await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        }

        return receipt;
    }

    public Task<AgentExternalProvisioningResource> GetAgentByExternalKeyAsync(
        string externalNamespace,
        string key,
        CancellationToken cancellationToken = default)
    {
        return ResolveService().GetAgentByExternalKeyAsync(
            externalNamespace,
            key,
            cancellationToken);
    }

    public async Task<AgentExternalProvisioningReceipt> ProvisionAgentByExternalKeyAsync(
        AgentExternalProvisioningCommand command,
        CancellationToken cancellationToken = default)
    {
        var receipt = await ResolveService().ProvisionAgentByExternalKeyAsync(
            command,
            cancellationToken);
        if (!receipt.Replayed)
        {
            await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        }

        return receipt;
    }

    public async Task<AgentExternalProvisioningReceipt> ArchiveAgentByExternalKeyAsync(
        AgentExternalArchiveCommand command,
        CancellationToken cancellationToken = default)
    {
        var receipt = await ResolveService().ArchiveAgentByExternalKeyAsync(
            command,
            cancellationToken);
        if (!receipt.Replayed)
        {
            await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        }

        return receipt;
    }

    public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().ListProvidersAsync(cancellationToken);
    }

    public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetProviderEditorAsync(providerId, cancellationToken);
    }

    public async Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        var providerId = await ResolveService().SaveProviderAsync(model, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        return providerId;
    }

    public async Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        await ResolveService().DeleteProviderAsync(providerId, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
    }

    public async Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var result = await ResolveService().TestProviderAsync(providerId, cancellationToken);
        referenceDataCacheInvalidator.Invalidate();
        return result;
    }

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default)
    {
        return ResolveService().RunProviderTestChatAsync(providerId, request, cancellationToken);
    }

    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(Guid providerId, ProviderModelMaintenanceEditorRequest request, CancellationToken cancellationToken = default)
    {
        var result = await ResolveService().CreateOrUpdateProviderModelAsync(providerId, request, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
        return result;
    }

    public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        return ResolveService().ListCapabilitiesAsync(cancellationToken);
    }

    public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetCapabilityEditorAsync(capabilityId, cancellationToken);
    }

    public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default)
    {
        return ResolveService().SaveCapabilityAsync(model, cancellationToken);
    }

    public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default)
    {
        return ResolveService().DeleteCapabilityAsync(capabilityId, cancellationToken);
    }

    public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
    {
        return ResolveService().VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
    }

    public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListChatSessionsAsync(agentId, cancellationToken);
    }

    public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetChatPageBootstrapAsync(includeTemplates, cancellationToken);
    }

    public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetChatAgentWorkspaceAsync(agentId, preferredSessionId, cancellationToken);
    }

    public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken);
    }

    public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default)
    {
        return ResolveService().RenameChatSessionAsync(agentId, chatSessionId, title, cancellationToken);
    }

    public Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteNewActivityOperationAsync(
            request.InitialActivityOperationId,
            request.AgentId,
            request.ChatSessionId,
            "Agent execution request accepted.",
            (service, operation) => service.ExecuteRunWithinOperationAsync(
                operation,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunResult> ExecuteRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchPinnedActivityOperation(
            operation,
            request.AgentId,
            request.ChatSessionId,
            request.InitialActivityOperationId,
            service => service.ExecuteRunWithinOperationAsync(
                operation,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteNewActivityOperationAsync(
            request.InitialActivityOperationId,
            request.AgentId,
            request.ChatSessionId,
            "Source execution request accepted.",
            (service, operation) => service.ExecuteSameSourceRunWithinOperationAsync(
                operation,
                source,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunSourceExecutionResult> ExecuteSameSourceRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        ExecutionRunSourceKey source,
        ExecutionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DispatchPinnedActivityOperation(
            operation,
            request.AgentId,
            request.ChatSessionId,
            request.InitialActivityOperationId,
            service => service.ExecuteSameSourceRunWithinOperationAsync(
                operation,
                source,
                request,
                cancellationToken));
    }

    public Task<ExecutionRunResult> ContinueExecutionRunAsync(
        Guid executionRunId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return ExecuteNewActivityOperationAsync(
            activityOperationId,
            agentId: null,
            chatSessionId: null,
            "Execution continuation accepted.",
            (service, operation) => service.ContinueExecutionRunWithinOperationAsync(
                operation,
                executionRunId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ExecuteNewActivityOperationAsync(
            options.InitialActivityOperationId,
            agentId,
            chatSessionId,
            "Agent chat request accepted.",
            (service, operation) => service.SendMessageWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                prompt,
                options,
                cancellationToken,
                attachmentPaths));
    }

    public Task<AgentChatRunResult> SendMessageWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        AgentChatRunOptions options,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        return DispatchPinnedActivityOperation(
            operation,
            agentId,
            chatSessionId,
            options.InitialActivityOperationId,
            service => service.SendMessageWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                prompt,
                options,
                cancellationToken,
                attachmentPaths));
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
        Guid agentId,
        Guid chatSessionId,
        AgentExecutionOperationId activityOperationId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return ExecuteNewActivityOperationAsync(
            activityOperationId,
            agentId,
            chatSessionId,
            "Approval response accepted.",
            (service, operation) => service.RespondToPendingApprovalsWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<ExecutionRunResult> ContinueExecutionRunWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid executionRunId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(decisions);
        return DispatchPinnedActivityOperation(
            operation,
            expectedAgentId: null,
            expectedChatSessionId: null,
            operation.StreamId.OperationId,
            service => service.ContinueExecutionRunWithinOperationAsync(
                operation,
                executionRunId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsWithinOperationAsync(
        IAgentExecutionActivityOperationLease operation,
        Guid agentId,
        Guid chatSessionId,
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        bool autoApprovePendingToolCalls = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return DispatchPinnedActivityOperation(
            operation,
            agentId,
            chatSessionId,
            operation.StreamId.OperationId,
            service => service.RespondToPendingApprovalsWithinOperationAsync(
                operation,
                agentId,
                chatSessionId,
                decisions,
                autoApprovePendingToolCalls,
                cancellationToken));
    }

    public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListExecutionLogAsync(agentId, chatSessionId, cancellationToken);
    }

    public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);
    }

    public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListMetricsAsync(agentId, cancellationToken);
    }

    public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListMemoryAsync(agentId, cancellationToken);
    }

    public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default)
    {
        return ResolveService().SaveMemoryAsync(model, cancellationToken);
    }

    public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        return ResolveService().DeleteMemoryAsync(memoryId, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListExecutionRunsAsync(query, cancellationToken);
    }

    public Task<AgentExecutionReportPage> QueryExecutionReportAsync(
        AgentExecutionReportQuery query,
        CancellationToken cancellationToken = default)
    {
        return ResolveService().QueryExecutionReportAsync(query, cancellationToken);
    }

    public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default)
    {
        return ResolveService().GetExecutionRunDetailAsync(executionRunId, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListExecutionArtifactsAsync(executionRunId, cancellationToken);
    }

    public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListExecutionWorkflowCheckpointsAsync(executionRunId, cancellationToken);
    }

    public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default)
    {
        return ResolveService().ListToolExecutionReceiptsAsync(executionRunId, cancellationToken);
    }

    private IAgentFrameworkWorkspaceService ResolveService()
    {
        lock (executionSubscriptionGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var service = ResolveCurrentWorkspaceService();
            UpdateExecutionSubscription(service);
            return service;
        }
    }

    private Task<TResult> ExecuteNewActivityOperationAsync<TResult>(
        AgentExecutionOperationId operationId,
        Guid? agentId,
        Guid? chatSessionId,
        string acceptedMessage,
        Func<
            IAgentFrameworkWorkspaceActivityExecutionService,
            IAgentExecutionActivityOperationLease,
            Task<TResult>> dispatch)
    {
        EnsureRequiredActivityOperationId(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedMessage);
        ArgumentNullException.ThrowIfNull(dispatch);

        IAgentExecutionActivityOperationLease operation;
        Task<TResult> completion;
        lock (executionSubscriptionGate)
        {
            var pinnedIdentity = ResolvePinnedActivityExecutionIdentity();
            var streamId = new AgentExecutionActivityStreamId(
                pinnedIdentity.ProfileId,
                pinnedIdentity.WorkspaceScope,
                pinnedIdentity.ProfileGeneration,
                operationId);
            operation = activityCoordinator.AdmitOperation(
                streamId,
                agentId,
                chatSessionId,
                acceptedMessage) switch
            {
                AgentExecutionActivityAdmitted admitted => admitted.Operation,
                AgentExecutionActivityRejected rejected =>
                    throw new AgentExecutionActivityAdmissionException(
                        rejected.StreamId,
                        rejected.Reason),
                _ => throw new InvalidOperationException(
                    "The activity coordinator returned an unknown admission result.")
            };

            try
            {
                var pinnedService = ResolvePinnedActivityExecutionService(
                    pinnedIdentity);
                completion = dispatch(
                    pinnedService.Service,
                    operation);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    TerminalizeOwnedCancellation(operation);
                }
                finally
                {
                    operation.Dispose();
                }

                throw;
            }
            catch
            {
                try
                {
                    TerminalizeOwnedFailure(operation);
                }
                finally
                {
                    operation.Dispose();
                }

                throw;
            }
        }

        return AwaitOwnedActivityOperationAsync(
            operation,
            completion);
    }

    private static async Task<TResult> AwaitOwnedActivityOperationAsync<TResult>(
        IAgentExecutionActivityOperationLease operation,
        Task<TResult> completion)
    {
        try
        {
            var result = await completion.ConfigureAwait(false);
            if (!operation.IsTerminal)
            {
                TerminalizeOwnedFailure(operation);
                throw new InvalidOperationException(
                    "The workspace execution completed without a terminal activity outcome.");
            }

            return result;
        }
        catch (AgentExecutionActivityPublicationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            TerminalizeOwnedCancellation(operation);
            throw;
        }
        catch
        {
            TerminalizeOwnedFailure(operation);
            throw;
        }
        finally
        {
            operation.Dispose();
        }
    }

    private TResult DispatchPinnedActivityOperation<TResult>(
        IAgentExecutionActivityOperationLease operation,
        Guid? expectedAgentId,
        Guid? expectedChatSessionId,
        AgentExecutionOperationId operationId,
        Func<IAgentFrameworkWorkspaceActivityExecutionService, TResult> dispatch)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(dispatch);

        lock (executionSubscriptionGate)
        {
            var pinnedService = ResolvePinnedActivityExecutionService();
            EnsureCurrentActivityAccess(
                operation,
                pinnedService,
                expectedAgentId,
                expectedChatSessionId,
                operationId);
            return dispatch(pinnedService.Service);
        }
    }

    public void Dispose()
    {
        lock (executionSubscriptionGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            executionUpdated = null;
            DetachExecutionSubscription();
            databaseSwitchNotificationService.Changed -= HandleDatabaseProfileChanged;
            executionUpdatedDispatcher.Dispose();
        }
    }

    private PinnedActivityExecutionService ResolvePinnedActivityExecutionService()
    {
        return ResolvePinnedActivityExecutionService(
            ResolvePinnedActivityExecutionIdentity());
    }

    private PinnedActivityExecutionService ResolvePinnedActivityExecutionService(
        PinnedActivityExecutionIdentity expectedIdentity)
    {
        EnsurePinnedActivityExecutionIdentity(
            expectedIdentity,
            ResolvePinnedActivityExecutionIdentity());
        var workspaceService = workspaceFactory.GetWorkspaceService(
            expectedIdentity.WorkspaceScope);
        var confirmedIdentity = ResolvePinnedActivityExecutionIdentity();
        EnsurePinnedActivityExecutionIdentity(
            expectedIdentity,
            confirmedIdentity);

        UpdateExecutionSubscription(workspaceService);
        var activityService =
            workspaceService as IAgentFrameworkWorkspaceActivityExecutionService
            ?? throw new NotSupportedException(
                "The current agent workspace does not support operation-bound execution.");
        return new PinnedActivityExecutionService(
            confirmedIdentity.ProfileId,
            confirmedIdentity.WorkspaceScope,
            confirmedIdentity.ProfileGeneration,
            activityService);
    }

    private PinnedActivityExecutionIdentity ResolvePinnedActivityExecutionIdentity()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var firstProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var firstProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        var workspaceScope = workspaceFactory.GetOrganizationScope();
        var confirmedProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var confirmedProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        var expectedScope = WorkspaceScopeDescriptor.Organization(
            confirmedProfile.Id.ToString("N"));
        if (firstProfile.Id != confirmedProfile.Id ||
            !string.Equals(
                firstProfile.Runtime.Fingerprint,
                confirmedProfile.Runtime.Fingerprint,
                StringComparison.Ordinal) ||
            firstProfileGeneration != confirmedProfileGeneration ||
            workspaceScope != expectedScope)
        {
            throw new InvalidOperationException(
                "The current database profile changed while the agent operation was being dispatched.");
        }

        return new PinnedActivityExecutionIdentity(
            confirmedProfile.Id,
            workspaceScope,
            confirmedProfileGeneration);
    }

    private static void EnsurePinnedActivityExecutionIdentity(
        PinnedActivityExecutionIdentity expectedIdentity,
        PinnedActivityExecutionIdentity actualIdentity)
    {
        if (actualIdentity != expectedIdentity)
        {
            throw new InvalidOperationException(
                "The current database profile changed while the agent operation was being dispatched.");
        }
    }

    private void EnsureCurrentActivityAccess(
        IAgentExecutionActivityOperationLease operation,
        PinnedActivityExecutionService pinnedService,
        Guid? expectedAgentId,
        Guid? expectedChatSessionId,
        AgentExecutionOperationId operationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        AgentExecutionActivityAccessPolicy.EnsureAuthorized(
            operation.StreamId,
            pinnedService.ProfileId,
            pinnedService.ProfileGeneration);
        if (operation.StreamId.WorkspaceScope != pinnedService.WorkspaceScope)
        {
            throw new AgentExecutionActivityAccessException(
                AgentExecutionActivityAccessRejectionReason.WorkspaceScopeMismatch,
                "The activity operation belongs to a different workspace scope.");
        }

        if (expectedAgentId.HasValue &&
            operation.AgentId != expectedAgentId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different agent.");
        }

        if (operationId.Value == Guid.Empty ||
            operation.StreamId.OperationId != operationId)
        {
            throw new InvalidOperationException(
                "The activity operation does not match the execution request.");
        }

        if (expectedChatSessionId.HasValue &&
            operation.ChatSessionId != expectedChatSessionId)
        {
            throw new InvalidOperationException(
                "The activity operation belongs to a different chat session.");
        }
    }

    private static void EnsureRequiredActivityOperationId(
        AgentExecutionOperationId operationId)
    {
        if (operationId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "An activity operation id is required.",
                nameof(operationId));
        }
    }

    private static void TerminalizeOwnedCancellation(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Cancel("The agent operation was cancelled.");
        }
    }

    private static void TerminalizeOwnedFailure(
        IAgentExecutionActivityOperationLease operation)
    {
        if (!operation.IsTerminal)
        {
            operation.Fail(
                "The agent operation failed.",
                AgentExecutionActivityFailureCodes.UnhandledExecutionFailure);
        }
    }

    private async Task SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(
        CancellationToken cancellationToken,
        Func<Exception, Exception>? synchronizationExceptionFactory = null)
    {
        referenceDataCacheInvalidator.Invalidate();
        try
        {
            await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (synchronizationExceptionFactory is not null)
        {
            throw synchronizationExceptionFactory(exception);
        }

        referenceDataCacheInvalidator.Invalidate();
    }

    private async Task RefreshDirectoryProjectionAfterCommittedAgentDeletionAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        TryInvalidateReferenceDataAfterAgentDeletion(agentId);
        try
        {
            await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Agent {AgentId} was deleted from the canonical catalog, but agent-directory projection synchronization failed.",
                agentId);
            return;
        }

        TryInvalidateReferenceDataAfterAgentDeletion(agentId);
    }

    private void TryInvalidateReferenceDataAfterAgentDeletion(Guid agentId)
    {
        try
        {
            referenceDataCacheInvalidator.Invalidate();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Agent {AgentId} was deleted from the canonical catalog, but reference-data cache invalidation failed.",
                agentId);
        }
    }

    private async Task RefreshProjectStructureAccessProjectionsAsync(
        Guid agentId,
        Guid projectId,
        ProjectStructureAccessChange change,
        CancellationToken cancellationToken)
    {
        InvalidateProjectStructureAccessReferenceData(agentId, projectId, change);

        try
        {
            await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Project-structure access was {AccessChange} in the catalog for agent {AgentId} and project {ProjectId}, but agent-directory projection synchronization failed.",
                change,
                agentId,
                projectId);
            return;
        }

        InvalidateProjectStructureAccessReferenceData(agentId, projectId, change);
    }

    private async Task RefreshBulkProjectStructureAccessProjectionsAsync(
        Guid projectId,
        int changedAgentCount,
        CancellationToken cancellationToken)
    {
        TryInvalidateBulkProjectStructureAccessReferenceData(projectId, changedAgentCount);
        try
        {
            await technicalAgentBridge.SynchronizeDirectoryProjectionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Project-structure access for project {ProjectId} was revoked from {ChangedAgentCount} catalog agents, but agent-directory projection synchronization failed.",
                projectId,
                changedAgentCount);
            return;
        }

        TryInvalidateBulkProjectStructureAccessReferenceData(projectId, changedAgentCount);
    }

    private void TryInvalidateBulkProjectStructureAccessReferenceData(
        Guid projectId,
        int changedAgentCount)
    {
        try
        {
            referenceDataCacheInvalidator.Invalidate();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Project-structure access for project {ProjectId} was revoked from {ChangedAgentCount} catalog agents, but reference-data cache invalidation failed.",
                projectId,
                changedAgentCount);
        }
    }

    private void InvalidateProjectStructureAccessReferenceData(
        Guid agentId,
        Guid projectId,
        ProjectStructureAccessChange change)
    {
        try
        {
            referenceDataCacheInvalidator.Invalidate();
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Project-structure access was {AccessChange} in the catalog for agent {AgentId} and project {ProjectId}, but reference-data cache invalidation failed.",
                change,
                agentId,
                projectId);
        }
    }

    private IAgentFrameworkWorkspaceService ResolveCurrentWorkspaceService()
    {
        return workspaceFactory.GetWorkspaceService(workspaceFactory.GetOrganizationScope());
    }

    private void UpdateExecutionSubscription(IAgentFrameworkWorkspaceService service)
    {
        if (ReferenceEquals(subscribedService, service))
        {
            return;
        }

        DetachExecutionSubscription();
        if (executionUpdated is not null)
        {
            service.ExecutionUpdated += HandleExecutionUpdated;
            subscribedService = service;
        }
    }

    private void DetachExecutionSubscription()
    {
        if (subscribedService is null)
        {
            return;
        }

        subscribedService.ExecutionUpdated -= HandleExecutionUpdated;
        subscribedService = null;
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        lock (executionSubscriptionGate)
        {
            if (disposed ||
                !ReferenceEquals(sender, subscribedService))
            {
                return;
            }

            executionUpdatedDispatcher.Publish(this, entry);
        }
    }

    private static IsolatedCompatibilityEventDispatcher<ExecutionLogEntry>
        CreateExecutionUpdatedDispatcher(
            ILogger<CurrentProfileAgentFrameworkWorkspaceService> logger)
    {
        return new(
            failure => logger.LogWarning(
                failure.Exception,
                "ExecutionUpdated subscriber failed for execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                failure.Event.ExecutionRunId,
                failure.Event.AgentId,
                failure.Event.ChatSessionId,
                failure.Event.Id,
                failure.Event.Phase,
                failure.Event.State),
            overflow => logger.LogWarning(
                "ExecutionUpdated subscriber mailbox overflow dropped {DroppedEventCount} update(s) at capacity {MailboxCapacity} while preserving canonical execution. Latest dropped identity: execution run {ExecutionRunId}, agent {AgentId}, chat session {ChatSessionId}, event {ExecutionEventId}, phase {Phase}, and state {ExecutionState}.",
                overflow.DroppedEventCount,
                overflow.MailboxCapacity,
                overflow.LastDroppedEvent.ExecutionRunId,
                overflow.LastDroppedEvent.AgentId,
                overflow.LastDroppedEvent.ChatSessionId,
                overflow.LastDroppedEvent.Id,
                overflow.LastDroppedEvent.Phase,
                overflow.LastDroppedEvent.State));
    }

    private void HandleDatabaseProfileChanged(
        object? sender,
        DatabaseProfileChangedNotification notification)
    {
        lock (executionSubscriptionGate)
        {
            if (disposed)
            {
                return;
            }

            DetachExecutionSubscription();
            executionUpdatedDispatcher.DiscardPendingEvents();
            try
            {
                var service = ResolveCurrentWorkspaceService();
                UpdateExecutionSubscription(service);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "ExecutionUpdated relay detached after database profile changed from {PreviousProfileId} to {CurrentProfileId} at generation {Generation}, but the current workspace service could not be resolved.",
                    notification.PreviousProfileId,
                    notification.CurrentProfileId,
                    notification.Generation);
            }
        }
    }

    private sealed record PinnedActivityExecutionService(
        Guid ProfileId,
        WorkspaceScopeDescriptor WorkspaceScope,
        DatabaseProfileGeneration ProfileGeneration,
        IAgentFrameworkWorkspaceActivityExecutionService Service);

    private sealed record PinnedActivityExecutionIdentity(
        Guid ProfileId,
        WorkspaceScopeDescriptor WorkspaceScope,
        DatabaseProfileGeneration ProfileGeneration);

    private enum ProjectStructureAccessChange
    {
        Granted,
        Revoked
    }
}
