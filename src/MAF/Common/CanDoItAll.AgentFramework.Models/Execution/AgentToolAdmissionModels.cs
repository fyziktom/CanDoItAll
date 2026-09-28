using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Identifier of a batch of agent tool calls proposed together by the model, as an object whose <c>value</c> is a
/// non-empty GUID.
/// </summary>
public readonly record struct AgentToolBatchId {
    [JsonConstructor]
    public AgentToolBatchId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>The batch identifier; never the all-zero GUID.</summary>
    public Guid Value { get; }
}

/// <summary>
/// Identifier of the business intent of one agent tool proposal, as an object whose <c>value</c> is a non-empty GUID.
/// </summary>
public readonly record struct AgentToolBusinessIntentId {
    [JsonConstructor]
    public AgentToolBusinessIntentId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>The intent identifier; never the all-zero GUID.</summary>
    public Guid Value { get; }
}

/// <summary>
/// SHA-256 fingerprint, as an object whose <c>value</c> holds 64 lower-case hexadecimal characters.
/// </summary>
public readonly record struct AgentToolSemanticDigest {
    [JsonConstructor]
    public AgentToolSemanticDigest(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("A tool semantic digest must be a SHA-256 value.", nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    /// <summary>The fingerprint: 64 lower-case hexadecimal characters.</summary>
    public string Value { get; }
}

/// <summary>
/// Agent execution run in which a tool call was proposed: either an interactive turn (with a chat session and an
/// authority snapshot) or a background source such as a process step, never both.
/// </summary>
public sealed record AgentToolSessionReference {
    [JsonConstructor]
    public AgentToolSessionReference(Guid executionRunId, Guid chatSessionId, AgentExecutionAuthorityId authorityId,
        AgentToolBackgroundSourceBinding? backgroundSource = null) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        if (backgroundSource is null) {
            ArgumentOutOfRangeException.ThrowIfEqual(chatSessionId, Guid.Empty);
            ArgumentOutOfRangeException.ThrowIfEqual(authorityId.Value, Guid.Empty);
        } else if (chatSessionId != Guid.Empty || authorityId.Value != Guid.Empty) {
            throw new ArgumentException("A background tool source cannot claim an interactive chat or authority.");
        }
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        AuthorityId = authorityId;
        BackgroundSource = backgroundSource;
    }

    /// <summary>Identifier of the agent execution run; never the all-zero GUID.</summary>
    public Guid ExecutionRunId { get; }

    /// <summary>Identifier of the chat session of an interactive turn; omitted for a background source.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid ChatSessionId { get; }

    /// <summary>
    /// Authority snapshot the interactive turn was admitted under; omitted for a background source.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public AgentExecutionAuthorityId AuthorityId { get; }

    /// <summary>Background owner that started the run; omitted for an interactive turn.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentToolBackgroundSourceBinding? BackgroundSource { get; }
}

/// <summary>
/// Database profile that an agent tool session was admitted under.
/// </summary>
public sealed record AgentToolProfileBinding {
    public AgentToolProfileBinding(Guid profileId, string fingerprint, DatabaseProfileGeneration generation) {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ProfileId = profileId;
        Fingerprint = fingerprint;
        Generation = generation;
    }

    /// <summary>Identifier of the database profile; never the all-zero GUID.</summary>
    public Guid ProfileId { get; }

    /// <summary>Fingerprint of the database profile's runtime settings at admission.</summary>
    public string Fingerprint { get; }

    /// <summary>Generation of the host's active database profile at admission.</summary>
    public DatabaseProfileGeneration Generation { get; }
}

public sealed record AgentToolSessionAdmission(
    AgentToolSessionReference Reference,
    Guid AgentId,
    AgentRuntimeContextPurpose Purpose,
    AgentToolProfileBinding Profile);

public enum AgentToolProposalEffect {
    Read,
    SensitiveDisclosure,
    Mutation
}

public enum AgentToolProposalRecovery {
    RevalidateAndRead,
    OwnerReceipt,
    ReconcileBeforeRetry
}

public sealed record AgentToolPreparedPayload {
    public const int MaximumJsonLength = 4_194_304;
    public const int MaximumSourcePreparationUtf8Bytes = 65_536;

    public AgentToolPreparedPayload(string toolName, int semanticVersion, AgentToolSemanticDigest digest,
        string argumentsJson, AgentToolProposalEffect effect, AgentToolProposalRecovery recovery)
        : this(toolName, semanticVersion, digest, argumentsJson, effect, recovery, null) { }

    [JsonConstructor]
    public AgentToolPreparedPayload(
        string toolName,
        int semanticVersion,
        AgentToolSemanticDigest digest,
        string argumentsJson,
        AgentToolProposalEffect effect,
        AgentToolProposalRecovery recovery,
        AgentToolProtocolEnvelope? sourcePreparation = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentOutOfRangeException.ThrowIfLessThan(semanticVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentsJson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(argumentsJson.Length, MaximumJsonLength);
        if (!Enum.IsDefined(effect) || !Enum.IsDefined(recovery)) {
            throw new ArgumentException("The prepared tool policy is invalid.");
        }
        if (sourcePreparation is not null) {
            ArgumentOutOfRangeException.ThrowIfLessThan(semanticVersion, 2);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                System.Text.Encoding.UTF8.GetByteCount(sourcePreparation.PayloadJson), MaximumSourcePreparationUtf8Bytes);
        }

        ToolName = toolName;
        SemanticVersion = semanticVersion;
        Digest = digest;
        ArgumentsJson = argumentsJson;
        Effect = effect;
        Recovery = recovery;
        SourcePreparation = sourcePreparation;
    }

    public string ToolName { get; }
    public int SemanticVersion { get; }
    public AgentToolSemanticDigest Digest { get; }
    public string ArgumentsJson { get; }
    public AgentToolProposalEffect Effect { get; }
    public AgentToolProposalRecovery Recovery { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentToolProtocolEnvelope? SourcePreparation { get; }

    public override string ToString()
        => $"{ToolName} semantic-v{SemanticVersion} {Digest.Value}";
}

public sealed record AgentToolAdmittedInvocation(
    AgentToolSessionReference Session,
    AgentToolBatchId BatchId,
    AgentToolBusinessIntentId IntentId,
    AgentToolPreparedPayload Payload,
    ExecutionApprovalStatus ApprovalStatus,
    AgentToolSemanticDigest? ApprovedDigest);
