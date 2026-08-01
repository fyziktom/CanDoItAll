using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Core;

public sealed record ProcessRuntimeEventEnvelope(
    RuntimeEventId EventId,
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessCorrelationId CorrelationId,
    RuntimeEventId? CausationId,
    ProcessEventActor Actor,
    string SchemaVersion,
    ProcessEventSensitivity Sensitivity,
    DateTimeOffset OccurredAtUtc,
    ProcessEventType EventType,
    string PayloadHash);

public sealed record ProcessEventActor(
    ProcessEventActorKind Kind,
    ProcessActorId Id);

public enum ProcessEventActorKind
{
    Unknown,
    System,
    User,
    Manager,
    Strategy,
    Driver
}

public enum ProcessEventSensitivity
{
    Unspecified,
    Normal,
    Restricted
}

public static class ProcessRuntimeEventRules
{
    public static ProcessValidationResult Validate(ProcessRuntimeEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var failures = new List<ProcessValidationFailure>();
        if (!string.Equals(envelope.SchemaVersion, ProcessContractVersions.RuntimeEventEnvelopeV1, StringComparison.Ordinal))
        {
            failures.Add(new ProcessValidationFailure(
                "RuntimeEvent.UnsupportedSchema",
                $"Runtime event schema '{envelope.SchemaVersion}' is not supported."));
        }

        if (envelope.Actor.Kind == ProcessEventActorKind.Unknown)
        {
            failures.Add(new ProcessValidationFailure(
                "RuntimeEvent.UnknownActor",
                "Runtime event actor kind must be explicit."));
        }

        if (envelope.Sensitivity == ProcessEventSensitivity.Unspecified)
        {
            failures.Add(new ProcessValidationFailure(
                "RuntimeEvent.MissingSensitivity",
                "Runtime event sensitivity must be explicit."));
        }

        if (envelope.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            failures.Add(new ProcessValidationFailure(
                "RuntimeEvent.TimestampNotUtc",
                "Runtime event timestamp must be UTC."));
        }

        if (string.IsNullOrWhiteSpace(envelope.PayloadHash))
        {
            failures.Add(new ProcessValidationFailure(
                "RuntimeEvent.MissingPayloadHash",
                "Runtime event payload hash must be present."));
        }

        return ProcessValidationResult.From(failures);
    }
}
