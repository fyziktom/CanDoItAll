using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowExecutorInvocationRecordEntity
{
    public Guid Id { get; set; }

    public string ScopeKey { get; set; } = string.Empty;

    public string InvocationKey { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid RunId { get; set; }

    public Guid WorkflowVersionId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string ExecutorId { get; set; } = string.Empty;

    public string ExecutorContractVersion { get; set; } = string.Empty;

    public Guid CausationRequestId { get; set; }

    public long CausationRequestVersion { get; set; }

    public Guid CausationOperationId { get; set; }

    public long LogicalGeneration { get; set; }

    public string InputHash { get; set; } = string.Empty;

    public WorkflowExecutorInvocationState State { get; set; }

    public int Attempt { get; set; }

    public long ConcurrencyVersion { get; set; }

    public string? LeaseOwnerId { get; set; }

    public long LeaseEpoch { get; set; }

    public DateTimeOffset? LeaseAcquiredAtUtc { get; set; }

    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

    public string ProtectedStoredResult { get; set; } = string.Empty;

    public string StoredResultHash { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string SafeMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowExecutorInvocationRecordEntity CreateClaimed(
        WorkflowExecutorInvocationClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new WorkflowExecutorInvocationRecordEntity
        {
            Id = Guid.NewGuid(),
            ScopeKey = request.Identity.ScopeKey.Value,
            InvocationKey = request.Identity.Key.Value,
            IdempotencyKey = request.Identity.IdempotencyKey.Value,
            RunId = request.Identity.RunId.Value,
            WorkflowVersionId = request.Identity.WorkflowVersionId.Value,
            NodeId = request.Identity.NodeId.Value,
            ExecutorId = request.Identity.ExecutorId.Value,
            ExecutorContractVersion = request.Identity.ExecutorContractVersion.Value,
            CausationRequestId = request.Identity.CausationRequestId.Value,
            CausationRequestVersion = request.Identity.CausationRequestVersion.Value,
            CausationOperationId = request.Identity.CausationOperationId.Value,
            LogicalGeneration = request.Identity.LogicalGeneration.Value,
            InputHash = request.Identity.InputHash.Value,
            State = WorkflowExecutorInvocationState.Claimed,
            Attempt = 1,
            ConcurrencyVersion = WorkflowExecutorInvocationConcurrencyVersion.Initial.Value,
            LeaseOwnerId = request.LeaseOwnerId.Value,
            LeaseEpoch = 1,
            LeaseAcquiredAtUtc = request.ClaimedAtUtc,
            LeaseExpiresAtUtc = request.LeaseExpiresAtUtc,
            CreatedAtUtc = request.ClaimedAtUtc,
            UpdatedAtUtc = request.ClaimedAtUtc
        };
    }
}
