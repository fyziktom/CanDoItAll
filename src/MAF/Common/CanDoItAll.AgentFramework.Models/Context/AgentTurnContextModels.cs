using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifies one immutable turn context capture.
/// </summary>
public readonly record struct AgentTurnContextId
{
    [JsonConstructor]
    public AgentTurnContextId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A turn context id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentTurnContextId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

/// <summary>
/// Durable, serialization-safe reference to the immutable context of one
/// admitted turn. It carries identities, versions, and digests only — never
/// raw model context, opaque attachments, service handles, or secrets. The
/// exact bounded payload stays in the request-scoped runtime lease
/// (fail-closed when lost) exactly like the current transient context lease.
/// Invariants: one turn has one digest; the digest cannot change; approval
/// continuation reuses this same reference; later UI switches never retarget
/// an admitted run.
/// </summary>
public sealed record AgentTurnContextReference
{
    public const int CurrentSchemaVersion = 1;

    public AgentTurnContextReference(
        AgentTurnContextId turnContextId,
        AgentContextEpochId contextEpochId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        string surface,
        string view,
        long observationVersion,
        string modelContextDigest,
        DateTimeOffset capturedAtUtc,
        AgentUiObservationId? observationId = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (turnContextId.IsEmpty)
        {
            throw new ArgumentException("A turn context id is required.", nameof(turnContextId));
        }

        if (contextEpochId.IsEmpty)
        {
            throw new ArgumentException("A context epoch id is required.", nameof(contextEpochId));
        }

        if (sourceKind.IsEmpty)
        {
            throw new ArgumentException("A turn context source kind is required.", nameof(sourceKind));
        }

        if (sourceId.IsEmpty)
        {
            throw new ArgumentException("A turn context source id is required.", nameof(sourceId));
        }

        if (observationVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observationVersion),
                observationVersion,
                "An observation version must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(modelContextDigest);
        if (modelContextDigest.Trim().Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A model context digest cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                nameof(modelContextDigest));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "A turn context schema version must be positive.");
        }

        TurnContextId = turnContextId;
        ContextEpochId = contextEpochId;
        SourceKind = sourceKind;
        SourceId = sourceId;
        Surface = surface?.Trim() ?? string.Empty;
        View = view?.Trim() ?? string.Empty;
        ObservationVersion = observationVersion;
        ModelContextDigest = modelContextDigest.Trim();
        CapturedAtUtc = capturedAtUtc;
        ObservationId = observationId;
        SchemaVersion = schemaVersion;
    }

    public AgentTurnContextId TurnContextId { get; }

    public AgentContextEpochId ContextEpochId { get; }

    public AgentChatContextSourceKind SourceKind { get; }

    public AgentChatContextSourceId SourceId { get; }

    public string Surface { get; }

    public string View { get; }

    public long ObservationVersion { get; }

    public string ModelContextDigest { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public AgentUiObservationId? ObservationId { get; }

    public int SchemaVersion { get; }
}

/// <summary>
/// Runtime-only descriptor binding one admitted turn to its observation,
/// conversation binding, transition, and resolved authority identity. This
/// object is request-scoped: it is never serialized into durable run or chat
/// state. Durable state stores <see cref="AgentTurnContextReference"/> and the
/// safe authority identity/fingerprint only.
/// </summary>
public sealed record AgentTurnContextCapture
{
    public AgentTurnContextCapture(
        AgentTurnContextReference reference,
        AgentUiObservationSnapshot observation,
        AgentContextTransition transition,
        AgentConversationContextBinding? conversationBinding,
        AgentExecutionAuthorityRecord authority)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(authority);

        if (observation.PublicationVersion != reference.ObservationVersion)
        {
            throw new ArgumentException(
                "A turn context capture must reference the exact captured observation version.",
                nameof(observation));
        }

        Reference = reference;
        Observation = observation;
        Transition = transition;
        ConversationBinding = conversationBinding;
        Authority = authority;
    }

    public AgentTurnContextReference Reference { get; }

    public AgentUiObservationSnapshot Observation { get; }

    public AgentContextTransition Transition { get; }

    public AgentConversationContextBinding? ConversationBinding { get; }

    public AgentExecutionAuthorityRecord Authority { get; }
}
