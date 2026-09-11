using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAgentSourceMutationAuthority(ICanonicalRuntimeDatabase database,
    IAgentCatalogReadLeaseStore catalog, IAgentExecutionProfileGenerationSource generations) {
    internal ProjectMutationAuthorization Bind(ProjectAgentMutationAdmission admission,
        IReadOnlyCollection<ProjectWriteAdmission> expectedProjects)
        => new(new BoundSource(this, admission), expectedProjects);

    internal async Task<ProjectAgentSourceMutationLease> AcquireAsync(ProjectAgentMutationAdmission admission,
        ProjectMutationTarget target, CancellationToken cancellationToken) {
        var held = await catalog.AcquireAgentReadLeaseAsync(admission.AgentId, cancellationToken);
        try {
            RequireSource(admission, target, held);
            return new(this, admission, target, held);
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    internal bool RequiresTaskTargetGuard(ProjectAgentMutationAdmission admission, IAgentCatalogReadLease held) {
        var current = AgentProjectStructureAccessMetadata.Read(held.Agent!.ConfigurationJson);
        return !admission.CanWriteTasks || !(current.CanWrite || current.CanWriteTasks);
    }

    internal void RequireSource(ProjectAgentMutationAdmission admission, ProjectMutationTarget target, IAgentCatalogReadLease held) {
        var profileId = database.Profile.Profile.Id;
        var agent = held.Agent;
        if (!Enum.IsDefined(target.Purpose) || target.ProjectId == Guid.Empty || target.ExistingProjects.IsDefault ||
                admission.DatabaseProfileId != profileId || held.Scope != WorkspaceScopeDescriptor.Organization(profileId.ToString("N")) ||
                agent is null || agent.Id != admission.AgentId || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active ||
                !agent.Permissions.CanUseTools || !admission.CanRead) {
            throw Denied("The original Agent source is no longer active in this profile or permitted to mutate projects.");
        }
        var current = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var currentStructure = current.CanWrite || current.CanWriteNonTaskStructure;
        var capturedWrite = target.Purpose switch {
            ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.CreateRoot => admission.CanCreateProjects,
            ProjectMutationPurpose.ReserveChild or ProjectMutationPurpose.CreateChild => admission.CanCreateSubprojects,
            ProjectMutationPurpose.HierarchyWrite => admission.CanWriteStructure && admission.CanCreateSubprojects,
            ProjectMutationPurpose.ExistingWrite when admission.Domain == ProjectAgentMutationDomain.Tasks => admission.CanWriteTasks,
            ProjectMutationPurpose.ExistingWrite => admission.CanWriteStructure,
            _ => false
        };
        var currentWrite = target.Purpose switch {
            ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.CreateRoot => current.CanCreateProjects,
            ProjectMutationPurpose.ReserveChild or ProjectMutationPurpose.CreateChild => current.CanCreateSubprojects,
            ProjectMutationPurpose.HierarchyWrite => currentStructure && current.CanCreateSubprojects,
            ProjectMutationPurpose.ExistingWrite when admission.Domain == ProjectAgentMutationDomain.Tasks => current.CanWrite || current.CanWriteTasks,
            ProjectMutationPurpose.ExistingWrite => currentStructure,
            _ => false
        };
        if (!capturedWrite || !current.CanRead || !currentWrite) {
            throw Denied("The original and current Agent grants do not permit this owner mutation.");
        }
        if (target.Reservation is not null && admission.CreatedProjectReservation is not null &&
                target.Reservation != admission.CreatedProjectReservation) {
            throw Denied("The creation target differs from the original reserved target.");
        }
        var reservation = target.Reservation ?? admission.CreatedProjectReservation;
        if (reservation is not null && (reservation.DatabaseProfileId != profileId || reservation.RequesterId != admission.AgentId ||
                reservation.LifetimeId == Guid.Empty || reservation.ProjectId == Guid.Empty)) {
            throw Denied("The creation reservation does not belong to the original Agent source.");
        }
        if (target.Purpose is ProjectMutationPurpose.CreateRoot or ProjectMutationPurpose.CreateChild &&
                (reservation is null || reservation.ProjectId != target.ProjectId ||
                    (target.Purpose == ProjectMutationPurpose.CreateChild) != reservation.ParentProjectId.HasValue)) {
            throw Denied("An Agent project creation requires its exact server reservation.");
        }
        if (target.Purpose is ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.ReserveChild && target.RequesterId != admission.AgentId) {
            throw Denied("The creation reservation requester is not the original Agent source.");
        }
        var expected = target.ExistingProjects;
        if (target.Purpose is ProjectMutationPurpose.CreateRoot or ProjectMutationPurpose.CreateChild) {
            expected = expected.Add(new(profileId, reservation!.ProjectId, reservation.LifetimeId));
        }
        foreach (var project in expected) {
            var lifetime = new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);
            var newlyCreated = reservation is not null && project.ProjectId == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId;
            var originalAllows = admission.AllowAllProjects || admission.ProjectIds.Contains(project.ProjectId) && admission.ProjectLifetimes.Contains(lifetime);
            if (newlyCreated) {
                originalAllows |= reservation!.ParentProjectId.HasValue ? admission.CanCreateSubprojects : admission.CanCreateProjects;
            }
            if (project.DatabaseProfileId != profileId || !originalAllows ||
                    !current.AllowAllProjects && (!current.AllowedProjectIds.Contains(project.ProjectId) || !current.AllowedProjectLifetimes.Contains(lifetime))) {
                throw Denied("The original and current grants do not cover every exact project lifetime in this mutation.");
            }
        }
        if (reservation?.ParentProjectId is { } parentId && reservation.ParentLifetimeId is { } parentLifetime &&
                !target.ExistingProjects.Any(project => project.ProjectId == parentId && project.LifetimeId == parentLifetime)) {
            throw Denied("The captured source does not include the reservation's exact parent lifetime.");
        }
        if (admission.Governance is not { } ceiling) {
            return;
        }
        var projectScopeAllowed = ceiling.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
            Guid.TryParse(ceiling.WorkspaceScope.Key, out var scopeId) &&
            target.Purpose is not (ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.CreateRoot) &&
            expected.All(project => project.ProjectId == scopeId ||
                reservation?.ParentProjectId == scopeId && project.ProjectId == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId);
        var organizationScopeAllowed = ceiling.WorkspaceScope.Kind == WorkspaceScopeKind.Organization &&
            Guid.TryParse(ceiling.WorkspaceScope.Key, out var authorityProfile) && authorityProfile == profileId;
        if (ceiling.AgentId != admission.AgentId || ceiling.DatabaseProfileId != profileId ||
                ceiling.DatabaseProfileGeneration != generations.GetGeneration() || !ceiling.ReadAllowed || !ceiling.MutationAllowed ||
                ceiling.AllowedOperations.Count > 0 && (admission.Operation is null || !ceiling.AllowedOperations.Contains(admission.Operation)) ||
                !(projectScopeAllowed || organizationScopeAllowed)) {
            throw Denied("The admitted execution ceiling does not permit this owner mutation in the current profile generation and scope.");
        }
    }

    private static ProjectStructureAgentException Denied(string message) => new(403, "ProjectMutationAuthorityDenied", message);

    private sealed class BoundSource(ProjectAgentSourceMutationAuthority owner, ProjectAgentMutationAdmission admission) : IProjectMutationSourceAuthority {
        public async Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default)
            => await owner.AcquireAsync(admission, target, cancellationToken);
    }
}

internal sealed class ProjectAgentSourceMutationLease(ProjectAgentSourceMutationAuthority owner, ProjectAgentMutationAdmission admission,
    ProjectMutationTarget target, IAgentCatalogReadLease held) : IProjectMutationSourceLease {
    private int disposed;
    internal bool RequiresTaskTargetGuard => owner.RequiresTaskTargetGuard(admission, held);
    public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => [];

    public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrent();
        return Task.CompletedTask;
    }

    public void RequireCurrent() {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        owner.RequireSource(admission, target, held);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0) {
            await held.DisposeAsync();
        }
    }
}
