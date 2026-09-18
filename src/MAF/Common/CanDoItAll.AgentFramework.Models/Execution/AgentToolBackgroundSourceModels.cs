using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

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

    public string SourceKind { get; }
    public string SourceId { get; }
    public AgentToolSemanticDigest OwnerFingerprint { get; }
    public AgentToolSemanticDigest ExecutionFingerprint { get; }
}

public sealed record AgentToolBackgroundInput(string Content);
