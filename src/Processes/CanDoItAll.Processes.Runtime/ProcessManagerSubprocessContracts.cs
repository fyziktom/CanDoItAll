using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessArtifactProjectionReference(
    ArtifactSlotId SourceSlotId,
    ArtifactSlotId TargetSlotId,
    ProcessArtifactScope SourceScope,
    ProcessArtifactScope TargetScope,
    string ContentHash);

public sealed record ProcessSubprocessMessageDraft(
    ProcessSubprocessMessageId MessageId,
    ProcessSubprocessMessageKind Kind,
    ProcessSubprocessMessageDirection Direction,
    ProcessRunId ParentRunId,
    ProcessRunId ChildRunId,
    ProcessStepInstanceId? ParentStepInstanceId,
    ProcessCorrelationId CorrelationId,
    RuntimeEventId? CausationId,
    string SchemaVersion,
    ProcessEventSensitivity Sensitivity,
    IReadOnlyList<ProcessArtifactProjectionReference> ArtifactReferences,
    ProcessManagerIdempotencyKey IdempotencyKey,
    string PayloadHash,
    DateTimeOffset OccurredAtUtc);

public sealed record ProcessSubprocessControlMessage(
    ProcessSubprocessMessageId MessageId,
    ProcessSubprocessMessageKind Kind,
    ProcessSubprocessMessageDirection Direction,
    ProcessRunId ParentRunId,
    ProcessRunId ChildRunId,
    ProcessStepInstanceId? ParentStepInstanceId,
    ProcessCorrelationId CorrelationId,
    RuntimeEventId? CausationId,
    string SchemaVersion,
    ProcessEventSensitivity Sensitivity,
    IReadOnlyList<ProcessArtifactProjectionReference> ArtifactReferences,
    ProcessManagerIdempotencyKey IdempotencyKey,
    string PayloadHash,
    DateTimeOffset CreatedAtUtc);
