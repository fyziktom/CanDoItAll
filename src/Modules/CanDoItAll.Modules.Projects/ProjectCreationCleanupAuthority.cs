using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectCreationCleanupAuthority(ProjectWriteAdmissionService admissions,
    IDbContextFactory<ProjectsDbContext> contexts, CoordinatedDatabaseTransaction transactions,
    IProjectDeletionCompletionQuery completions) {
    internal async Task<IProjectLifecycleCurrentWriteAccess> CaptureLocalAsync(ProjectDeletionPreparationContext original,
        CancellationToken cancellationToken) {
        var binding = await admissions.CaptureCreationBindingAsync(cancellationToken);
        RequireOriginal(binding, original);
        return new LocalAccess(admissions, binding);
    }

    internal IProjectLifecycleCurrentWriteAccess AfterRelease(ProjectCreationDatasetBinding binding,
        ProjectDeletionCompletionBinding completion, ProjectMutationAuthorization? authorization,
        ProjectCreationReservation? reservation, IProjectLifecycleCurrentWriteAccess caller) {
        return new ReleasedAccess(this, binding, completion, authorization, reservation, caller);
    }

    private async ValueTask<ProjectLifecycleWriteAccessDecision> ObserveReleasedAsync(ProjectCreationDatasetBinding binding,
        ProjectDeletionCompletionBinding completion, ProjectMutationAuthorization? authorization,
        ProjectCreationReservation? reservation, IProjectLifecycleCurrentWriteAccess caller, CancellationToken cancellationToken) {
        RequireOriginal(binding, completion.Original);
        if (authorization is not null && reservation is null) {
            throw new InvalidOperationException("Governed creation cleanup requires its exact server reservation.");
        }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        if (await caller.ObserveAsync(budget.Token) != ProjectLifecycleWriteAccessDecision.Allowed ||
                await completions.ObserveAsync(completion, budget.Token) is not ProjectDeletionCompletionObservation.Released) {
            return ProjectLifecycleWriteAccessDecision.Denied;
        }
        await admissions.RequireCreationBindingAsync(binding, budget.Token);
        if (authorization is null) {
            return ProjectLifecycleWriteAccessDecision.Allowed;
        }
        var target = completion.Original.Project;
        if (reservation!.DatabaseProfileId != target.DatabaseProfileId || reservation.ProjectId != target.ProjectId ||
                reservation.LifetimeId != target.LifetimeId || authorization.ExpectedProjects.Any(project => project.ProjectId == target.ProjectId)) {
            throw new InvalidOperationException("Creation cleanup cannot replace its original reservation or remove a source project.");
        }
        await using var context = await contexts.CreateDbContextAsync(budget.Token);
        await using var observation = await ProjectSourceMutationScope.BeginBoundAsync(context,
            [ProjectMutationScopeKeys.ForProject(target.ProjectId)],
            reservation.ParentProjectId.HasValue ? ProjectMutationPurpose.ReserveChild : ProjectMutationPurpose.ReserveRoot,
            target.ProjectId, authorization.ExpectedProjects.Select(project => project.ProjectId).ToArray(), authorization,
            admissions, transactions, binding, budget.Token, reservation, reservation.RequesterId,
            authorization.ExpectedProjects);
        await admissions.RequireCreationBindingAsync(binding, budget.Token);
        return await caller.ObserveAsync(budget.Token);
    }

    private static void RequireOriginal(ProjectCreationDatasetBinding binding, ProjectDeletionPreparationContext original) {
        if (binding.Profile.Profile.Id != original.Completion.DatabaseProfileId || binding.Binding.Namespace != original.Completion.Namespace) {
            throw new InvalidOperationException("Creation cleanup cannot adopt another profile or dataset namespace.");
        }
    }

    private sealed class LocalAccess(ProjectWriteAdmissionService admissions, ProjectCreationDatasetBinding original)
        : IProjectLifecycleCurrentWriteAccess {
        public async ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            await admissions.RequireCreationBindingAsync(original, cancellationToken);
            return ProjectLifecycleWriteAccessDecision.Allowed;
        }
    }

    private sealed class ReleasedAccess(ProjectCreationCleanupAuthority owner, ProjectCreationDatasetBinding binding,
        ProjectDeletionCompletionBinding completion, ProjectMutationAuthorization? authorization,
        ProjectCreationReservation? reservation, IProjectLifecycleCurrentWriteAccess caller) : IProjectLifecycleCurrentWriteAccess {
        public ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken)
            => owner.ObserveReleasedAsync(binding, completion, authorization, reservation, caller, cancellationToken);
    }
}
