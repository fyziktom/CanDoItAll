using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// Version of an external request, as an object whose <c>value</c> member holds the positive integer. The public
/// projections return it as the plain integer <c>version</c>.
/// </summary>
public readonly record struct WorkflowExternalRequestVersion
{
    public WorkflowExternalRequestVersion(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Workflow external request version must be positive.");
        }

        Value = value;
    }

    /// <summary>The request version, starting at 1.</summary>
    public long Value { get; }

    public static WorkflowExternalRequestVersion Initial { get; } = new(1);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// State of an external request of a workflow run, as a JSON integer: 0 Pending (accepts a response),
/// 1 ResponseClaimed (an answer is being processed), 2 Responded, 3 Denied, 4 Superseded (replaced by a newer
/// request), 5 Cancelled, 6 LegacyNonResumable (an old wait that the response operation cannot resume).
/// </summary>
public enum WorkflowExternalRequestState
{
    Pending,
    ResponseClaimed,
    Responded,
    Denied,
    Superseded,
    Cancelled,
    LegacyNonResumable
}

/// <summary>
/// Stored response contract of an external request: the schema and size limit a response must satisfy. It appears in
/// full, including the schema text and hash, only in the stored request records of test-run results; the public
/// request projection returns a bounded form of it.
/// </summary>
public sealed record WorkflowExternalResponseContract
{
    public WorkflowExternalResponseContract(
        WorkflowExternalRequestKind kind,
        string schemaId,
        int schemaVersion,
        string schemaJson,
        int maximumPayloadBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        if (maximumPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes));
        }

        using var _ = JsonDocument.Parse(schemaJson);
        Kind = kind;
        SchemaId = schemaId.Trim();
        SchemaVersion = schemaVersion;
        SchemaJson = schemaJson;
        SchemaHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schemaJson)));
        MaximumPayloadBytes = maximumPayloadBytes;
    }

    /// <summary>
    /// Kind of request the contract belongs to, as a JSON integer: 0 HumanInput, 1 Approval, 2 ToolApproval.
    /// </summary>
    public WorkflowExternalRequestKind Kind { get; }

    /// <summary>
    /// Identifier of the response schema, for example <c>CanDoItAll.WorkflowApprovalResponse/v1</c>; trimmed.
    /// </summary>
    public string SchemaId { get; }

    /// <summary>
    /// Version of the response schema; positive, and 1 is the only version responses are accepted for.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>The JSON Schema of the response as JSON text in a string.</summary>
    public string SchemaJson { get; }

    /// <summary>SHA-256 hash of <c>schemaJson</c>, as lower-case hexadecimal.</summary>
    public string SchemaHash { get; }

    /// <summary>Maximum size of a response value's JSON text, in UTF-8 bytes; positive.</summary>
    public int MaximumPayloadBytes { get; }
}

public sealed record WorkflowExternalRequestContinuation(
    WorkflowBackendExternalRequestLink Request,
    WorkflowBackendCheckpointLink Checkpoint,
    WorkflowCompilerContractVersion CompilerContractVersion,
    WorkflowTopologyFingerprint TopologyFingerprint,
    WorkflowBackendCheckpointPayloadHash CheckpointPayloadHash);

public sealed record WorkflowExternalRequestAuthorizationPolicySnapshot(
    WorkflowLaunchActor? OriginActor,
    WorkflowExecutorId? ExecutorId,
    WorkflowExecutorCapabilityFlags RequiredCapabilities,
    WorkflowExecutorApprovalRequirement ApprovalRequirement,
    string IntendedApproverSubjectId)
{
    public string IntendedApproverSubjectId { get; init; } =
        string.IsNullOrWhiteSpace(IntendedApproverSubjectId)
            ? string.Empty
            : IntendedApproverSubjectId.Trim();

    public WorkspaceScopeDescriptor? AuthorizationScope { get; init; }

    public string AuthorizationPolicyFingerprint { get; init; } = string.Empty;

    public int ResponseAuthorizationLifetimeSeconds { get; init; }
}
