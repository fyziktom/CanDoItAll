using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessReadOnlyVerificationJobRunner {
    Task<ProcessReadOnlyVerificationJobRunResult> RunAsync(
        ProcessReadOnlyVerificationJob job,
        CancellationToken cancellationToken = default);
}

internal sealed class ProcessReadOnlyVerificationJobRunner(
    IProcessManagerReadOnlyVerificationFacade managerVerificationFacade) : IProcessReadOnlyVerificationJobRunner {
    public async Task<ProcessReadOnlyVerificationJobRunResult> RunAsync(
        ProcessReadOnlyVerificationJob job,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(job);

        var readback = await managerVerificationFacade.VerifyForReadbackAsync(
            job.ToManagerReadbackRequest(),
            cancellationToken);

        return new ProcessReadOnlyVerificationJobRunResult(
            job.Id,
            job.SourceKind,
            job.SourceReference,
            readback,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false);
    }
}

internal sealed record ProcessReadOnlyVerificationJobRunResult(
    Guid JobId,
    ProcessReadOnlyVerificationJobSourceKind SourceKind,
    string SourceReference,
    ProcessManagerReadOnlyVerificationReadbackDto Readback,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation) {
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob);
}
