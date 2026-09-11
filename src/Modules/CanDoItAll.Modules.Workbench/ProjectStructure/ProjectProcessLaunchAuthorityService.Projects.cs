using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectProcessProjectOperation { Native, Update, CreateRoot, CreateChild, ChangeHierarchy, MoveToChild }

public sealed partial class ProjectProcessLaunchAuthorityService {
    internal Task RequireProjectLifetimesForMutationAsync(IReadOnlyCollection<ProjectWriteAdmission> projects, CancellationToken cancellationToken)
        => projectAdmissions.RequireManyForMutationAsync(projects, cancellationToken);

    private static ProcessProjectMutationCeiling CaptureProjectMutationCeiling(AgentProjectStructureAccessSettings access,
        ProcessLaunchAgentCeiling ceiling) {
        var write = ceiling.MutationAllowed && access.CanRead;
        var structure = ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access);
        return new(write && access.CanCreateProjects && ceiling.WorkspaceScopeKind == ProcessLaunchSourceScopeKind.Organization &&
                Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureProjectCreate),
            write && access.CanCreateSubprojects && Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureSubprojectCreate),
            write && access.CanCreateSubprojects && structure && Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureSubprojectLink),
            write && access.CanCreateSubprojects && structure && Allows(ceiling, ProjectStructureToolPolicy.ProjectStructureNodesToNewSubproject));
    }

    internal async Task<ProjectProcessProjectAuthorityLease> AcquireProjectMutationAsync(ProjectProcessMutationAdmission admission,
        ProjectMutationTarget target, ProjectProcessProjectOperation operation, CancellationToken cancellationToken) {
        var saved = admission.Dispatch.SourceAuthority ?? throw Denied("The Process source authority must be reconciled before project mutation.");
        IAgentCatalogReadLease? held = null;
        try {
            if (saved.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                held = await catalog.AcquireAgentReadLeaseAsync(source.Ceiling.AgentId, cancellationToken);
            }
            RequireProjectMutation(admission, target, operation, held);
            return new(this, admission, target, operation, held);
        } catch {
            if (held is not null) {
                await held.DisposeAsync();
            }
            throw;
        }
    }

    private void RequireProjectMutation(ProjectProcessMutationAdmission admission, ProjectMutationTarget target,
        ProjectProcessProjectOperation operation, IAgentCatalogReadLease? held) {
        var saved = admission.Dispatch.SourceAuthority!;
        RequireSource(saved, saved, held);
        var original = admission.ProjectAdmission;
        if (target.ProjectId == Guid.Empty || target.ExistingProjects.IsDefault || !MatchesPurpose(operation, target.Purpose)) {
            throw Denied("The Process project operation does not match the actual owner mutation.");
        }
        var current = saved.Principal is ProcessLaunchPrincipal.AgentExecution source
            ? CaptureProjectMutationCeiling(AgentProjectStructureAccessMetadata.Read(held!.Agent!.ConfigurationJson), source.Ceiling)
            : new ProcessProjectMutationCeiling(true, true, true, true);
        if (operation is not (ProjectProcessProjectOperation.Native or ProjectProcessProjectOperation.Update) && saved.ProjectMutations is null) {
            throw Denied("This saved Process has no original project-creation or hierarchy ceiling. Reconcile its source authority before this effect.");
        }
        if (!AllowsProjectOperation(saved.ProjectMutations, operation) || !AllowsProjectOperation(current, operation) ||
                operation == ProjectProcessProjectOperation.Update && saved.Principal is ProcessLaunchPrincipal.AgentExecution updater &&
                    !Allows(updater.Ceiling, ProjectStructureToolPolicy.ProjectStructureProjectUpdate)) {
            throw Denied("The original Process source and its current policy do not permit this project operation.");
        }
        foreach (var project in target.ExistingProjects) {
            if (project == original) {
                continue;
            }
            var created = admission.FindCreatedProject(project);
            if (created is null) {
                throw Denied("The Process target is neither its original project lifetime nor an exact server reservation retained by this tool session.");
            }
            RequireCreatedTarget(saved, current, admission, created);
        }
        if (target.Purpose is ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.ReserveChild &&
                target.RequesterId != admission.Dispatch.Evidence.ExecutorAgentId) {
            throw Denied("The reservation requester does not match the admitted Process executor.");
        }
        if (target.Purpose is ProjectMutationPurpose.CreateRoot or ProjectMutationPurpose.CreateChild) {
            var reservation = target.Reservation ?? throw Denied("Process project creation requires its exact server reservation.");
            RequireCreatedTarget(saved, current, admission, reservation);
            if (reservation.ProjectId != target.ProjectId ||
                    (target.Purpose == ProjectMutationPurpose.CreateChild) != reservation.ParentProjectId.HasValue ||
                    reservation.ParentProjectId is { } parent && !target.ExistingProjects.Contains(
                        new(reservation.DatabaseProfileId, parent, reservation.ParentLifetimeId!.Value))) {
                throw Denied("The server reservation does not match the prepared target and original parent lifetime.");
            }
        }
    }

    private static void RequireCreatedTarget(ProcessLaunchAuthority saved, ProcessProjectMutationCeiling current,
        ProjectProcessMutationAdmission admission, ProjectCreationReservation reservation) {
        var ceiling = saved.ProjectMutations ?? throw Denied("The saved Process has no creation ceiling for this retained reservation. Reconciliation is required.");
        var permitted = reservation.ParentProjectId.HasValue
            ? ceiling.CanCreateSubprojects && current.CanCreateSubprojects || ceiling.CanMoveNodesToSubproject && current.CanMoveNodesToSubproject
            : ceiling.CanCreateProjects && current.CanCreateProjects;
        if (!permitted || reservation.Id == Guid.Empty || reservation.ProjectId == Guid.Empty || reservation.LifetimeId == Guid.Empty ||
                reservation.DatabaseProfileId != saved.DatabaseProfileId || reservation.RequesterId != admission.Dispatch.Evidence.ExecutorAgentId ||
                reservation.ParentProjectId.HasValue != reservation.ParentLifetimeId.HasValue ||
                saved.Principal is ProcessLaunchPrincipal.AgentExecution { Ceiling.WorkspaceScopeKind: ProcessLaunchSourceScopeKind.Project } &&
                    (reservation.ParentProjectId != admission.ProjectAdmission.ProjectId || reservation.ParentLifetimeId != admission.ProjectAdmission.LifetimeId)) {
            throw Denied("The exact reserved target is outside the original Process creation ceiling or its current source policy.");
        }
    }

    private static bool AllowsProjectOperation(ProcessProjectMutationCeiling? ceiling, ProjectProcessProjectOperation operation) => operation switch {
        ProjectProcessProjectOperation.Native or ProjectProcessProjectOperation.Update => true,
        ProjectProcessProjectOperation.CreateRoot => ceiling?.CanCreateProjects == true,
        ProjectProcessProjectOperation.CreateChild => ceiling?.CanCreateSubprojects == true,
        ProjectProcessProjectOperation.ChangeHierarchy => ceiling?.CanChangeHierarchy == true,
        ProjectProcessProjectOperation.MoveToChild => ceiling?.CanMoveNodesToSubproject == true,
        _ => false
    };

    private static bool MatchesPurpose(ProjectProcessProjectOperation operation, ProjectMutationPurpose purpose) => operation switch {
        ProjectProcessProjectOperation.Native or ProjectProcessProjectOperation.Update => purpose == ProjectMutationPurpose.ExistingWrite,
        ProjectProcessProjectOperation.CreateRoot => purpose is ProjectMutationPurpose.ReserveRoot or ProjectMutationPurpose.CreateRoot,
        ProjectProcessProjectOperation.CreateChild or ProjectProcessProjectOperation.MoveToChild => purpose is ProjectMutationPurpose.ReserveChild or ProjectMutationPurpose.CreateChild,
        ProjectProcessProjectOperation.ChangeHierarchy => purpose == ProjectMutationPurpose.HierarchyWrite,
        _ => false
    };

    private async Task RequireProjectMutationForTransactionAsync(ProjectProcessMutationAdmission admission, ProjectMutationTarget target,
        ProjectProcessProjectOperation operation, IAgentCatalogReadLease? held, CancellationToken cancellationToken) {
        RequireProjectMutation(admission, target, operation, held);
        var reservations = target.ExistingProjects.Where(project => project != admission.ProjectAdmission)
            .Select(project => admission.FindCreatedProject(project)!).ToArray();
        await projectAdmissions.RequireConsumedCreationsForMutationAsync(reservations, cancellationToken);
    }

    internal sealed class ProjectProcessProjectAuthorityLease(ProjectProcessLaunchAuthorityService owner,
        ProjectProcessMutationAdmission admission, ProjectMutationTarget target, ProjectProcessProjectOperation operation,
        IAgentCatalogReadLease? held) : IAsyncDisposable {
        private int disposed;
        internal ImmutableArray<ProjectWriteAdmission> SourceProjects => [admission.ProjectAdmission];

        internal Task RequireForMutationAsync(CancellationToken cancellationToken) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            return owner.RequireProjectMutationForTransactionAsync(admission, target, operation, held, cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0 && held is not null) {
                await held.DisposeAsync();
            }
        }
    }
}
