using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Projects;

public sealed partial class ProjectWriteAdmissionService {
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
