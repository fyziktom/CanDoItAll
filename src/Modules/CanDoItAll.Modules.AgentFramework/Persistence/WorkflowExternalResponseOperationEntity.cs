using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowExternalResponseOperationEntity : IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid RunId { get; set; }

    public long ExpectedRequestVersion { get; set; }

    public string IdempotencyKeyHash { get; set; } = string.Empty;

    public string ResponsePayloadHash { get; set; } = string.Empty;

    public string ActorScopeFingerprint { get; set; } = string.Empty;

    public string ProtectedResponsePayload { get; set; } = string.Empty;

    public int ActorKind { get; set; }

    public string ActorSubjectId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public int State { get; set; }

    public int Attempt { get; set; }

    public long OperationVersion { get; set; }

    public DateTimeOffset AcceptedAtUtc { get; set; }

    public string? LeaseOwnerId { get; set; }

    public long LeaseEpoch { get; set; }

    public DateTimeOffset? LeaseAcquiredAtUtc { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int OutcomeCode { get; set; }

    public string SafeMessage { get; set; } = string.Empty;

    public string? FinalResultJson { get; set; }

    public int ReplayCount { get; set; }

    public DateTimeOffset? LastReplayedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
