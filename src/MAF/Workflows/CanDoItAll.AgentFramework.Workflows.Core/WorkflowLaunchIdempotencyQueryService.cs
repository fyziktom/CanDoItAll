using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowLaunchIdempotencyQueryService(
    IWorkflowLaunchIdempotencyQueryStore queryStore,
    IWorkflowRunStore runStore) : IWorkflowLaunchIdempotencyQueryService
{
    public async Task<WorkflowLaunchIdempotencyEvidence?> FindApiKeyAsync(
        WorkflowLaunchIdempotencyKey callerKey,
        CancellationToken cancellationToken = default)
    {
        var record = await queryStore.FindApiKeyAsync(callerKey, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var currentRun = await runStore.GetRunAsync(record.OriginalRunId, cancellationToken);
        var resolvedRequest = record.Completion?.ResolvedRequest;
        var run = currentRun ?? record.Completion?.Run;
        var runState = run?.State;

        return new WorkflowLaunchIdempotencyEvidence(
            WorkflowLaunchIdempotencyRequestFactory.CreateKeyHash(callerKey),
            record.Fingerprint.Value,
            record.Fingerprint.CanonicalInputHash,
            record.Scope.WorkflowId,
            record.Scope.SelectionKind,
            record.Scope.RequestedVersionId,
            resolvedRequest?.Definition.VersionId,
            resolvedRequest?.Backend.Kind,
            record.OriginalRunId,
            record.State,
            runState,
            runState is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled,
            record.CreatedAtUtc,
            record.CompletedAtUtc,
            record.ReplayCount > 0,
            record.ReplayCount,
            record.LastReplayedAtUtc);
    }
}
