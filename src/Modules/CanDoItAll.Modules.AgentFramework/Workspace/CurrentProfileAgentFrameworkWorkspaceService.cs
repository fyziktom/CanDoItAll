using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class CurrentProfileAgentFrameworkWorkspaceService(
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator,
    ILogger<CurrentProfileAgentFrameworkWorkspaceService> logger) : IAgentFrameworkWorkspaceService
{
    private readonly HashSet<IAgentFrameworkWorkspaceService> subscribedServices = new();
    private EventHandler<ExecutionLogEntry>? executionUpdated;

    public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
    {
        add
        {
            executionUpdated += value;
            EnsureExecutionSubscription(ResolveService());
        }
        remove
        {
            executionUpdated -= value;
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

    public async Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await ResolveService().DeleteAgentAsync(agentId, cancellationToken);
        await SynchronizeDirectoryProjectionWithReferenceDataInvalidationAsync(cancellationToken);
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
        return ResolveService().ExecuteRunAsync(request, cancellationToken);
    }

    public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default)
    {
        return ResolveService().ContinueExecutionRunAsync(executionRunId, approved, autoApprovePendingToolCalls, cancellationToken);
    }

    public Task<AgentChatRunResult> SendMessageAsync(
        Guid agentId,
        Guid? chatSessionId,
        string prompt,
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? attachmentPaths = null,
        AgentChatRunOptions? options = null)
    {
        return ResolveService().SendMessageAsync(agentId, chatSessionId, prompt, cancellationToken, attachmentPaths, options);
    }

    public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default)
    {
        return ResolveService().RespondToPendingApprovalsAsync(agentId, chatSessionId, approved, autoApprovePendingToolCalls, cancellationToken);
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
        var service = workspaceFactory.GetWorkspaceService(workspaceFactory.GetOrganizationScope());
        EnsureExecutionSubscription(service);
        return service;
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

    private void EnsureExecutionSubscription(IAgentFrameworkWorkspaceService service)
    {
        if (executionUpdated is null)
        {
            return;
        }

        if (subscribedServices.Add(service))
        {
            service.ExecutionUpdated += HandleExecutionUpdated;
        }
    }

    private void HandleExecutionUpdated(object? sender, ExecutionLogEntry entry)
    {
        executionUpdated?.Invoke(this, entry);
    }

    private enum ProjectStructureAccessChange
    {
        Granted,
        Revoked
    }
}
