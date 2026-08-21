using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct WorkflowBackendSessionId
{
    [JsonConstructor]
    public WorkflowBackendSessionId(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireOpaque(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowBackendCheckpointId
{
    [JsonConstructor]
    public WorkflowBackendCheckpointId(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireOpaque(value, nameof(value));
    }

    public string Value { get; }

    public static WorkflowBackendCheckpointId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public readonly record struct WorkflowBackendRequestId
{
    [JsonConstructor]
    public WorkflowBackendRequestId(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireOpaque(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowBackendRequestPortId
{
    [JsonConstructor]
    public WorkflowBackendRequestPortId(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireOpaque(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowCheckpointCommitOrdinal
{
    public WorkflowCheckpointCommitOrdinal(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Workflow checkpoint commit ordinal cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowBackendCheckpointFormat
{
    public WorkflowBackendCheckpointFormat(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireOpaque(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowBackendCheckpointFormatVersion
{
    public WorkflowBackendCheckpointFormatVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Workflow checkpoint format version must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowCompilerContractVersion
{
    [JsonConstructor]
    public WorkflowCompilerContractVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Workflow compiler contract version must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowTopologyFingerprint
{
    [JsonConstructor]
    public WorkflowTopologyFingerprint(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public static WorkflowTopologyFingerprint Create(string canonicalTopology)
    {
        ArgumentNullException.ThrowIfNull(canonicalTopology);
        return new(WorkflowBackendCheckpointValue.ComputeSha256(canonicalTopology));
    }

    public override string ToString() => Value;
}

public readonly record struct WorkflowBackendCheckpointPayloadHash
{
    [JsonConstructor]
    public WorkflowBackendCheckpointPayloadHash(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public static WorkflowBackendCheckpointPayloadHash Compute(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        return new(WorkflowBackendCheckpointValue.ComputeSha256(payloadJson));
    }

    public override string ToString() => Value;
}

public sealed record WorkflowBackendCheckpointPayload
{
    public WorkflowBackendCheckpointPayload(
        string json,
        WorkflowBackendCheckpointPayloadHash sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var _ = JsonDocument.Parse(json);
        Json = json;
        Sha256 = sha256;
    }

    public string Json { get; }

    public WorkflowBackendCheckpointPayloadHash Sha256 { get; }

    public bool HasValidHash => WorkflowBackendCheckpointPayloadHash.Compute(Json) == Sha256;

    public static WorkflowBackendCheckpointPayload Create(string json)
        => new(json, WorkflowBackendCheckpointPayloadHash.Compute(json));
}

public sealed record WorkflowBackendCheckpointSession(
    WorkflowBackendSessionId Id,
    WorkflowRunId RunId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowRuntimeBackendKind Backend,
    WorkflowBackendCheckpointFormat Format,
    WorkflowBackendCheckpointFormatVersion FormatVersion,
    WorkflowCompilerContractVersion CompilerContractVersion,
    WorkflowTopologyFingerprint TopologyFingerprint);

public sealed record WorkflowBackendCheckpointLink(
    WorkflowBackendSessionId SessionId,
    WorkflowBackendCheckpointId CheckpointId);

public sealed record WorkflowBackendExternalRequestLink(
    WorkflowExternalRequestId ExternalRequestId,
    WorkflowBackendRequestId BackendRequestId,
    WorkflowBackendRequestPortId BackendRequestPortId);

public sealed record WorkflowBackendCheckpointIndexEntry(
    WorkflowBackendCheckpointLink Link,
    WorkflowBackendCheckpointLink? Parent,
    WorkflowCheckpointCommitOrdinal CommitOrdinal,
    DateTimeOffset CreatedAtUtc);

public sealed record WorkflowBackendCheckpointPayloadRecord(
    WorkflowBackendCheckpointSession Session,
    WorkflowBackendCheckpointIndexEntry Index,
    WorkflowBackendCheckpointPayload Payload,
    WorkflowBackendExternalRequestLink? ExternalRequestLink);

internal static class WorkflowBackendCheckpointValue
{
    private const int Sha256HexLength = 64;

    public static string RequireOpaque(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Workflow backend identifiers cannot contain leading or trailing whitespace.", parameterName);
        }

        return value;
    }

    public static string RequireSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != Sha256HexLength || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Workflow SHA-256 values must contain exactly 64 hexadecimal characters.", parameterName);
        }

        return value.ToLowerInvariant();
    }

    public static string ComputeSha256(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
