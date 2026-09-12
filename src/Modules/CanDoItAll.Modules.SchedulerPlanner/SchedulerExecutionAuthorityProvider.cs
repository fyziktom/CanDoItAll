using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.SchedulerPlanner;

internal sealed class SchedulerExecutionAuthorityProvider(
    IAgentCatalogReadLeaseStore catalogLeases,
    IDatabaseProfileRuntimeAccessor profileAccessor) : IAgentExecutionSourceAuthorityProvider {
    public string SourceKind => SchedulerAgentChatContextBuilder.SourceKind;

    public async ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
        AgentExecutionSourceAuthorityRequest request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.SourceKind.Value, SourceKind, StringComparison.Ordinal) ||
            !string.Equals(request.SourceId.Value, SchedulerAgentChatContextBuilder.SourceId, StringComparison.Ordinal) ||
            request.Agent.Id == Guid.Empty || request.Agent.Status != AgentLifecycleStatus.Active || request.Agent.IsTemplate) {
            throw new AgentExecutionAuthorityMismatchException(
                "The Scheduler source requires an active executable agent and its published position.");
        }
        if (request.ObservedWorkspaceScope is not null && !request.IsCapturedSandboxRevalidation) {
            throw new AgentExecutionAuthorityMismatchException(
                "The Scheduler source cannot publish a workspace scope.");
        }

        RequireCurrentProfile(request.CurrentDatabaseProfileId);
        if (!SchedulerAgentRuntimeAuthorizationPolicy.IsManagedSchedulerActor(request.Agent)) {
            return new AgentExecutionSourceAuthorityDecision(WorkspaceScopeDescriptor.Sandbox,
                ReadAllowed: true, MutationAllowed: false, AgentExecutionAuthorityPolicyVersions.Canonical);
        }
        await using var catalog = await catalogLeases.AcquireAgentReadLeaseAsync(request.Agent.Id, cancellationToken).ConfigureAwait(false);
        RequireCurrentProfile(request.CurrentDatabaseProfileId);
        if (catalog.Scope != WorkspaceScopeDescriptor.Organization(request.CurrentDatabaseProfileId.ToString("N")) ||
                catalog.Capabilities.IsDefault ||
                catalog.Agent is not { IsTemplate: false, Status: AgentLifecycleStatus.Active } currentAgent ||
                currentAgent.Id != request.Agent.Id) {
            throw new AgentExecutionAuthorityMismatchException(
                "The source catalog does not contain the current active agent in its original database profile.");
        }
        return new AgentExecutionSourceAuthorityDecision(
            WorkspaceScopeDescriptor.Sandbox,
            ReadAllowed: true,
            MutationAllowed: SchedulerAgentRuntimeAuthorizationPolicy.IsManagedSchedulerActor(currentAgent) &&
                SchedulerAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                currentAgent, catalog.Capabilities, SchedulerToolPolicy.SchedulerWorkflowScheduleCreate),
            AgentExecutionAuthorityPolicyVersions.Canonical);
    }

    private void RequireCurrentProfile(Guid expectedProfileId) {
        if (expectedProfileId == Guid.Empty || profileAccessor.ResolveCurrentProfile().Profile.Id != expectedProfileId) {
            throw new AgentExecutionAuthorityMismatchException(
                "The Scheduler source belongs to a different database profile.");
        }
    }
}
