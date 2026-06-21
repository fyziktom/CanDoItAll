using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRestrictedDiagnosticEvidence(
    string Detail,
    string EvidenceHash,
    ProcessEventSensitivity Sensitivity);

public sealed record ProcessDiagnosticReference(
    ProcessDiagnosticReferenceId ReferenceId,
    ProcessEventSensitivity Sensitivity,
    string EvidenceHash,
    string StorageReference);

public sealed record ProcessIncidentSafeContent(
    string Title,
    string Summary);

public sealed record ProcessIncidentSignal(
    ProcessIncidentId IncidentId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    RuntimeEventId SourceEventId,
    ProcessStepInstanceId? StepInstanceId,
    ArtifactSlotId? ArtifactSlotId,
    ProcessIncidentClassification Classification,
    ProcessIncidentSeverity Severity,
    ProcessRestrictedDiagnosticEvidence DiagnosticEvidence,
    ProcessIncidentSafeContent SafeContent,
    IReadOnlySet<ProcessRecoveryActionKind> AllowedActions,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessCorrelationId CorrelationId,
    DateTimeOffset OccurredAtUtc,
    string PayloadHash);

public sealed record ProcessIncident(
    ProcessIncidentId IncidentId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    RuntimeEventId SourceEventId,
    ProcessStepInstanceId? StepInstanceId,
    ArtifactSlotId? ArtifactSlotId,
    ProcessIncidentClassification Classification,
    ProcessIncidentSeverity Severity,
    ProcessIncidentStatus Status,
    ProcessDiagnosticReference DiagnosticReference,
    ProcessIncidentSafeContent SafeContent,
    IReadOnlySet<ProcessRecoveryActionKind> AllowedActions,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessCorrelationId CorrelationId,
    ProcessEventSensitivity Sensitivity,
    DateTimeOffset CreatedAtUtc,
    RuntimeEventId? ResolutionEventId);

public sealed record ProcessManagerWorkItem(
    ProcessManagerWorkItemId WorkItemId,
    ProcessManagerWorkItemKind Kind,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessCorrelationId CorrelationId,
    RuntimeEventId? CausationId,
    ProcessManagerIdempotencyKey IdempotencyKey,
    ProcessManagerWorkPriority Priority,
    ProcessEventSensitivity Sensitivity,
    string PayloadHash,
    DateTimeOffset EnqueuedAtUtc);
