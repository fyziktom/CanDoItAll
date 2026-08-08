using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Runtime.Abstractions;

/// <summary>
/// Everything an adapter needs to capture one turn's provider/framework continuation
/// state into a <see cref="RuntimeStateEnvelope"/>. SDK-free: the opaque payload is
/// already serialized by the adapter before this request is built.
/// </summary>
public sealed record AgentRuntimeStateCaptureRequest(
    Guid ProviderProfileId,
    ProviderTransportKind ProviderTransport,
    string Model,
    string ToolsetFingerprint,
    string ContextPolicyFingerprint,
    string PayloadJson,
    DateTimeOffset CapturedAtUtc,
    AgentChatHistoryMode? HistoryMode = null)
{
    /// <summary>Admitted authority policy fingerprint (schema v2 dimension).</summary>
    public string AuthorityPolicyFingerprint { get; init; } = string.Empty;

    /// <summary>Effective capability set fingerprint (schema v2 dimension).</summary>
    public string CapabilityPolicyFingerprint { get; init; } = string.Empty;
}

/// <summary>
/// Outcome of unwrapping a <see cref="RuntimeStateEnvelope"/> that a compatibility policy
/// has already judged restorable. Never inspects or exposes anything beyond the opaque
/// payload the owning adapter itself wrote.
/// </summary>
public sealed record AgentRuntimeStateRestoreResult
{
    private AgentRuntimeStateRestoreResult(bool succeeded, string payloadJson, string failureReason)
    {
        Succeeded = succeeded;
        PayloadJson = payloadJson;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }

    /// <summary>The adapter-opaque payload. Empty when <see cref="Succeeded"/> is false.</summary>
    public string PayloadJson { get; }

    /// <summary>Log-safe failure reason. Empty when <see cref="Succeeded"/> is true.</summary>
    public string FailureReason { get; }

    public static AgentRuntimeStateRestoreResult Restored(string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        return new AgentRuntimeStateRestoreResult(true, payloadJson, string.Empty);
    }

    public static AgentRuntimeStateRestoreResult Failed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new AgentRuntimeStateRestoreResult(false, string.Empty, reason);
    }
}

/// <summary>
/// Adapter that owns serialization/deserialization of one framework's runtime state into
/// the adapter-neutral <see cref="RuntimeStateEnvelope"/>. Implementations live next to the
/// SDK they wrap (for example MAF); this contract itself stays SDK-free so application and
/// domain code never depend on a concrete provider SDK.
/// </summary>
public interface IAgentRuntimeStateAdapter
{
    /// <summary>The <see cref="RuntimeStateEnvelope.AdapterId"/> this adapter owns.</summary>
    string AdapterId { get; }

    /// <summary>Wraps a freshly captured opaque payload into a versioned envelope.</summary>
    RuntimeStateEnvelope CreateEnvelope(AgentRuntimeStateCaptureRequest request);

    /// <summary>
    /// Unwraps an envelope a compatibility policy has already judged restorable. Adapters
    /// still defend their own adapter id/schema range before trusting the payload.
    /// </summary>
    AgentRuntimeStateRestoreResult TryRestore(RuntimeStateEnvelope envelope);
}

/// <summary>
/// Explicit outcome of evaluating a persisted runtime-state envelope (or its absence)
/// against the current run's provider/model/toolset/context-policy identity. Never a
/// heuristic: every outcome is a named, auditable decision.
/// </summary>
public enum RuntimeStateCompatibilityOutcome
{
    /// <summary>The envelope's adapter/schema/provider/model/toolset/context-policy all match; restore its payload.</summary>
    CompatibleRestore,

    /// <summary>The stored state is legacy/unversioned but parseable; wrap it as schema v1 and restore.</summary>
    RegisteredMigration,

    /// <summary>No adapter payload exists at all; replay the canonical transcript/context reference instead.</summary>
    SafeCanonicalReplay,

    /// <summary>The envelope cannot be trusted for this turn; the caller must fail closed.</summary>
    Incompatible
}

/// <summary>
/// A compatibility policy's decision. <see cref="Reason"/> is always log-safe: fingerprints,
/// ids, and enum names only, never raw payload or transcript content.
/// </summary>
public sealed record RuntimeStateCompatibilityDecision(
    RuntimeStateCompatibilityOutcome Outcome,
    string Reason,
    string? MigrationId = null)
{
    private readonly string reason = ValidateReason(Reason);

    public string Reason
    {
        get => reason;
        init => reason = ValidateReason(value);
    }

    private static string ValidateReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return reason;
    }
}

/// <summary>
/// Everything a compatibility policy needs to decide whether a persisted runtime-state
/// envelope may be restored for the current turn. <see cref="Envelope"/> is null whenever the
/// persisted text did not parse as an envelope, including when nothing was ever persisted.
/// <see cref="IsLegacyUnversionedState"/> and <see cref="HasUnparseableStoredState"/> are
/// mutually exclusive flags that disambiguate the three ways <see cref="Envelope"/> can be
/// null: legacy pre-envelope state that at least parses as JSON (<see cref="IsLegacyUnversionedState"/>),
/// state that is present but fails to parse as JSON at all (<see cref="HasUnparseableStoredState"/> —
/// this must resolve to <see cref="RuntimeStateCompatibilityOutcome.Incompatible"/>, never a
/// silent replay), or no state at all (both false).
/// </summary>
public sealed record RuntimeStateCompatibilityRequest(
    RuntimeStateEnvelope? Envelope,
    bool IsLegacyUnversionedState,
    bool HasUnparseableStoredState,
    Guid CurrentProviderProfileId,
    ProviderTransportKind CurrentProviderTransport,
    string CurrentModel,
    string CurrentToolsetFingerprint,
    string CurrentContextPolicyFingerprint,
    AgentChatHistoryMode? CurrentHistoryMode)
{
    /// <summary>Admitted authority policy fingerprint for the current run.</summary>
    public string CurrentAuthorityPolicyFingerprint { get; init; } = string.Empty;

    /// <summary>Effective capability set fingerprint for the current run.</summary>
    public string CurrentCapabilityPolicyFingerprint { get; init; } = string.Empty;

    /// <summary>Names-only toolset fingerprint used to evaluate schema-v1 envelopes.</summary>
    public string CurrentLegacyToolsetNameFingerprint { get; init; } = string.Empty;

    /// <summary>The current adapter package version for the explicit compatibility range.</summary>
    public string CurrentAdapterPackageVersion { get; init; } = string.Empty;
}

/// <summary>
/// Owns the explicit restore/migrate/replay/fail decision for one adapter's runtime state.
/// Implementations are adapter-specific (for example MAF) but the contract stays SDK-free.
/// </summary>
public interface IRuntimeStateCompatibilityPolicy
{
    RuntimeStateCompatibilityDecision Evaluate(RuntimeStateCompatibilityRequest request);
}
