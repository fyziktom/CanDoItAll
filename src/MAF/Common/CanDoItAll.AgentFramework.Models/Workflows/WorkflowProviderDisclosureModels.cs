using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public static class WorkflowProviderDisclosureProtocol {
    public static WorkflowCompilerContractVersion Legacy { get; } = new(1);
    public static WorkflowCompilerContractVersion Current { get; } = new(2);
    public const int MaximumEvidencePayloadBytes = 65_536;

    public static void RequireSupported(WorkflowCompilerContractVersion version) {
        if (version != Legacy && version != Current) {
            throw new InvalidOperationException("The Workflow provider-disclosure compiler contract is unsupported.");
        }
    }
}

public readonly record struct WorkflowDisclosureOwnerId {
    [JsonConstructor]
    public WorkflowDisclosureOwnerId(string value) {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))) {
            throw new ArgumentException("A Workflow disclosure owner requires a bounded stable identifier.", nameof(value));
        }
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutionContentHash {
    [JsonConstructor]
    public WorkflowExecutionContentHash(string value) {
        if (value is not { Length: 64 } || value.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("A Workflow execution content hash requires SHA-256.", nameof(value));
        }
        Value = value.ToUpperInvariant();
    }

    public string Value { get; }
    public static WorkflowExecutionContentHash Compute(string value) {
        ArgumentNullException.ThrowIfNull(value);
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    }

    public void Validate() => _ = new WorkflowExecutionContentHash(Value);
}

public sealed record WorkflowRunDisclosureDeclaration(
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowExecutionContentHash DefinitionHash,
    WorkflowExecutionContentHash SourceHash,
    WorkflowCompilerContractVersion CompilerVersion) {
    public ImmutableArray<WorkflowNodeSimulationAdmission> Simulations { get; init; } = [];

    public void Validate() {
        if (RunId.Value == Guid.Empty || WorkflowId.Value == Guid.Empty || VersionId.Value == Guid.Empty) {
            throw new InvalidOperationException("A Workflow disclosure declaration requires its original run and definition.");
        }
        WorkflowProviderDisclosureProtocol.RequireSupported(CompilerVersion);
        DefinitionHash.Validate();
        SourceHash.Validate();
        if (Simulations.IsDefault || Simulations.Any(value => value is null) ||
                Simulations.Select(value => value.NodeId).Distinct().Count() != Simulations.Length) {
            throw new InvalidOperationException("The Workflow simulation admission is missing or ambiguous.");
        }
        foreach (var simulation in Simulations) {
            simulation.Validate();
        }
    }
}

public sealed record WorkflowNodeSimulationAdmission(WorkflowNodeId NodeId, WorkflowExecutionContentHash Hash) {
    public WorkflowPreviewSimulationStep? Step { get; init; }

    public void Validate() {
        if (string.IsNullOrWhiteSpace(NodeId.Value)) {
            throw new InvalidOperationException("A Workflow simulation admission requires its exact node.");
        }
        Hash.Validate();
        if (Step is not null && (Step.NodeId != NodeId || WorkflowProviderDisclosureContent.Simulation(Step) != Hash)) {
            throw new InvalidOperationException("The retained Workflow simulation differs from its original admitted step.");
        }
    }
}

public sealed record WorkflowNodeCompletionProof(
    Guid CompletionId,
    WorkflowExecutionOccurrence Occurrence,
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowExecutionContentHash DefinitionHash,
    WorkflowExecutionContentHash SettingsHash,
    WorkflowExecutionContentHash InputHash,
    WorkflowExecutionContentHash ResultHash,
    WorkflowExecutionContentHash SourceHash,
    WorkflowCompilerContractVersion CompilerVersion) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowExecutionContentHash? SimulationHash { get; init; }

    public void Validate() {
        if (CompletionId == Guid.Empty || Occurrence is null || WorkflowId.Value == Guid.Empty ||
                VersionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(NodeId.Value)) {
            throw new InvalidOperationException("A completed Workflow observation requires its actual occurrence and node.");
        }
        WorkflowProviderDisclosureProtocol.RequireSupported(CompilerVersion);
        DefinitionHash.Validate();
        SettingsHash.Validate();
        InputHash.Validate();
        ResultHash.Validate();
        SourceHash.Validate();
        SimulationHash?.Validate();
    }
}

public sealed record WorkflowProviderReadEvidence(
    WorkflowDisclosureOwnerId Owner,
    int SchemaVersion,
    WorkflowExecutionOccurrence Occurrence,
    WorkflowVersionId VersionId,
    WorkflowNodeId NodeId,
    string PayloadJson) {
    public void Validate() {
        _ = new WorkflowDisclosureOwnerId(Owner.Value);
        if (SchemaVersion <= 0 || Occurrence is null || VersionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(NodeId.Value) ||
                string.IsNullOrWhiteSpace(PayloadJson) || Encoding.UTF8.GetByteCount(PayloadJson) > WorkflowProviderDisclosureProtocol.MaximumEvidencePayloadBytes) {
            throw new InvalidOperationException("The Workflow owner read evidence is missing, unsupported or exceeds its record bound.");
        }
        using var document = JsonDocument.Parse(PayloadJson);
    }
}

public sealed record WorkflowReadEvidenceManifest(int PartCount, WorkflowExecutionContentHash Hash) {
    public void Validate() {
        if (PartCount <= 0) {
            throw new InvalidOperationException("A Workflow read-evidence manifest requires all of its bounded parts.");
        }
        Hash.Validate();
    }
}

public sealed record WorkflowCompletedNodeRead(
    WorkflowNodeCompletionProof Proof,
    WorkflowReadEvidenceManifest? Manifest,
    IReadOnlyList<WorkflowProviderReadEvidence> Evidence);

public sealed record WorkflowProviderDisclosureHistory(
    WorkflowRunDisclosureDeclaration? Declaration,
    IReadOnlyList<WorkflowCompletedNodeRead> Completions,
    IReadOnlyList<WorkflowNodeId> UnprovenCompletedNodeIds);

public static class WorkflowProviderDisclosureContent {
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static WorkflowExecutionContentHash Simulation(WorkflowPreviewSimulationStep simulation)
        => WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(simulation, JsonOptions));

    public static WorkflowExecutionContentHash Definition(WorkflowDefinition definition)
        => WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(definition, JsonOptions));

    public static WorkflowExecutionContentHash Source(WorkflowLaunchOrigin? origin)
        => WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(origin, JsonOptions));

    public static WorkflowExecutionContentHash Settings(WorkflowNode node)
        => WorkflowExecutionContentHash.Compute(JsonSerializer.Serialize(node.Settings, JsonOptions));

    public static WorkflowReadEvidenceManifest? Manifest(IReadOnlyList<WorkflowProviderReadEvidence> evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0) {
            return null;
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var part in evidence) {
            ArgumentNullException.ThrowIfNull(part);
            part.Validate();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(part, JsonOptions);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return new(evidence.Count, new(Convert.ToHexString(hash.GetHashAndReset())));
    }

    public static void RequireEvidenceMatches(WorkflowNodeCompletionProof proof, IReadOnlyList<WorkflowProviderReadEvidence> evidence) {
        proof.Validate();
        foreach (var part in evidence) {
            part.Validate();
            if (part.Occurrence != proof.Occurrence || part.VersionId != proof.VersionId || part.NodeId != proof.NodeId) {
                throw new InvalidOperationException("Workflow owner evidence belongs to a different actual node occurrence.");
            }
        }
    }
}
