using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessAcceptedExecutionInputReader(ProcessPersistenceDbContext context, ICanonicalRuntimeDatabase database)
    : IProcessAcceptedExecutionInputReader {
    public async Task<ProcessAcceptedExecutionInput> ReadAsync(ProcessRunId runId, ProcessStepInstanceId stepId,
        CancellationToken cancellationToken = default) {
        if (runId.Value == Guid.Empty || stepId.Value == Guid.Empty) {
            throw new ArgumentException("Original Process input requires an exact run and step.");
        }
        var row = await (from runtime in context.RuntimeStates.AsNoTracking()
            join launch in context.PreparedLaunches.AsNoTracking() on runtime.LaunchAdmissionId equals launch.Id
            join input in context.AcceptedAssignmentSources.AsNoTracking().Where(item => item.StepInstanceId == stepId.Value)
                on launch.Id equals input.AdmissionId into inputs
            from input in inputs.DefaultIfEmpty()
            where runtime.RunId == runId.Value
            select new {
                Runtime = new ProcessRuntimeStateEntity {
                    RunId = runtime.RunId, RootRunId = runtime.RootRunId, PlanId = runtime.PlanId, PlanHash = runtime.PlanHash,
                    LaunchAdmissionId = runtime.LaunchAdmissionId,
                    ProjectAdmissionDatabaseProfileId = runtime.ProjectAdmissionDatabaseProfileId,
                    ProjectAdmissionProjectId = runtime.ProjectAdmissionProjectId, ProjectAdmissionLifetimeId = runtime.ProjectAdmissionLifetimeId
                },
                launch.Id, launch.RunId, launch.PlanId, launch.AcceptedAtUtc, launch.PreparationFingerprint,
                launch.AcceptedSourceVersion, launch.AcceptedSourceHeaderJson, launch.AcceptedSourceHeaderFingerprint,
                Input = input
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new ProcessExecutionAuthorityMismatchException("The Process run has no original accepted launch.");
        if (row.AcceptedAtUtc is null || row.RunId != runId.Value || row.PlanId != row.Runtime.PlanId) {
            throw new ProcessExecutionAuthorityMismatchException("The original input launch differs from its accepted runtime.");
        }
        if (row.AcceptedSourceVersion == ProcessAcceptedSourceVersion.LegacyPreparedLaunch &&
            row.AcceptedSourceHeaderJson is null && row.AcceptedSourceHeaderFingerprint is null && row.Input is null) {
            throw new ProcessAcceptedExecutionInputRequiredException(runId, stepId);
        }
        if (row.AcceptedSourceHeaderJson is not { } headerJson || row.AcceptedSourceHeaderFingerprint is not { } headerFingerprint ||
            row.Input is not { } inputRow) {
            throw new InvalidDataException("The original Process execution input has incomplete accepted source evidence.");
        }
        ProcessAcceptedSourceEnvelope metadata = new(inputRow.PayloadJson, inputRow.Fingerprint);
        var original = ProcessAcceptedSourceCodec.Read(row.AcceptedSourceVersion, new(row.Id), stepId,
            row.PreparationFingerprint, new(headerJson, headerFingerprint), metadata);
        EfProcessAcceptedSource.RequireRuntime(original, row.Runtime);
        if (original.Header.Authority is not { } authority || authority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The original execution input requires its accepted source and database profile.");
        }
        if (inputRow.InputVersion == ProcessAcceptedExecutionInputVersion.NotCaptured &&
            inputRow.InputJson is null && inputRow.InputFingerprint is null) {
            throw new ProcessAcceptedExecutionInputRequiredException(runId, stepId);
        }
        if (inputRow.InputJson is not { } inputJson || inputRow.InputFingerprint is not { } fingerprint) {
            throw new InvalidDataException("The original Process execution input has incomplete captured evidence.");
        }
        var decoded = ProcessAcceptedExecutionInputCodec.Read(inputRow.InputVersion, new(inputJson, fingerprint), headerFingerprint, metadata);
        return new(new(row.Id), original.Header.RootRunId, row.PreparationFingerprint, fingerprint, authority, decoded.Assignment) {
            Requirements = decoded.Requirements
        };
    }
}
