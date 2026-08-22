using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public readonly record struct WorkflowExternalRequestPayloadHash
{
    [JsonConstructor]
    public WorkflowExternalRequestPayloadHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 64 ||
            value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Workflow external request payload hash must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record WorkflowExternalRequestBoundaryRecord(
    WorkflowExternalRequestId RequestId,
    WorkflowExternalRequestVersion RequestVersion,
    WorkflowExternalRequestState State,
    WorkflowExternalResponseContract ResponseContract,
    WorkflowExternalRequestContinuation Continuation,
    WorkflowExternalRequestPayloadHash RequestPayloadHash,
    DateTimeOffset CreatedAtUtc)
{
    public WorkflowExternalRequestAuthorizationPolicySnapshot? AuthorizationPolicy { get; init; }

    public static bool TryCreate(
        WorkflowExternalRequestRecord request,
        out WorkflowExternalRequestBoundaryRecord? boundary)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State == WorkflowExternalRequestState.LegacyNonResumable ||
            request.ResponseContract is null ||
            request.Continuation is null)
        {
            boundary = null;
            return false;
        }

        boundary = new WorkflowExternalRequestBoundaryRecord(
            request.Id,
            request.Version,
            request.State,
            request.ResponseContract,
            request.Continuation,
            new WorkflowExternalRequestPayloadHash(
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request.RequestJson)))),
            request.CreatedAtUtc)
        {
            AuthorizationPolicy = request.AuthorizationPolicy
        };
        return true;
    }
}

public enum WorkflowExternalRequestBoundarySaveOutcome
{
    Created,
    Updated,
    RequestNotFound,
    VersionConflict,
    NativeCheckpointLinkUnavailable
}

public sealed record WorkflowExternalRequestBoundarySaveResult(
    WorkflowExternalRequestBoundarySaveOutcome Outcome,
    WorkflowExternalRequestBoundaryRecord? Boundary)
{
    public bool Succeeded => Outcome is WorkflowExternalRequestBoundarySaveOutcome.Created or
        WorkflowExternalRequestBoundarySaveOutcome.Updated;
}

public enum WorkflowExternalRequestBoundaryReadOutcome
{
    Found,
    RequestNotFound,
    LegacyNonResumable,
    NativeCheckpointLinkUnavailable
}

public sealed record WorkflowExternalRequestBoundaryReadResult(
    WorkflowExternalRequestBoundaryReadOutcome Outcome,
    WorkflowExternalRequestBoundaryRecord? Boundary)
{
    public bool Succeeded => Outcome == WorkflowExternalRequestBoundaryReadOutcome.Found;
}

public interface IWorkflowExternalRequestBoundaryStore
{
    Task<WorkflowExternalRequestBoundarySaveResult> UpsertAsync(
        WorkflowExternalRequestBoundaryRecord boundary,
        CancellationToken cancellationToken = default);

    Task<WorkflowExternalRequestBoundaryReadResult> ReadAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowResumeBoundaryLoadRequest(
    WorkflowExternalResponseOperationId OperationId);

public sealed record WorkflowResumableExternalRequestContext(
    WorkflowExternalResponseOperationRecord Operation,
    WorkflowRunSnapshot Run,
    WorkflowExternalRequestRecord Request,
    WorkflowExternalRequestBoundaryRecord Boundary);

public enum WorkflowResumeBoundaryLoadOutcome
{
    Found,
    OperationNotFound,
    RunNotFound,
    RequestNotFound,
    LegacyNonResumable,
    RequestNotPending,
    RunNotWaiting,
    LinkageMismatch,
    CheckpointMissing,
    CheckpointCorrupt,
    CheckpointIncompatible,
    TopologyMismatch,
    WorkflowVersionMismatch
}

public sealed record WorkflowResumeBoundaryLoadResult(
    WorkflowResumeBoundaryLoadOutcome Outcome,
    WorkflowResumableExternalRequestContext? Context)
{
    public bool Succeeded => Outcome == WorkflowResumeBoundaryLoadOutcome.Found;
}

public sealed record WorkflowResumeBoundaryCommitRequest(
    WorkflowExternalResponseOperationId OperationId,
    WorkflowExternalResponseOperationConcurrencyVersion ExpectedOperationVersion,
    WorkflowExternalResponseLeaseOwnerId LeaseOwnerId,
    WorkflowExternalResponseLeaseEpoch LeaseEpoch,
    WorkflowExternalRequestVersion ExpectedRequestVersion,
    WorkflowBackendStartResult BackendResult,
    WorkflowExternalResponseOperationFinalResult FinalResult,
    DateTimeOffset CommittedAtUtc);

public enum WorkflowResumeBoundaryCommitOutcome
{
    Committed,
    OperationNotFound,
    RunNotFound,
    RequestNotFound,
    ConcurrencyConflict,
    LeaseConflict,
    RequestVersionConflict,
    CancellationWon,
    InvalidResultBoundary
}

public sealed record WorkflowResumeBoundaryCommitResult(
    WorkflowResumeBoundaryCommitOutcome Outcome,
    WorkflowExternalResponseOperationRecord? Operation,
    WorkflowRunSnapshot? Run,
    WorkflowExternalRequestRecord? NextRequest)
{
    public bool Succeeded => Outcome == WorkflowResumeBoundaryCommitOutcome.Committed;
}

public sealed record WorkflowResumeBoundaryCancellationRequest(
    WorkflowRunId RunId,
    WorkflowExternalRequestId RequestId,
    WorkflowExternalRequestVersion ExpectedRequestVersion,
    DateTimeOffset CancelledAtUtc,
    string SafeReason);

public enum WorkflowResumeBoundaryCancellationOutcome
{
    Cancelled,
    RequestNotFound,
    RunNotFound,
    RequestVersionConflict,
    ActiveResume,
    AlreadyTerminal
}

public sealed record WorkflowResumeBoundaryCancellationResult(
    WorkflowResumeBoundaryCancellationOutcome Outcome,
    WorkflowRunSnapshot? Run,
    WorkflowExternalRequestRecord? Request,
    WorkflowExternalResponseOperationRecord? Operation)
{
    public bool Succeeded => Outcome == WorkflowResumeBoundaryCancellationOutcome.Cancelled;
}

public interface IWorkflowResumeBoundaryStore
{
    Task<WorkflowResumeBoundaryLoadResult> LoadAsync(
        WorkflowResumeBoundaryLoadRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowResumeBoundaryCommitResult> TryCommitAsync(
        WorkflowResumeBoundaryCommitRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowResumeBoundaryCancellationResult> TryCancelAsync(
        WorkflowResumeBoundaryCancellationRequest request,
        CancellationToken cancellationToken = default);
}
