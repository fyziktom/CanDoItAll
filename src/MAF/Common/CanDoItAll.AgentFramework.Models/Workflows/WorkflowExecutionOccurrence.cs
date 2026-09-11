using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record WorkflowExecutionOccurrence {
    [JsonConstructor]
    public WorkflowExecutionOccurrence(WorkflowRunId runId, string path) {
        if (runId.Value == Guid.Empty) {
            throw new ArgumentException("An execution occurrence requires its admitted run.", nameof(runId));
        }

        if (path is not { Length: 64 } || path.Any(character => !char.IsAsciiHexDigit(character))) {
            throw new ArgumentException("An execution occurrence requires a SHA-256 path.", nameof(path));
        }

        RunId = runId;
        Path = path.ToUpperInvariant();
    }

    public WorkflowRunId RunId { get; }
    public string Path { get; }

    public static WorkflowExecutionOccurrence Start(WorkflowRunId runId)
        => new(runId, Hash("workflow-occurrence-v1", runId.ToString()));

    public WorkflowExecutionOccurrence Advance(WorkflowVersionId versionId, WorkflowNodeId nodeId) {
        if (versionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(nodeId.Value)) {
            throw new ArgumentException("An execution occurrence requires its saved workflow version and node.");
        }

        return new(RunId, Hash("workflow-occurrence-v1", Path, versionId.ToString(), nodeId.Value));
    }

    private static string Hash(params string[] parts) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var part in parts) {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
