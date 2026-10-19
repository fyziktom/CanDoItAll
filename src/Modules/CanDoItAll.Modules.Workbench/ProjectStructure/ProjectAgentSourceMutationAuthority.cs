using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectAgentSourceMutationAuthority(ICanonicalRuntimeDatabase database,
    IAgentCatalogReadLeaseStore catalog, IAgentExecutionProfileGenerationSource generations, ProjectWriteAdmissionService projects) {
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
        if (admission.SourceProject is { } sourceProject && !expected.Contains(sourceProject)) {
            expected = expected.Add(sourceProject);
        }
        foreach (var project in expected) {
            var lifetime = new AgentProjectStructureLifetime(project.DatabaseProfileId, project.ProjectId, project.LifetimeId);
            var created = admission.FindCreatedProject(project) ??
                (reservation is not null && reservation.ProjectId == project.ProjectId && reservation.LifetimeId == project.LifetimeId
                    ? reservation : null);
            var newlyCreated = created is not null;
            var originalAllows = admission.AllowAllProjects || admission.ProjectIds.Contains(project.ProjectId) && admission.ProjectLifetimes.Contains(lifetime);
            if (newlyCreated) {
                originalAllows |= created!.ParentProjectId.HasValue
                    ? admission.CanCreateSubprojects && current.CanCreateSubprojects
                    : admission.CanCreateProjects && current.CanCreateProjects;
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
            admission.SourceProject is { } scopedProject && scopedProject.DatabaseProfileId == profileId && scopedProject.ProjectId == scopeId &&
            (ceiling.SourceProjectLifetime is null || ceiling.SourceProjectLifetime ==
                new AgentProjectStructureLifetime(scopedProject.DatabaseProfileId, scopedProject.ProjectId, scopedProject.LifetimeId)) &&
            target.Purpose is not (ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.CreateRoot) &&
            (target.Purpose is not (ProjectMutationPurpose.ReserveChild or ProjectMutationPurpose.CreateChild) ||
                target.ExistingProjects.Length == 1 && target.ExistingProjects[0] == scopedProject) &&
            expected.All(project => project.ProjectId == scopeId ||
                admission.FindCreatedProject(project) is { } child && child.ParentProjectId == scopeId && child.ParentLifetimeId == scopedProject.LifetimeId);
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

    internal Task RequirePriorCreationsAsync(ProjectAgentMutationAdmission admission, ProjectMutationTarget target,
        CancellationToken cancellationToken, ProjectWriteAdmission? retired = null)
        => projects.RequireConsumedCreationsForMutationAsync(admission.PriorCreatedProjects.Where(reservation =>
            target.ExistingProjects.Any(project => project != retired && project.DatabaseProfileId == reservation.DatabaseProfileId &&
                project.ProjectId == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId)).ToArray(), cancellationToken);

    private sealed class BoundSource(ProjectAgentSourceMutationAuthority owner, ProjectAgentMutationAdmission admission) : IProjectMutationSourceAuthority {
        public async Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default)
            => await owner.AcquireAsync(admission, target, cancellationToken);
    }
}

internal sealed class ProjectAgentSourceMutationLease(ProjectAgentSourceMutationAuthority owner, ProjectAgentMutationAdmission admission,
    ProjectMutationTarget target, IAgentCatalogReadLease held) : IProjectMutationSourceLease, IProjectGovernedInvocationLease {
    private int disposed;
    internal bool RequiresTaskTargetGuard => owner.RequiresTaskTargetGuard(admission, held);
    public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => admission.SourceProject is { } source ? [source] : [];
    public ValueTask<AgentToolInvocationPublicationLease> AcquireInvocationAsync(AgentToolInvocationClaim claim, CancellationToken cancellationToken) {
        RequireCurrent();
        return claim.AcquirePublicationAsync(held, cancellationToken);
    }

    public ProjectDeletionSourceRecovery CaptureDeletionRecoverySource() {
        RequireCurrent();
        return held is IProjectDeletionSourceRecoveryLease source
            ? source.CaptureDeletionRecoverySource()
            : throw new NotSupportedException("The acquired Agent catalog has no original deletion recovery binding.");
    }

    public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrent();
        return owner.RequirePriorCreationsAsync(admission, target, cancellationToken);
    }

    public Task RequireForDeletionCommitAsync(ProjectDeletionCommitTarget deletion, CancellationToken cancellationToken = default) {
        deletion.RequireOriginalTarget(target);
        if (admission.SourceProject?.ProjectId == deletion.Project.ProjectId) {
            throw new InvalidOperationException("An Agent deletion cannot retire the project that supplies its original source authority.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        RequireCurrent();
        return owner.RequirePriorCreationsAsync(admission, target, cancellationToken, deletion.Project);
    }

    public Task RequireForReservedCancellationCommitAsync(ProjectReservationCancellationTarget cancellation,
        CancellationToken cancellationToken = default) {
        target.RequireReservedCancellation(cancellation);
        if (admission.SourceProject?.ProjectId == cancellation.Project.ProjectId) {
            throw new InvalidOperationException("A never-created reservation cannot supply the Agent's live source authority.");
        }
        return RequireForMutationAsync(cancellationToken);
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
