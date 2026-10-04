using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed class WorkflowPreviewOwner(IWorkflowTestRunner runner, IWorkflowStructureAuthorityFactory authority,
    IProjectStructureRuntimeGateway projects, IWorkflowExecutorCatalog executors, ILogger logger) {
    private bool pending;
    private bool unknown;
    public WorkflowRunSnapshot? Accepted { get; private set; }
    public WorkflowRunId? ReservedRunId { get; private set; }
    public WorkflowPreviewOperations Operations => new(
        definition => WorkflowPreviewPreparation.Analyze(definition, executors.ListExecutors()), LoadProjectsAsync, RunAsync);

    private async Task<IReadOnlyList<WorkflowPreviewProject>> LoadProjectsAsync(CancellationToken cancellationToken)
        => (await projects.ListProjectsAsync(cancellationToken)).Select(project => new WorkflowPreviewProject(project.Id, project.Name)).ToArray();

    private async Task<WorkflowPreviewOutcome> RunAsync(WorkflowPreviewSubmission submission,
        Func<WorkflowNodeId, Task> progress, CancellationToken owner) {
        if (owner.IsCancellationRequested) {
            return new WorkflowPreviewOutcome.Rejected("This editor is no longer active.");
        }
        if (pending || unknown) {
            return new WorkflowPreviewOutcome.Unknown(ReservedRunId);
        }
        pending = true;
        var dispatched = false;
        try {
            var captured = await authority.CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface, owner);
            if (owner.IsCancellationRequested) {
                return new WorkflowPreviewOutcome.Rejected("The original editor context changed before preview admission.");
            }
            var request = new WorkflowTestRunRequest(null, null, submission.Definition with {
                Status = WorkflowLifecycleStatus.Draft,
                VersionId = WorkflowVersionId.New()
            }, submission.InputJson, WorkflowRuntimeBackendKind.InProcess, false) {
                PreviewSimulationPlan = submission.SimulationPlan,
                StructureAuthority = captured
            };
            using var progressScope = WorkflowNodeExecutionProgressScope.Push(new WorkflowPreviewProgressObserver(progress));
            dispatched = true;
            var result = await runner.RunAsync(request, CancellationToken.None);
            Accepted = result.Run;
            ReservedRunId = result.Run?.RunId;
            return new WorkflowPreviewOutcome.Completed(result.Validation, result.Run, result.Succeeded,
                result.DetailsComplete, result.Run is { } run
                    ? WorkflowEventPresentationPolicy.RunSummary(run.State, run.Summary)
                    : "Workflow preview completed.");
        } catch (WorkflowLaunchAdmissionObservationException error) {
            unknown = true;
            ReservedRunId = error.ReservedRunId;
            logger.LogWarning("Workflow preview admission is unconfirmed for {RunId}: {FailureType}", ReservedRunId, error.GetType().Name);
            return new WorkflowPreviewOutcome.Unknown(ReservedRunId);
        } catch (Exception error) {
            unknown = dispatched;
            logger.LogWarning("Workflow preview failed at {Phase}: {FailureType}",
                dispatched ? "admission acknowledgement" : "authority capture", error.GetType().Name);
            return unknown ? new WorkflowPreviewOutcome.Unknown(ReservedRunId)
                : new WorkflowPreviewOutcome.Rejected("Preview authority could not be captured. No run was submitted.");
        } finally {
            pending = false;
        }
    }
}

internal sealed class WorkflowPreviewProgressObserver(Func<WorkflowNodeId, Task> notify) : IWorkflowNodeExecutionProgressObserver {
    public ValueTask RecordAsync(WorkflowNodeExecutionProgress progress, CancellationToken cancellationToken = default)
        => progress.State == WorkflowNodeExecutionProgressState.Started
            ? new(notify(progress.NodeId)) : ValueTask.CompletedTask;
}
