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
    ProcessRuntimeProjectionQueryService processQueryService)
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
                run.AgentId);
    }

    private async Task<AgentRecruitingTargetResolution> ResolveWorkflowRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await workflowRunStore.GetRunAsync(new WorkflowRunId(runId), cancellationToken);
        return run is null
            ? new AgentRecruitingTargetResolution(false, "not-found", false)
            : new AgentRecruitingTargetResolution(
                true,
                run.State.ToString(),
                run.State is WorkflowRunState.Completed
                    or WorkflowRunState.Failed
                    or WorkflowRunState.Cancelled);
    }

    private async Task<AgentRecruitingTargetResolution> ResolveProcessRunAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await processQueryService.GetRunDetailAsync(
            new ProcessRunDetailQuery(new ProcessRunId(runId)),
            cancellationToken);
        return run is null
            ? new AgentRecruitingTargetResolution(false, "not-found", false)
            : new AgentRecruitingTargetResolution(
                true,
                run.Status.ToString(),
                run.Status is ProcessProjectedRunStatus.Completed
                    or ProcessProjectedRunStatus.Failed
                    or ProcessProjectedRunStatus.Cancelled);
    }
}
