using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal static class WorkflowRequestedRunRecovery
{
    public static async Task<WorkflowRunSnapshot?> TryResolveAsync(
        IWorkflowRunStore store,
        WorkflowRunId? requestedRunId,
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind requestedBackend,
        CancellationToken cancellationToken)
    {
        if (requestedRunId is null)
        {
            return null;
        }

        var run = await store.GetRunAsync(requestedRunId.Value, cancellationToken);
        return run is null ? null : Validate(run, definition, requestedBackend);
    }

    public static async Task<WorkflowRunSnapshot> ResolveRequiredAsync(
        IWorkflowRunStore store,
        WorkflowRunId requestedRunId,
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind requestedBackend,
        CancellationToken cancellationToken)
    {
        var run = await store.GetRunAsync(requestedRunId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Workflow run '{requestedRunId}' was reported as existing but could not be loaded.");
        return Validate(run, definition, requestedBackend);
    }

    private static WorkflowRunSnapshot Validate(
        WorkflowRunSnapshot run,
        WorkflowDefinition definition,
        WorkflowRuntimeBackendKind requestedBackend)
    {
        if (run.WorkflowId != definition.Id ||
            run.VersionId != definition.VersionId ||
            run.Backend != requestedBackend)
        {
            throw new InvalidOperationException(
                $"Requested workflow run '{run.RunId}' belongs to a different workflow definition or runtime backend.");
        }

        return run;
    }
}
