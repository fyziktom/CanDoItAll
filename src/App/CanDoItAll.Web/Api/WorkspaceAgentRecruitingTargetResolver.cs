using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Web.Api;

internal sealed class WorkspaceAgentRecruitingTargetResolver(
    ISandboxWorkspaceExecutionRunStore executionRunStore,
    IWorkflowRunStore workflowRunStore,
    IWorkflowCatalogService workflowCatalogService,
    ProcessRuntimeProjectionQueryService processQueryService,
    IProcessRunRecordReader processRunRecordReader)
    : IAgentRecruitingTargetResolver
{
    public async Task<AgentRecruitingTargetResolution> ResolveAsync(
        AgentRecruitingExecutionTarget target,
        CancellationToken cancellationToken = default)
    {
        return target.Kind switch
        {
            AgentRecruitingTargetKind.AgentExecutionRun =>
                await ResolveAgentExecutionAsync(target.Id, cancellationToken),
            AgentRecruitingTargetKind.WorkflowRun =>
                await ResolveWorkflowRunAsync(target.Id, cancellationToken),
            AgentRecruitingTargetKind.ProcessRun =>
                await ResolveProcessRunAsync(target.Id, cancellationToken),
            _ => new AgentRecruitingTargetResolution(false, "unsupported", false)
        };
    }

    private async Task<AgentRecruitingTargetResolution> ResolveAgentExecutionAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await executionRunStore.GetExecutionRunAsync(runId, cancellationToken);
        return run is null
            ? new AgentRecruitingTargetResolution(false, "not-found", false)
            : new AgentRecruitingTargetResolution(
                true,
                run.State.ToString(),
                run.State is ExecutionState.Completed or ExecutionState.Failed,
                [run.AgentId]);
    }

    private async Task<AgentRecruitingTargetResolution> ResolveWorkflowRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await workflowRunStore.GetRunAsync(new WorkflowRunId(runId), cancellationToken);
        if (run is null)
        {
            return new AgentRecruitingTargetResolution(false, "not-found", false);
        }

        var definition = await workflowCatalogService.GetDefinitionAsync(
            run.WorkflowId,
            run.VersionId,
            cancellationToken);
        var events = await workflowRunStore.ListEventsAsync(run.RunId, cancellationToken);
        var executedNodeIds = events
            .Where(item =>
                item.NodeId.HasValue &&
                item.Kind is WorkflowEventKind.ExecutorInvoked
                    or WorkflowEventKind.ExecutorCompleted
                    or WorkflowEventKind.ExecutorFailed)
            .Select(item => item.NodeId!.Value)
            .ToHashSet();
        var participatingAgentIds = definition?.Definition.Graph.Nodes
            .Where(node => executedNodeIds.Contains(node.Id))
            .Select(node => node.Settings.AgentId)
            .OfType<Guid>()
            .Where(agentId => agentId != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];
        return new AgentRecruitingTargetResolution(
            true,
            run.State.ToString(),
            run.State is WorkflowRunState.Completed
                or WorkflowRunState.Failed
                or WorkflowRunState.Cancelled,
            participatingAgentIds);
    }

    private async Task<AgentRecruitingTargetResolution> ResolveProcessRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await processQueryService.GetRunDetailAsync(
            new ProcessRunDetailQuery(new ProcessRunId(runId)),
            cancellationToken);
        if (run is null)
        {
            return new AgentRecruitingTargetResolution(false, "not-found", false);
        }

        var recordPage = await processRunRecordReader.ListAsync(
            new ProcessRunRecordListQuery(Take: 1)
            {
                RunIds = [new ProcessRunId(runId)],
                Payload = ProcessRunRecordListPayload.Compact
            },
            cancellationToken);
        var participatingAgentIds = recordPage.Records.SingleOrDefault()?.ParticipantIds
            .Select(participant => Guid.TryParse(participant.Value, out var agentId)
                ? agentId
                : Guid.Empty)
            .Where(agentId => agentId != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];
        return new AgentRecruitingTargetResolution(
            true,
            run.Status.ToString(),
            run.Status is ProcessProjectedRunStatus.Completed
                or ProcessProjectedRunStatus.Failed
                or ProcessProjectedRunStatus.Cancelled,
            participatingAgentIds);
    }
}
