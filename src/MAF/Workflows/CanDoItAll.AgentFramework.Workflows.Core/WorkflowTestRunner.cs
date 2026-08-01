using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowTestRunner(
    IWorkflowCatalogService catalog,
    IWorkflowLaunchService launchService,
    IWorkflowRuntimeManager runtimeManager,
    IWorkflowRunStore runStore) : IWorkflowTestRunner
{
    private const string PreviewActorSubjectId = "workflow-test-runner";

    public async Task<WorkflowTestRunResult> RunAsync(
        WorkflowTestRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ValidateOnly)
        {
            return await ValidateOnlyAsync(request, cancellationToken);
        }

        var selection = ResolveSelection(request);
        if (selection is null)
        {
            return MissingDefinitionResult();
        }

        try
        {
            var launchResult = await launchService.LaunchAsync(
                new WorkflowLaunchIntent(
                    selection,
                    WorkflowLaunchMode.Preview,
                    new WorkflowLaunchOrigin.Preview(
                        new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, PreviewActorSubjectId),
                        new WorkflowLaunchCorrelationId(Guid.NewGuid())),
                    request.InputJson,
                    WorkflowLaunchCompletionPolicy.WaitForStopped,
                    new WorkflowLaunchIdempotency.NotRequested())
                {
                    RequestedBackend = request.RequestedBackend,
                    PreviewSimulationPlan = request.PreviewSimulationPlan
                },
                cancellationToken);
            var run = launchResult.Run;
            var events = await runtimeManager.ListEventsAsync(run.RunId, cancellationToken);
            var artifacts = await runStore.ListArtifactsAsync(run.RunId, cancellationToken);
            var pendingExternalRequests = await runStore.ListPendingExternalRequestsAsync(run.RunId, cancellationToken);
            var checkpoints = await runStore.ListCheckpointsAsync(run.RunId, cancellationToken);

            return new WorkflowTestRunResult(
                run.State is WorkflowRunState.Completed or WorkflowRunState.WaitingForInput or WorkflowRunState.Idle,
                WorkflowValidationResult.Success,
                run,
                events,
                artifacts,
                pendingExternalRequests,
                run.State == WorkflowRunState.Failed ? run.Summary : string.Empty)
            {
                Checkpoints = checkpoints
            };
        }
        catch (WorkflowLaunchValidationException exception)
        {
            return new WorkflowTestRunResult(
                Succeeded: false,
                exception.Validation,
                Run: null,
                Events: [],
                Artifacts: [],
                PendingExternalRequests: [],
                ErrorMessage: exception.Message);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return FailureResult(exception.Message);
        }
    }

    private async Task<WorkflowTestRunResult> ValidateOnlyAsync(
        WorkflowTestRunRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DraftDefinition is not null)
        {
            var draftValidation = await catalog.ValidateDefinitionAsync(request.DraftDefinition, cancellationToken);
            return ValidationResult(draftValidation);
        }

        if (request.WorkflowId is not { } workflowId || request.VersionId is not { } versionId)
        {
            return MissingDefinitionResult();
        }

        var detail = await catalog.GetDefinitionAsync(workflowId, versionId, cancellationToken);
        return detail is null
            ? MissingDefinitionResult()
            : ValidationResult(detail.Validation);
    }

    private static WorkflowDefinitionSelection? ResolveSelection(WorkflowTestRunRequest request)
        => request switch
        {
            { DraftDefinition: not null } => new WorkflowDefinitionSelection.DraftPreview(request.DraftDefinition),
            { WorkflowId: { } workflowId, VersionId: { } versionId } =>
                new WorkflowDefinitionSelection.ExactSavedVersion(workflowId, versionId),
            _ => null
        };

    private static WorkflowTestRunResult ValidationResult(WorkflowValidationResult validation)
        => new(
            validation.Succeeded,
            validation,
            Run: null,
            Events: [],
            Artifacts: [],
            PendingExternalRequests: [],
            ErrorMessage: validation.Succeeded ? string.Empty : "Workflow definition failed validation.");

    private static WorkflowTestRunResult MissingDefinitionResult()
        => new(
            Succeeded: false,
            new WorkflowValidationResult(
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.MissingName,
                    "An exact saved workflow id and version or a draft workflow definition is required.")
            ]),
            Run: null,
            Events: [],
            Artifacts: [],
            PendingExternalRequests: [],
            ErrorMessage: "Workflow definition was not found.");

    private static WorkflowTestRunResult FailureResult(string message)
        => new(
            Succeeded: false,
            new WorkflowValidationResult(
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidWorkflowSettings,
                    message)
            ]),
            Run: null,
            Events: [],
            Artifacts: [],
            PendingExternalRequests: [],
            ErrorMessage: message);
}
