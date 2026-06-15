using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

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
        var lifecycle = ProcessReadOnlyVerificationJobLifecycleRecord.FromReadback(job, readback);

        return new ProcessReadOnlyVerificationJobRunResult(
            job.Id,
            job.SourceKind,
            job.SourceReference,
            job.CorrelationId,
            lifecycle,
            readback,
            NoMutationPerformed: true,
            AllowsProcessMutation: false,
            AllowsTransitionMutation: false,
            AllowsFinalizerMutation: false)
        {
            Contract = ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob) with
            {
                RequestIdentity = new ProcessRuntimeHostRequestIdentity(
                    job.Id,
                    job.Payload.ProcessRunId,
                    job.Payload.StepRunId,
                    job.RequestedBy,
                    job.RequestedAt),
                AuditReference = readback.AuditRecordId.HasValue
                    ? new ProcessRuntimeHostAuditReference(
                        $"verification-job:{readback.AuditRecordId.Value:N}",
                        readback.AuditRecordObservationHash,
                        readback.ObservedAt)
                    : null
            }
        };
    }
}

internal enum ProcessReadOnlyVerificationJobLifecycleStatus
{
    Completed = 1,
    Denied = 2
}

internal sealed record ProcessReadOnlyVerificationJobLifecycleRecord(
    Guid JobId,
    ProcessReadOnlyVerificationJobSourceKind SourceKind,
    string SourceReference,
    string CorrelationId,
    ProcessDriverVerificationGatewayLane Lane,
    Guid ProcessRunId,
    Guid StepRunId,
    ProcessReadOnlyVerificationJobLifecycleStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    Guid? AuditRecordId,
    int AuditRecordCount,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation)
{
    public static ProcessReadOnlyVerificationJobLifecycleRecord FromReadback(
        ProcessReadOnlyVerificationJob job,
        ProcessManagerReadOnlyVerificationReadbackDto readback)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(readback);

        return new ProcessReadOnlyVerificationJobLifecycleRecord(
            job.Id,
            job.SourceKind,
            job.SourceReference,
            job.CorrelationId,
            job.Lane,
            job.Payload.ProcessRunId,
            job.Payload.StepRunId,
            readback.Status == ProcessManagerReadOnlyVerificationFacadeStatus.Succeeded
                ? ProcessReadOnlyVerificationJobLifecycleStatus.Completed
                : ProcessReadOnlyVerificationJobLifecycleStatus.Denied,
            job.RequestedAt,
            readback.ObservedAt,
            readback.AuditRecordId,
            readback.AuditRecords.Count,
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
    string CorrelationId,
    ProcessReadOnlyVerificationJobLifecycleRecord Lifecycle,
    ProcessManagerReadOnlyVerificationReadbackDto Readback,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation) {
    public ProcessRuntimeHostContractSnapshot Contract { get; init; } =
        ProcessRuntimeHostContractSnapshot.Create(ProcessRuntimeHostContractSurface.SchedulerWorkflowReadOnlyJob);
}
