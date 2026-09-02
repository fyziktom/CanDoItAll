using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

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

    public long Value { get; }

    public static WorkflowExternalRequestVersion Initial { get; } = new(1);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

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

    public WorkflowExternalRequestKind Kind { get; }

    public string SchemaId { get; }

    public int SchemaVersion { get; }

    public string SchemaJson { get; }

    public string SchemaHash { get; }

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
