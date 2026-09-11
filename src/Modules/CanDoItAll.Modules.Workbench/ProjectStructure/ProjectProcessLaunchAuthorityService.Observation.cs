using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectProcessLaunchAuthorityService {
    public async Task<IAsyncDisposable> AcquireResultReadAsync(ProcessLaunchAuthority authority,
        CancellationToken cancellationToken = default) {
        authority.Validate();
        IAgentCatalogReadLease? held = null;
        try {
            if (authority.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await catalog.AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireReadSource(authority, held);
            return held is null ? CompletedReadCheck.Instance : held;
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    public async Task<IAsyncDisposable> AcquireUncommittedResultReadAsync(AgentExecutionGovernanceSnapshot governance,
        Guid projectId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(governance);
        if (governance.DatabaseProfileId != database.Profile.Profile.Id) {
            throw Denied("The saved proposal belongs to a different runtime profile.");
        }
        var project = await CaptureProjectAsync(projectId, cancellationToken);
        var authority = new ProcessLaunchAuthority(
            new ProcessLaunchPrincipal.AgentExecution(CreateAgentCeiling(governance), ProcessLaunchAgentOperation.StructureStart),
            governance.DatabaseProfileId, project, false, false, governance.PolicyFingerprint);
        return await AcquireResultReadAsync(authority, cancellationToken);
    }

    public async Task<ProcessSourceAuthorityObservation> ObserveAsync(ProcessLaunchAuthority authority,
        CancellationToken cancellationToken = default) {
        authority.Validate();
        IAgentCatalogReadLease? held = null;
        try {
            if (authority.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await catalog.AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireReadSource(authority, held);
            try {
                RequireSource(authority, authority, held);
                if (authority.ProjectAdmission is { } project) {
                    await projectAdmissions.RequireCurrentAsync(ToProject(project), cancellationToken);
                }
                return new(true, true);
            } catch (ProcessLaunchAuthorityRejectedException) {
                return new(true, false);
            } catch (ProjectWriteAdmissionRejectedException) {
                return new(true, false);
            }
        } catch (ProcessLaunchAuthorityRejectedException) {
            return new(false, false);
        } finally {
            if (held is not null) {
                await held.DisposeAsync();
            }
        }
    }

    private void RequireReadSource(ProcessLaunchAuthority authority, IAgentCatalogReadLease? held) {
        if (authority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw Denied("The saved Process source belongs to a different runtime profile.");
        }
        if (authority.Principal is not ProcessLaunchPrincipal.AgentExecution source) {
            RequireOperator(authority);
            return;
        }
        if (held is null || authority.ProjectAdmission is not { } project) {
            throw Denied("The saved Process Agent source has no exact project authority.");
        }
        RequireCatalogScope(held);
        var ceiling = source.Ceiling;
        if (!ceiling.ReadAllowed || ceiling.DatabaseProfileGeneration != generations.GetGeneration().Value ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Project &&
                    (!Guid.TryParse(ceiling.WorkspaceScopeKey, out var sourceProject) || sourceProject != project.ProjectId) ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Organization &&
                    (!Guid.TryParse(ceiling.WorkspaceScopeKey, out var sourceProfile) || sourceProfile != project.DatabaseProfileId) ||
                ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Sandbox) {
            throw Denied("The saved Process Agent source no longer permits result disclosure.");
        }
        var agent = held.Agent;
        if (agent is null || agent.Id != ceiling.AgentId || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active ||
                !agent.Permissions.CanUseTools) {
            throw Denied("The saved Process source Agent no longer permits result disclosure.");
        }
        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if (!access.CanRead || !access.AllowAllProjects && (!access.AllowedProjectIds.Contains(project.ProjectId) ||
                !access.AllowedProjectLifetimes.Contains(new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)))) {
            throw Denied("Current Agent read policy does not grant the original Process project lifetime.");
        }
    }

    private sealed class CompletedReadCheck : IAsyncDisposable {
        internal static CompletedReadCheck Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
