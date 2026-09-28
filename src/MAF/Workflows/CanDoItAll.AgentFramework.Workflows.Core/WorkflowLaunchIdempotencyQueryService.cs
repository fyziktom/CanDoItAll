using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowLaunchIdempotencyQueryService(
    IWorkflowLaunchIdempotencyQueryStore queryStore,
    IWorkflowRunStore runStore,
    IWorkflowLaunchAuthorizationScopeResolver authorizationScopeResolver) : IWorkflowLaunchIdempotencyQueryService
{
    public async Task<WorkflowLaunchIdempotencyEvidence?> FindApiKeyAsync(
        WorkflowLaunchIdempotencyKey callerKey,
        WorkflowLaunchOrigin.Api caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var record = await queryStore.FindApiKeyAsync(callerKey, cancellationToken);
        // A key belongs to the caller that recorded it; a key recorded by another caller is not disclosed and reads as
        // not found.
        if (record is null || record.Scope.OriginScopeKey != CreateCallerScopeKey(caller))
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

    // The same caller scope a start records: the caller's actor under the authorization scope a launch resolves now.
    private WorkflowLaunchOriginScopeKey CreateCallerScopeKey(WorkflowLaunchOrigin.Api caller)
    {
        var authorization = authorizationScopeResolver.Resolve(caller);
        return WorkflowLaunchIdempotencyRequestFactory.CreateOriginScopeKey(caller with
        {
            AuthorizationScope = authorization.Scope,
            AuthorizationPolicyFingerprint = authorization.PolicyFingerprint
        });
    }
}
