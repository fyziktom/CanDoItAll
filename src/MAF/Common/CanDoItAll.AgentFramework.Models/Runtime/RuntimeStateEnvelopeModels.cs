using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Well-known runtime adapter identifiers.
/// </summary>
public static class RuntimeStateAdapterIds
{
    public const string Maf = "maf";
}

/// <summary>
/// Versioned, adapter-neutral container for provider/framework continuation
/// state. The payload is opaque to application and domain code; only the
/// owning adapter's serializer/compatibility policy may interpret it. A
/// mismatched envelope is migrated explicitly or rejected — never silently
/// reset or replayed. The presence of adapter state does not override the
/// execution run or the conversation binding, and approval identifiers remain
/// application-owned.
/// </summary>
public sealed record RuntimeStateEnvelope
{
    /// <summary>
    /// Schema v2 adds the separated authority-policy and capability-policy
    /// fingerprints and switches the toolset fingerprint to the tool-contract
    /// hash. v1 envelopes (names-only toolset fingerprint, no policy split)
    /// remain readable through the adapter's explicit compatibility rules.
    /// </summary>
    public const int CurrentSchemaVersion = 2;
    public const int MaximumAdapterIdLength = 100;
    public const int MaximumIdentifierLength = 200;

    public RuntimeStateEnvelope(
        string adapterId,
        int schemaVersion,
        string adapterPackageVersion,
        Guid providerProfileId,
        ProviderTransportKind providerTransport,
        string model,
        string toolsetFingerprint,
        string contextPolicyFingerprint,
        DateTimeOffset createdAtUtc,
        string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        if (adapterId.Trim().Length > MaximumAdapterIdLength)
        {
            throw new ArgumentException(
                $"A runtime state adapter id cannot exceed {MaximumAdapterIdLength} characters.",
                nameof(adapterId));
        }

        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "A runtime state schema version must be positive.");
        }

        if (providerProfileId == Guid.Empty)
        {
            throw new ArgumentException("A provider profile id is required.", nameof(providerProfileId));
        }

        if (!Enum.IsDefined(providerTransport))
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerTransport),
                providerTransport,
                "Unknown provider transport kind.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (model.Trim().Length > MaximumIdentifierLength)
        {
            throw new ArgumentException(
                $"A runtime state model identifier cannot exceed {MaximumIdentifierLength} characters.",
                nameof(model));
        }

        var normalizedToolsetFingerprint = toolsetFingerprint?.Trim() ?? string.Empty;
        if (normalizedToolsetFingerprint.Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A toolset fingerprint cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                nameof(toolsetFingerprint));
        }

        var normalizedContextPolicyFingerprint = contextPolicyFingerprint?.Trim() ?? string.Empty;
        if (normalizedContextPolicyFingerprint.Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A context policy fingerprint cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                nameof(contextPolicyFingerprint));
        }

        ArgumentNullException.ThrowIfNull(payloadJson);

        AdapterId = adapterId.Trim();
        SchemaVersion = schemaVersion;
        AdapterPackageVersion = adapterPackageVersion?.Trim() ?? string.Empty;
        ProviderProfileId = providerProfileId;
        ProviderTransport = providerTransport;
        Model = model.Trim();
        ToolsetFingerprint = normalizedToolsetFingerprint;
        ContextPolicyFingerprint = normalizedContextPolicyFingerprint;
        CreatedAtUtc = createdAtUtc;
        PayloadJson = payloadJson;
    }

    public string AdapterId { get; }

    public int SchemaVersion { get; }

    public string AdapterPackageVersion { get; }

    public Guid ProviderProfileId { get; }

    public ProviderTransportKind ProviderTransport { get; }

    public string Model { get; }

    public string ToolsetFingerprint { get; }

    public string ContextPolicyFingerprint { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Opaque adapter payload. Never inspected outside the owning adapter.</summary>
    public string PayloadJson { get; }

    private readonly AgentChatHistoryMode? historyMode;

    /// <summary>
    /// The agent's chat-history mode at capture time (source: <c>agent.ChatHistoryMode</c>).
    /// Optional and serialization-safe: envelopes written before this field existed simply
    /// omit it, and readers must not infer a default that widens restore eligibility.
    /// </summary>
    public AgentChatHistoryMode? HistoryMode
    {
        get => historyMode;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown chat history mode.");
            }

            historyMode = value;
        }
    }

    private readonly string authorityPolicyFingerprint = string.Empty;

    /// <summary>
    /// Fingerprint of the admitted execution authority policy at capture time
    /// (schema v2). Separate from the model-context digest so an authority
    /// policy change invalidates state even when the visible UI context is
    /// unchanged. Empty for v1 envelopes and for runs without an admitted
    /// authority.
    /// </summary>
    public string AuthorityPolicyFingerprint
    {
        get => authorityPolicyFingerprint;
        init => authorityPolicyFingerprint = NormalizeFingerprint(value, nameof(AuthorityPolicyFingerprint));
    }

    private readonly string capabilityPolicyFingerprint = string.Empty;

    /// <summary>
    /// Fingerprint of the effectively exposed capability set at capture time
    /// (schema v2). Empty for v1 envelopes.
    /// </summary>
    public string CapabilityPolicyFingerprint
    {
        get => capabilityPolicyFingerprint;
        init => capabilityPolicyFingerprint = NormalizeFingerprint(value, nameof(CapabilityPolicyFingerprint));
    }

    private static string NormalizeFingerprint(string? value, string propertyName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > AgentChatContextLimits.MaximumFingerprintLength)
        {
            throw new ArgumentException(
                $"A fingerprint cannot exceed {AgentChatContextLimits.MaximumFingerprintLength} characters.",
                propertyName);
        }

        return normalized;
    }

    /// <summary>
    /// Whether this envelope belongs to the given adapter at a schema version
    /// the adapter declares readable. Callers decide migration or rejection;
    /// this record never migrates silently.
    /// </summary>
    public bool IsCompatibleWith(string adapterId, int minimumSchemaVersion, int maximumSchemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        if (minimumSchemaVersion <= 0 || maximumSchemaVersion < minimumSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumSchemaVersion),
                maximumSchemaVersion,
                "A schema version compatibility range must be positive and ordered.");
        }

        return string.Equals(AdapterId, adapterId.Trim(), StringComparison.OrdinalIgnoreCase)
            && SchemaVersion >= minimumSchemaVersion
            && SchemaVersion <= maximumSchemaVersion;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Canonical JSON projection persisted at the session-persistence seam.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Attempts to parse persisted text as a <see cref="RuntimeStateEnvelope"/>. Returns
    /// false for anything that is not this exact envelope shape (including pre-envelope
    /// legacy payloads and malformed JSON) — callers decide what "false" means for their
    /// restore path; this method never guesses.
    /// </summary>
    public static bool TryParse(string? json, out RuntimeStateEnvelope? envelope)
    {
        envelope = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var candidate = JsonSerializer.Deserialize<RuntimeStateEnvelope>(json, SerializerOptions);
            if (candidate is null)
            {
                return false;
            }

            envelope = candidate;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}
