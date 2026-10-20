using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class EfProcessAcceptedSource {
    internal static void Capture(ProcessPersistenceDbContext context, ProcessPreparedLaunchEntity launch) {
        if (launch.AcceptedAtUtc is not null || launch.AcceptedSourceVersion != ProcessAcceptedSourceVersion.LegacyPreparedLaunch ||
            launch.AcceptedSourceHeaderJson is not null || launch.AcceptedSourceHeaderFingerprint is not null) {
            throw Mismatch("The original Process source can only be captured at first launch acceptance.");
        }
        var projection = ProcessAcceptedSourceCodec.Capture(ProcessPreparedLaunchCodec.Read(launch));
        launch.AcceptedSourceVersion = ProcessAcceptedSourceVersion.OriginalProjection;
        launch.AcceptedSourceHeaderJson = projection.Header.PayloadJson;
        launch.AcceptedSourceHeaderFingerprint = projection.Header.Fingerprint;
        context.AcceptedAssignmentSources.AddRange(projection.Assignments.Select(item => new ProcessAcceptedAssignmentSourceEntity {
            AdmissionId = launch.Id, StepInstanceId = item.Key.Value, PayloadJson = item.Value.PayloadJson, Fingerprint = item.Value.Fingerprint
        }));
    }

    internal static async Task<ProcessAcceptedSource> ReadAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateEntity state, ProcessStepInstanceId stepId, CancellationToken cancellationToken) {
        var admissionId = state.LaunchAdmissionId ?? throw Mismatch("The Process runtime has no original launch admission.");
        var row = await (from launch in context.PreparedLaunches.AsNoTracking()
            where launch.Id == admissionId
            join source in context.AcceptedAssignmentSources.AsNoTracking().Where(item => item.StepInstanceId == stepId.Value)
                on launch.Id equals source.AdmissionId into sources
            from source in sources.DefaultIfEmpty()
            select new {
                Launch = new ProcessPreparedLaunchEntity {
                    Id = launch.Id, RunId = launch.RunId, PlanId = launch.PlanId, CallerIntentId = launch.CallerIntentId,
                    AdmissionSequence = launch.AdmissionSequence, RequestFingerprint = launch.RequestFingerprint,
                    PreparationFingerprint = launch.PreparationFingerprint, PreparedAtUtc = launch.PreparedAtUtc,
                    AcceptedAtUtc = launch.AcceptedAtUtc, Execute = launch.Execute, State = launch.State,
                    LinkDeliveryState = launch.LinkDeliveryState, DeliveredLinkId = launch.DeliveredLinkId,
                    LinkConflictReason = launch.LinkConflictReason, PublicFailure = launch.PublicFailure,
                    AcceptedSourceVersion = launch.AcceptedSourceVersion, AcceptedSourceHeaderJson = launch.AcceptedSourceHeaderJson,
                    AcceptedSourceHeaderFingerprint = launch.AcceptedSourceHeaderFingerprint,
                    PayloadJson = launch.AcceptedSourceVersion == ProcessAcceptedSourceVersion.LegacyPreparedLaunch ? launch.PayloadJson : string.Empty
                },
                Assignment = source == null ? null : new ProcessAcceptedSourceEnvelope(source.PayloadJson, source.Fingerprint)
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw Mismatch("The Process runtime points to a missing original launch admission.");
        var saved = row.Launch;
        if (saved.AcceptedAtUtc is null || saved.RunId != state.RunId || saved.PlanId != state.PlanId) {
            throw Mismatch("The Process runtime does not match its accepted launch admission.");
        }
        var original = saved.AcceptedSourceVersion switch {
            ProcessAcceptedSourceVersion.LegacyPreparedLaunch when saved.AcceptedSourceHeaderJson is null &&
                saved.AcceptedSourceHeaderFingerprint is null && row.Assignment is null
                => ProcessAcceptedSourceCodec.ReadLegacy(ProcessPreparedLaunchCodec.Read(saved), stepId),
            ProcessAcceptedSourceVersion.OriginalProjection when saved.AcceptedSourceHeaderJson is { } payload &&
                saved.AcceptedSourceHeaderFingerprint is { } fingerprint && row.Assignment is { } assignment
                => ProcessAcceptedSourceCodec.Read(saved.AcceptedSourceVersion, new(saved.Id), stepId,
                    saved.PreparationFingerprint, new(payload, fingerprint), assignment),
            _ => throw Mismatch("The accepted Process source version has missing, inconsistent or unsupported evidence.")
        };
        var header = original.Header;
        if (header.RunId.Value != state.RunId || header.RootRunId.Value != state.RootRunId || header.PlanId.Value != state.PlanId ||
            header.PlanHash != state.PlanHash || header.ProjectAdmission != ProcessPersistenceMappers.ReadProjectAdmission(state)) {
            throw Mismatch("The original Process source does not match its exact runtime or project lifetime.");
        }
        return original;
    }

    private static ProcessExecutionAuthorityMismatchException Mismatch(string message) => new(message);
}
