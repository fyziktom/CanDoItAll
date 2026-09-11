using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectProcessExecutionMutationService {
    internal ProjectMutationAuthorization BindProjectSource(ProjectProcessMutationAdmission admission,
        IReadOnlyCollection<ProjectWriteAdmission> expectedProjects, ProjectProcessProjectOperation operation)
        => new(new BoundProjectSource(this, admission, operation), expectedProjects);

    private async Task<IProjectMutationSourceLease> AcquireOwnerSourceAsync(ProjectProcessMutationAdmission admission,
        ProjectMutationTarget target, ProjectProcessProjectOperation operation, CancellationToken cancellationToken) {
        if (!admission.Dispatch.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase)) {
            throw new ProcessExecutionAuthorityMismatchException("The admitted Process step does not permit native project effects.");
        }
        var current = await ObserveAsync(admission.Dispatch.Evidence.ExecutionRunId, cancellationToken);
        if (current is null || !current.Dispatch.ObservedCurrentDispatch ||
                current.Dispatch.OwnerFingerprint != admission.Dispatch.OwnerFingerprint ||
                current.Dispatch.ProjectReference != admission.Dispatch.ProjectReference ||
                current.Dispatch.Evidence != admission.Dispatch.Evidence) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process execution no longer owns this project mutation.");
        }
        var held = await authorities.AcquireProjectMutationAsync(admission, target, operation, cancellationToken);
        return new ProcessProjectSourceLease(admission.Dispatch, held, processGuard);
    }

    private sealed class BoundProjectSource(ProjectProcessExecutionMutationService owner, ProjectProcessMutationAdmission admission,
        ProjectProcessProjectOperation operation) : IProjectMutationSourceAuthority {
        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default)
            => owner.AcquireOwnerSourceAsync(admission, target, operation, cancellationToken);
    }

    private sealed class ProcessProjectSourceLease(ProcessExecutionDispatchAuthority dispatch,
        ProjectProcessLaunchAuthorityService.ProjectProcessProjectAuthorityLease source,
        IProcessExecutionMutationGuard guard) : IProjectMutationSourceLease {
        private int disposed;
        public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => source.SourceProjects;

        public async Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            await guard.RequireForMutationAsync(dispatch, cancellationToken);
            await source.RequireForMutationAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync() {
            if (Interlocked.Exchange(ref disposed, 1) == 0) {
                await source.DisposeAsync();
            }
        }
    }
}
