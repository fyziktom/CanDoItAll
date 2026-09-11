using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectScheduledWorkflowSourceAuthorityPolicy(
    ICanonicalRuntimeDatabase database,
    IAgentCatalogReadLeaseStore catalog,
    IAgentExecutionProfileGenerationSource generations,
    IWorkflowScheduledAuthorityPolicy schedules,
    IOptionsMonitor<ApiAccessOptions> apiOptions,
    TimeProvider clock) : IWorkflowScheduledSourceAuthorityPolicy {
    public async Task<IWorkflowScheduledSourceAuthorityLease> AcquireAsync(WorkflowStructureAuthority authority,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(authority);
        IAgentCatalogReadLease? held = null;
        try {
            if (authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution) {
                var governance = authority.AgentGovernance
                    ?? throw Denied("The scheduled Agent source has no saved execution authority; reconcile its real source before launching.");
                held = await catalog.AcquireAgentReadLeaseAsync(governance.AgentId, cancellationToken);
            }
            RequireSource(authority, held);
            return new SourceLease(this, authority, held);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private void RequireSource(WorkflowStructureAuthority authority, IAgentCatalogReadLease? held) {
        if (!Enum.IsDefined(authority.Channel) || !Enum.IsDefined(authority.OperatorSurface) ||
                authority.DatabaseProfileId != database.Profile.Profile.Id || string.IsNullOrWhiteSpace(authority.PolicyFingerprint) ||
                authority.Principal is null || authority.ProjectIds is null ||
                authority.ProcessAuthority is not null || authority.ExpiresAtUtc <= clock.GetUtcNow()) {
            throw Denied("The scheduled Workflow has an invalid, expired, or different-profile source authority.");
        }
        if (authority.Channel != WorkflowStructureAuthorityChannel.AgentExecution) {
            RequireOperator(authority);
            return;
        }
        var governance = authority.AgentGovernance;
        if (governance is null || held is null ||
                held.Scope != WorkspaceScopeDescriptor.Organization(database.Profile.Profile.Id.ToString("N")) ||
                authority.Principal.Kind != WorkflowLaunchActorKind.Agent ||
                !Guid.TryParse(authority.Principal.SubjectId, out var sourceAgentId) || sourceAgentId != governance.AgentId ||
                governance.DatabaseProfileId != authority.DatabaseProfileId || governance.DatabaseProfileGeneration != generations.GetGeneration() ||
                governance.PolicyFingerprint != authority.PolicyFingerprint || !governance.ReadAllowed || !governance.MutationAllowed ||
                governance.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
                    (!Guid.TryParse(governance.WorkspaceScope.Key, out var projectId) || projectId == Guid.Empty || projectId != authority.ProjectId) ||
                governance.WorkspaceScope.Kind == WorkspaceScopeKind.Organization &&
                    (!Guid.TryParse(governance.WorkspaceScope.Key, out var profileId) || profileId != authority.DatabaseProfileId) ||
                governance.WorkspaceScope.Kind is not (WorkspaceScopeKind.Organization or WorkspaceScopeKind.Project) ||
                !schedules.AllowsScheduling(governance)) {
            throw Denied("The saved Agent execution ceiling does not permit this schedule in the current profile generation.");
        }
        var agent = held.Agent;
        if (agent is null || agent.Id != sourceAgentId || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active ||
                !agent.Permissions.CanUseTools || !agent.Permissions.CanScheduleWork) {
            throw Denied("The scheduled source Agent is no longer active or permitted to use tools and schedule work.");
        }
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (authority.AllProjects && !access.AllowAllProjects ||
                !access.AllowAllProjects && (authority.ProjectId != Guid.Empty && !access.AllowedProjectIds.Contains(authority.ProjectId) ||
                    authority.ProjectIds.Any(id => !access.AllowedProjectIds.Contains(id))) ||
                authority.CanCreateTasks && (!access.CanRead || !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access)) ||
                authority.CanCreateAssets && (!access.CanRead || !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access))) {
            throw Denied("Current Agent project policy is narrower than the saved schedule's output ceiling.");
        }
    }

    private void RequireOperator(WorkflowStructureAuthority authority) {
        var options = apiOptions.CurrentValue;
        if (authority.Principal.Kind != WorkflowLaunchActorKind.User ||
                authority.PolicyFingerprint != OperatorFingerprint(authority.OperatorSurface) ||
                authority.OperatorSurface == WorkflowStructureOperatorSurface.Api && (!options.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.LocalOperator && options.Authorization.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator &&
                        (!options.Authorization.Enabled || !authority.ExpiresAtUtc.HasValue)) ||
                authority.Channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator &&
                    authority.OperatorSurface != WorkflowStructureOperatorSurface.Api) {
            throw Denied("The schedule's operator authorization expired or its current API policy changed.");
        }
    }

    private string OperatorFingerprint(WorkflowStructureOperatorSurface surface) {
        if (surface == WorkflowStructureOperatorSurface.UserInterface) {
            return ProjectWorkflowContributionFingerprint.Hash("structure-local-ui-v1");
        }
        var options = apiOptions.CurrentValue;
        return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new {
            ApiEnabled = options.Enabled,
            AuthorizationEnabled = options.Authorization.Enabled,
            options.Authorization.Issuer,
            options.Authorization.Audience,
            SigningKeyFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Authorization.SigningKey)))
        }));
    }

    private async Task RequireForMutationAsync(WorkflowStructureAuthority authority, IAgentCatalogReadLease? held,
        CancellationToken cancellationToken) {
        RequireSource(authority, held);
        if (authority.SchedulerAuthority is { } schedule) {
            await schedules.RequireCurrentForMutationAsync(schedule, cancellationToken);
        }
    }

    private static WorkflowScheduledSourceAuthorityException Denied(string message) => new(message);

    private sealed class SourceLease(ProjectScheduledWorkflowSourceAuthorityPolicy owner, WorkflowStructureAuthority authority,
        IAgentCatalogReadLease? held) : IWorkflowScheduledSourceAuthorityLease {
        private int disposed;

        public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return owner.RequireForMutationAsync(authority, held, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
