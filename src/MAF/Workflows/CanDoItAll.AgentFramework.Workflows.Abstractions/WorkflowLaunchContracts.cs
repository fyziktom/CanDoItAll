using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowLaunchService
{
    Task<WorkflowLaunchResult> LaunchAsync(
        WorkflowLaunchIntent intent,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowRunLauncher
{
    Task<WorkflowRunSnapshot> StartAsync(
        WorkflowResolvedRuntimeRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowLaunchIdempotencyStore
{
    Task<WorkflowLaunchIdempotencyClaimResult> TryClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchRequestFingerprint fingerprint,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowRunId proposedRunId,
        DateTimeOffset claimedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> TryRenewClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        WorkflowLaunchIdempotencyCompletion completion,
        CancellationToken cancellationToken = default);

    Task<bool> TryReleaseClaimAsync(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowLaunchIdempotencyClaimToken claimToken,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowLaunchIdempotencyConflictException(
    WorkflowLaunchIdempotencyScope scope) : InvalidOperationException(
        $"Workflow launch idempotency key '{scope.CallerKey}' was reused for a different request in the same launch scope.")
{
    public WorkflowLaunchIdempotencyScope Scope { get; } = scope;
}

public sealed class WorkflowLaunchIdempotencyClaimLostException(
    WorkflowLaunchIdempotencyScope scope) : InvalidOperationException(
        $"Workflow launch idempotency claim for key '{scope.CallerKey}' was lost before the launch result could be recorded.")
{
    public WorkflowLaunchIdempotencyScope Scope { get; } = scope;
}

public sealed class WorkflowLaunchIdempotencyReleaseException(
    WorkflowLaunchIdempotencyScope scope,
    Exception launchException,
    Exception? releaseException = null) : InvalidOperationException(
        $"Workflow launch failed and its idempotency claim for key '{scope.CallerKey}' could not be released safely.",
        releaseException ?? launchException)
{
    public WorkflowLaunchIdempotencyScope Scope { get; } = scope;

    public Exception LaunchException { get; } = launchException;
}

public sealed class WorkflowRunAlreadyExistsException(
    WorkflowRunId runId) : InvalidOperationException(
        $"Workflow run '{runId}' already exists.")
{
    public WorkflowRunId RunId { get; } = runId;
}

public sealed class WorkflowLaunchValidationException : InvalidOperationException
{
    public WorkflowLaunchValidationException(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        WorkflowValidationResult validation,
        string message)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(validation);
        WorkflowId = workflowId;
        VersionId = versionId;
        Validation = validation;
    }

    public WorkflowId WorkflowId { get; }

    public WorkflowVersionId VersionId { get; }

    public WorkflowValidationResult Validation { get; }
}
