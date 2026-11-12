using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed partial class ProjectWriteAdmissionService {
    public async Task AcquireMutationGatesAsync(IReadOnlyCollection<ProjectWriteAdmission> admissions, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admissions);
        foreach (var admission in admissions) {
            ArgumentNullException.ThrowIfNull(admission);
            if (admission.DatabaseProfileId != DatabaseProfileId) {
                throw new ProjectWriteAdmissionRejectedException(admission);
            }
        }
        await using var context = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        if (context.Database.IsRelational() && !context.Database.IsNpgsql()) {
            throw new NotSupportedException("Project mutation gates require PostgreSQL for relational persistence.");
        }
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(
            context, admissions.Select(admission => ProjectMutationScopeKeys.ForProject(admission.ProjectId)).ToArray(), cancellationToken);
    }

    public async Task RequireUnderMutationGateAsync(ProjectWriteAdmission admission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.DatabaseProfileId != DatabaseProfileId) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        await using var context = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(
            context, [ProjectMutationScopeKeys.ForProject(admission.ProjectId)], cancellationToken);
        await RequireAsync(context, admission, cancellationToken);
    }
}
