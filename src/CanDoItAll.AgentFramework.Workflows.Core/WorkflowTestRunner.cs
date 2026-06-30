using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowTestRunner(
    IWorkflowCatalogService catalog,
    IWorkflowRuntimeManager runtimeManager,
    IWorkflowRunStore runStore) : IWorkflowTestRunner
{
    public async Task<WorkflowTestRunResult> RunAsync(
        WorkflowTestRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var definition = await ResolveDefinitionAsync(request, cancellationToken);
        if (definition is null)
        {
            return new WorkflowTestRunResult(
                Succeeded: false,
                new WorkflowValidationResult(
                [
                    new WorkflowValidationIssue(
                        WorkflowValidationIssueCode.MissingName,
                        "A saved workflow id or draft workflow definition is required.")
                ]),
                Run: null,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: "Workflow definition was not found.");
        }

        var validation = await catalog.ValidateDefinitionAsync(definition, cancellationToken);
        if (!validation.Succeeded || request.ValidateOnly)
        {
            return new WorkflowTestRunResult(
                validation.Succeeded,
                validation,
                Run: null,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: validation.Succeeded ? string.Empty : "Workflow definition failed validation.");
        }

        try
        {
            var run = await runtimeManager.StartAsync(
                definition,
                new WorkflowRunStartRequest(
                    definition.Id,
                    definition.VersionId,
                    string.IsNullOrWhiteSpace(request.InputJson) ? "{}" : request.InputJson,
                    request.RequestedBackend,
                    SourceProcessRunId: null,
                    SourceProcessAssignmentId: null)
                {
                    PreviewSimulationPlan = request.PreviewSimulationPlan
                },
                cancellationToken);
            var events = await runtimeManager.ListEventsAsync(run.RunId, cancellationToken);
            var artifacts = await runStore.ListArtifactsAsync(run.RunId, cancellationToken);
            var pendingExternalRequests = await runStore.ListPendingExternalRequestsAsync(run.RunId, cancellationToken);
            var checkpoints = await runStore.ListCheckpointsAsync(run.RunId, cancellationToken);

            return new WorkflowTestRunResult(
                run.State is WorkflowRunState.Completed or WorkflowRunState.WaitingForInput or WorkflowRunState.Idle,
                validation,
                run,
                events,
                artifacts,
                pendingExternalRequests,
                run.State == WorkflowRunState.Failed ? run.Summary : string.Empty)
            {
                Checkpoints = checkpoints
            };
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            return new WorkflowTestRunResult(
                Succeeded: false,
                validation,
                Run: null,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: exception.Message);
        }
    }

    private async Task<WorkflowDefinition?> ResolveDefinitionAsync(
        WorkflowTestRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DraftDefinition is not null)
        {
            return request.DraftDefinition;
        }

        if (request.WorkflowId is not { } workflowId)
        {
            return null;
        }

        return (await catalog.GetDefinitionAsync(workflowId, request.VersionId, cancellationToken))?.Definition;
    }
}
