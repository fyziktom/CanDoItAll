using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct AgentToolBatchId {
    [JsonConstructor]
    public AgentToolBatchId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct AgentToolBusinessIntentId {
    [JsonConstructor]
    public AgentToolBusinessIntentId(Guid value) {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct AgentToolSemanticDigest {
    [JsonConstructor]
    public AgentToolSemanticDigest(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("A tool semantic digest must be a SHA-256 value.", nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }
}

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

    public Guid ExecutionRunId { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid ChatSessionId { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public AgentExecutionAuthorityId AuthorityId { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentToolBackgroundSourceBinding? BackgroundSource { get; }
}

public sealed record AgentToolProfileBinding {
    public AgentToolProfileBinding(Guid profileId, string fingerprint, DatabaseProfileGeneration generation) {
        ArgumentOutOfRangeException.ThrowIfEqual(profileId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ProfileId = profileId;
        Fingerprint = fingerprint;
        Generation = generation;
    }

    public Guid ProfileId { get; }
    public string Fingerprint { get; }
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

    public AgentToolPreparedPayload(
        string toolName,
        int semanticVersion,
        AgentToolSemanticDigest digest,
        string argumentsJson,
        AgentToolProposalEffect effect,
        AgentToolProposalRecovery recovery) {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentOutOfRangeException.ThrowIfLessThan(semanticVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(digest.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentsJson);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(argumentsJson.Length, MaximumJsonLength);
        if (!Enum.IsDefined(effect) || !Enum.IsDefined(recovery)) {
            throw new ArgumentException("The prepared tool policy is invalid.");
        }

        ToolName = toolName;
        SemanticVersion = semanticVersion;
        Digest = digest;
        ArgumentsJson = argumentsJson;
        Effect = effect;
        Recovery = recovery;
    }

    public string ToolName { get; }
    public int SemanticVersion { get; }
    public AgentToolSemanticDigest Digest { get; }
    public string ArgumentsJson { get; }
    public AgentToolProposalEffect Effect { get; }
    public AgentToolProposalRecovery Recovery { get; }

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
