using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectDefinitionAgentReceiptAccess(ProjectAgentMutationAdmission original,
    IAgentCatalogReadLeaseStore catalog, ProjectWriteAdmissionService projects) : IProjectDefinitionReceiptAccess {
    public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
        if (target.DatabaseProfileId != projects.DatabaseProfileId) {
            return false;
        }
        await using var held = await catalog.AcquireAgentReadLeaseAsync(original.AgentId, cancellationToken);
        if (held.Scope != WorkspaceScopeDescriptor.Organization(target.DatabaseProfileId.ToString("N")) ||
            !ProjectDefinitionGovernedReadPolicy.AgentCanRead(original, held.Agent, target)) {
            return false;
        }
        return await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
    }
}

internal sealed class ProjectDefinitionProcessReceiptAccess(ProjectProcessMutationAdmission original,
    ProjectProcessExecutionMutationService processes, IAgentCatalogReadLeaseStore catalog,
    ProjectWriteAdmissionService projects) : IProjectDefinitionReceiptAccess {
    public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
        if (target.DatabaseProfileId != projects.DatabaseProfileId) {
            return false;
        }
        var current = await processes.ReadAccessAsync(original.Dispatch.Evidence.ExecutionRunId, cancellationToken);
        if (!ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current, target)) {
            return false;
        }
        if (current!.Dispatch.SourceAuthority!.Principal is ProcessLaunchPrincipal.AgentExecution source) {
            await using var held = await catalog.AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            if (held.Scope != WorkspaceScopeDescriptor.Organization(target.DatabaseProfileId.ToString("N")) ||
                !ProjectDefinitionGovernedReadPolicy.CurrentAgentCanRead(source.Ceiling.AgentId, held.Agent, target)) {
                return false;
            }
            return await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
        }
        return await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
    }
}

internal static class ProjectDefinitionGovernedReadPolicy {
    internal static bool AgentCanRead(ProjectAgentMutationAdmission original, AgentDefinition? current,
        ProjectWriteAdmission target) {
        if (!original.CanRead || !CurrentAgentCanRead(original.AgentId, current, target)) {
            return false;
        }
        var reservation = original.CreatedProjectReservation;
        var created = reservation is not null && reservation.RequesterId == original.AgentId &&
            reservation.ProjectId == target.ProjectId && reservation.LifetimeId == target.LifetimeId;
        var originalAllows = original.AllowAllProjects || original.ProjectIds.Contains(target.ProjectId) &&
            original.ProjectLifetimes.Any(item => item.DatabaseProfileId == original.DatabaseProfileId &&
                item.ProjectId == target.ProjectId && item.LifetimeId == target.LifetimeId) || created;
        if (!originalAllows) {
            return false;
        }
        if (original.Governance is not { } governance) {
            return false;
        }
        if (!governance.ReadAllowed || governance.AgentId != original.AgentId || governance.DatabaseProfileId != original.DatabaseProfileId) {
            return false;
        }
        return governance.WorkspaceScope.Kind switch {
            WorkspaceScopeKind.Organization => Guid.TryParse(governance.WorkspaceScope.Key, out var profile) && profile == original.DatabaseProfileId,
            WorkspaceScopeKind.Project => Guid.TryParse(governance.WorkspaceScope.Key, out var project) &&
                original.SourceProject is { } source && source.ProjectId == project &&
                (source.ProjectId == target.ProjectId && source.LifetimeId == target.LifetimeId ||
                    created && reservation!.ParentProjectId == source.ProjectId && reservation.ParentLifetimeId == source.LifetimeId),
            _ => false
        };
    }

    internal static bool CurrentAgentCanRead(Guid agentId, AgentDefinition? current, ProjectWriteAdmission target) {
        if (current is null || current.Id != agentId || current.IsTemplate || current.Status != AgentLifecycleStatus.Active || !current.Permissions.CanUseTools) {
            return false;
        }
        var access = AgentProjectStructureAccessMetadata.Read(current.ConfigurationJson);
        return access.CanRead && (access.AllowAllProjects || access.AllowedProjectIds.Contains(target.ProjectId) &&
            access.AllowedProjectLifetimes.Contains(new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId)));
    }

    internal static bool ProcessCanRead(ProjectProcessMutationAdmission original, ProjectProcessExecutionAccess? current,
        ProjectWriteAdmission target) {
        if (current is not { CanRead: true, Dispatch.SourceAuthority.ProjectAdmission: not null, Dispatch.ProjectReference: not null }) {
            return false;
        }
        var expected = original.Dispatch.Evidence;
        var observed = current.Dispatch.Evidence;
        if (observed.ExecutionRunId != expected.ExecutionRunId || observed.ExecutorAgentId != expected.ExecutorAgentId ||
            observed.RunId != expected.RunId || observed.StepInstanceId != expected.StepInstanceId ||
            current.Dispatch.RootRunId != original.Dispatch.RootRunId ||
            current.Dispatch.ProjectReference.AdmissionId != original.Dispatch.ProjectReference?.AdmissionId) {
            return false;
        }
        var source = original.ProjectAdmission;
        var currentSource = current.Dispatch.SourceAuthority.ProjectAdmission;
        if (source.ProjectId != currentSource.ProjectId || source.LifetimeId != currentSource.LifetimeId) {
            return false;
        }
        return source.ProjectId == target.ProjectId && source.LifetimeId == target.LifetimeId ||
            original.CreatedProjects.Any(item => item.RequesterId == expected.ExecutorAgentId && item.ProjectId == target.ProjectId && item.LifetimeId == target.LifetimeId);
    }
}
