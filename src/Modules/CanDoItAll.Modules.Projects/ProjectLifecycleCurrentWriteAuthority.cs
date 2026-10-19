using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleCurrentWriteAuthority(ProjectWriteAdmissionService admissions,
    IDbContextFactory<ProjectsDbContext> contexts, CoordinatedDatabaseTransaction transactions) {
    public async Task<IProjectLifecycleCurrentWriteAccess> CaptureHierarchyAsync(ProjectMutationAuthorization authorization,
        ProjectLifecycleSubmission original, CancellationToken cancellationToken = default) {
        var child = original.Command switch {
            ProjectLifecycleCommand.EnsureLink ensure => ensure.Child,
            ProjectLifecycleCommand.ReconnectLink reconnect => reconnect.Expected.Child,
            _ => throw new ArgumentException("A hierarchy observation requires the original link or reconnect command.", nameof(original))
        };
        var binding = await admissions.CaptureCreationBindingAsync(cancellationToken);
        if (binding.Profile.Profile.Id != child.DatabaseProfileId || binding.Binding.Namespace != original.Operation.Namespace) {
            throw new InvalidOperationException("The hierarchy observation cannot adopt another dataset.");
        }
        return new Captured(this, authorization, new(ProjectMutationPurpose.HierarchyWrite, child.ProjectId,
            authorization.ExpectedProjects), binding);
    }

    private async ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(ProjectMutationAuthorization authorization,
        ProjectMutationTarget target, ProjectCreationDatasetBinding binding, CancellationToken cancellationToken) {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        await admissions.RequireCreationBindingAsync(binding, budget.Token);
        await using var context = await contexts.CreateDbContextAsync(budget.Token);
        await using var observation = await ProjectSourceMutationScope.BeginBoundAsync(context,
            target.ExistingProjects.Select(project => ProjectMutationScopeKeys.ForProject(project.ProjectId)).ToArray(),
            target.Purpose, target.ProjectId, target.ExistingProjects.Select(project => project.ProjectId).ToArray(), authorization,
            admissions, transactions, binding, budget.Token, expectedProjectAdmissions: target.ExistingProjects);
        await admissions.RequireCreationBindingAsync(binding, budget.Token);
        return ProjectLifecycleWriteAccessDecision.Allowed;
    }

    private sealed class Captured(ProjectLifecycleCurrentWriteAuthority owner, ProjectMutationAuthorization authorization,
        ProjectMutationTarget target, ProjectCreationDatasetBinding binding) : IProjectLifecycleCurrentWriteAccess {
        public ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken)
            => owner.ObserveAsync(authorization, target, binding, cancellationToken);
    }
}
