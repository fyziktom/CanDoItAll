using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessManagerControlLoop
{
    public async Task<ProcessIncidentHandlingResult> RaiseIncidentAsync(
        ProcessIncidentSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ValidateUtc(signal.OccurredAtUtc, nameof(signal.OccurredAtUtc));
        ValidatePayloadHash(signal.PayloadHash, nameof(signal.PayloadHash));

        var duplicate = await dependencies.Incidents.FindByIdempotencyKeyAsync(
            signal.RunId,
            signal.IdempotencyKey,
            cancellationToken);
        if (duplicate is not null)
        {
            var duplicateEvent = CreateEvent(
                duplicate.RootRunId,
                duplicate.RunId,
                duplicate.CorrelationId,
                duplicate.SourceEventId,
                ProcessRuntimeEventTypes.ManagerIncidentRaised,
                duplicate.Sensitivity,
                duplicate.CreatedAtUtc,
                signal.PayloadHash);
            var duplicateDecision = NewDecision(
                duplicate.RootRunId,
                duplicate.RunId,
                duplicate.IncidentId,
                ProcessManagerDecisionKind.IncidentRecorded,
                ProcessManagerDecisionStatus.Duplicate,
                signal.IdempotencyKey,
                duplicateEvent,
                ProcessRecoveryPolicyDenial.None,
                signal.PayloadHash);
            var duplicateWorkItem = NewWorkItem(
                ProcessManagerWorkItemKind.Incident,
                duplicate.RootRunId,
                duplicate.RunId,
                duplicate.CorrelationId,
                duplicate.SourceEventId,
                duplicate.IdempotencyKey,
                ProcessManagerWorkPriority.High,
                duplicate.Sensitivity,
                signal.PayloadHash,
                duplicate.CreatedAtUtc);

            return new ProcessIncidentHandlingResult(
                duplicate,
                duplicateDecision,
                duplicateEvent,
                duplicateWorkItem,
                IsDuplicate: true,
                []);
        }

        var diagnosticReference = await dependencies.Diagnostics.StoreAsync(
            signal.RunId,
            signal.SourceEventId,
            signal.DiagnosticEvidence,
            cancellationToken);
        var incident = new ProcessIncident(
            signal.IncidentId,
            signal.RootRunId,
            signal.RunId,
            signal.SourceEventId,
            signal.StepInstanceId,
            signal.ArtifactSlotId,
            signal.Classification,
            signal.Severity,
            ProcessIncidentStatus.AwaitingPolicy,
            diagnosticReference,
            signal.SafeContent,
            signal.AllowedActions,
            signal.IdempotencyKey,
            signal.CorrelationId,
            diagnosticReference.Sensitivity,
            signal.OccurredAtUtc,
            null);
        var decisionEvent = CreateEvent(
            signal.RootRunId,
            signal.RunId,
            signal.CorrelationId,
            signal.SourceEventId,
            ProcessRuntimeEventTypes.ManagerIncidentRaised,
            diagnosticReference.Sensitivity,
            signal.OccurredAtUtc,
            signal.PayloadHash);
        var decision = NewDecision(
            signal.RootRunId,
            signal.RunId,
            signal.IncidentId,
            ProcessManagerDecisionKind.IncidentRecorded,
            ProcessManagerDecisionStatus.Recorded,
            signal.IdempotencyKey,
            decisionEvent,
            ProcessRecoveryPolicyDenial.None,
            signal.PayloadHash);
        var workItem = NewWorkItem(
            ProcessManagerWorkItemKind.Incident,
            signal.RootRunId,
            signal.RunId,
            signal.CorrelationId,
            signal.SourceEventId,
            signal.IdempotencyKey,
            ProcessManagerWorkPriority.High,
            diagnosticReference.Sensitivity,
            signal.PayloadHash,
            signal.OccurredAtUtc);

        await dependencies.Incidents.SaveAsync(incident, cancellationToken);
        await dependencies.Decisions.SaveAsync(decision, cancellationToken);
        await dependencies.Queue.EnqueueAsync(workItem, cancellationToken);

        return new ProcessIncidentHandlingResult(
            incident,
            decision,
            decisionEvent,
            workItem,
            IsDuplicate: false,
            []);
    }
}
