using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionInitialSourceCodec {
    public const string Format = "process-decision-initial-source";
    public const int Version = 1;
    public const int MaximumUtf8Bytes = 4096;
    public const int MaximumStepKeyCharacters = 256;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, MaxDepth = 4
    };

    public static string Serialize(ProcessExecutionClaimEvidence evidence) {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.ExecutionRunId == Guid.Empty || evidence.ExecutorAgentId == Guid.Empty ||
            evidence.RunId.Value == Guid.Empty || evidence.StepInstanceId.Value == Guid.Empty ||
            evidence.DispatchClaimToken == Guid.Empty || evidence.StepKey is not { Length: > 0 and <= MaximumStepKeyCharacters } ||
            string.IsNullOrWhiteSpace(evidence.StepKey) ||
            evidence.ExecutionCreatedAtUtc == default || evidence.ExecutionCreatedAtUtc.Offset != TimeSpan.Zero ||
            !evidence.ExecutionMayDispatch) {
            throw new InvalidDataException("Initial Process evidence requires its exact dispatchable child, claim and UTC creation time.");
        }
        var json = JsonSerializer.Serialize(new Wire(Version, evidence.ExecutionRunId, evidence.ExecutorAgentId,
            evidence.RunId.Value, evidence.StepInstanceId.Value, evidence.DispatchClaimToken, evidence.StepKey,
            evidence.ExecutionCreatedAtUtc, evidence.ExecutionMayDispatch), Options);
        RequireSize(json);
        return json;
    }

    public static ProcessExecutionClaimEvidence Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        RequireSize(json);
        try {
            var wire = JsonSerializer.Deserialize<Wire>(json, Options)
                ?? throw new InvalidDataException("Initial Process evidence is absent.");
            if (wire.Version != Version) {
                throw new InvalidDataException("The initial Process evidence version is unsupported.");
            }
            ProcessExecutionClaimEvidence evidence = new(wire.ExecutionRunId, wire.ExecutorAgentId,
                new(wire.RunId), new(wire.StepInstanceId), wire.DispatchClaimToken, wire.StepKey,
                wire.ExecutionCreatedAtUtc, wire.ExecutionMayDispatch);
            if (Serialize(evidence) != json) {
                throw new InvalidDataException("Initial Process evidence is not canonical.");
            }
            return evidence;
        } catch (Exception failure) when (failure is JsonException or ArgumentException) {
            throw new InvalidDataException("Initial Process evidence is malformed.", failure);
        }
    }

    private static void RequireSize(string json) {
        if (json.Length > MaximumUtf8Bytes || Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes) {
            throw new InvalidDataException("Initial Process evidence exceeds its byte bound.");
        }
    }

    private sealed record Wire(int Version, Guid ExecutionRunId, Guid ExecutorAgentId, Guid RunId,
        Guid StepInstanceId, Guid DispatchClaimToken, string StepKey, DateTimeOffset ExecutionCreatedAtUtc, bool ExecutionMayDispatch);
}
