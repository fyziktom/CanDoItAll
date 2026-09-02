using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct WorkflowExternalResponseOperationId
{
    [JsonConstructor]
    public WorkflowExternalResponseOperationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Workflow external response operation id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static WorkflowExternalResponseOperationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkflowExternalResponseIdempotencyKey
{
    private const int MaximumLength = 256;

    [JsonConstructor]
    public WorkflowExternalResponseIdempotencyKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Workflow external response idempotency key cannot exceed {MaximumLength} characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => "[REDACTED]";
}

public readonly record struct WorkflowExternalResponseIdempotencyKeyHash
{
    [JsonConstructor]
    public WorkflowExternalResponseIdempotencyKeyHash(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExternalResponsePayloadHash
{
    [JsonConstructor]
    public WorkflowExternalResponsePayloadHash(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExternalResponseActorScopeFingerprint
{
    [JsonConstructor]
    public WorkflowExternalResponseActorScopeFingerprint(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExternalResponsePayload
{
    [JsonConstructor]
    public WorkflowExternalResponsePayload(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var _ = JsonDocument.Parse(json);
        Json = json;
    }

    public string Json { get; }

    public override string ToString() => "[REDACTED]";
}

public readonly record struct WorkflowExternalResponseOperationConcurrencyVersion
{
    [JsonConstructor]
    public WorkflowExternalResponseOperationConcurrencyVersion(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Workflow external response operation concurrency version cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public static WorkflowExternalResponseOperationConcurrencyVersion Initial { get; } = new(0);

    public WorkflowExternalResponseOperationConcurrencyVersion Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowExternalResponseLeaseOwnerId
{
    [JsonConstructor]
    public WorkflowExternalResponseLeaseOwnerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow external response lease owner id cannot contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExternalResponseLeaseEpoch
{
    [JsonConstructor]
    public WorkflowExternalResponseLeaseEpoch(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Workflow external response lease epoch must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public WorkflowExternalResponseLeaseEpoch Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
