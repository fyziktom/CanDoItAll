using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Background owner that started an agent execution run without an interactive chat, such as a process step.
/// </summary>
public sealed record AgentToolBackgroundSourceBinding {
    [JsonConstructor]
    public AgentToolBackgroundSourceBinding(string sourceKind, string sourceId, AgentToolSemanticDigest ownerFingerprint,
        AgentToolSemanticDigest executionFingerprint) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceKind.Length, 128);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceId.Length, 512);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerFingerprint.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionFingerprint.Value);
        SourceKind = sourceKind;
        SourceId = sourceId;
        OwnerFingerprint = ownerFingerprint;
        ExecutionFingerprint = executionFingerprint;
    }

    /// <summary>Kind of the owner, for example <c>process-step</c>; at most 128 characters.</summary>
    public string SourceKind { get; }

    /// <summary>Identifier of the owner; at most 512 characters.</summary>
    public string SourceId { get; }

    /// <summary>Fingerprint of the owner's state recorded when the run was admitted.</summary>
    public AgentToolSemanticDigest OwnerFingerprint { get; }

    /// <summary>Fingerprint of the admitted execution.</summary>
    public AgentToolSemanticDigest ExecutionFingerprint { get; }
}

public sealed record AgentToolBackgroundInput(string Content);
