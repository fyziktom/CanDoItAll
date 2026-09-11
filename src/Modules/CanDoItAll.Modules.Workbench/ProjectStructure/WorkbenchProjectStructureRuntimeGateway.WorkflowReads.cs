using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class WorkbenchProjectStructureRuntimeGateway {
    public async Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListWorkflowProjectsAsync(WorkflowStructureReadContext context,
        CancellationToken cancellationToken = default) {
        var run = await RequireWorkflowReadAsync(context, cancellationToken);
        IReadOnlyList<ProjectWriteAdmission> targets;
        ProjectStructureRuntimeProjectSummary[] result;
        await using (var source = await workflowAuthority.AcquireReadAsync(run, cancellationToken)) {
            targets = await source.ListTargetsAsync(cancellationToken);
            await source.RequireCurrentAsync(targets, cancellationToken);
            var allowedIds = targets.Select(target => target.ProjectId).ToHashSet();
            result = (await ListProjectsAsync(cancellationToken)).Where(project => allowedIds.Contains(project.Id)).ToArray();
        }
        run = await RequireWorkflowReadAsync(context, cancellationToken);
        await using var disclosure = await workflowAuthority.AcquireReadAsync(run, cancellationToken);
        await disclosure.RequireCurrentAsync(targets, cancellationToken);
        WorkflowStructureReadEvidence.Capture(context, WorkflowProjectStructureOperation.ListProjects, targets);
        return result;
    }

    public async Task<ProjectStructureRuntimeReadResponse> ReadWorkflowStructureAsync(Guid projectId,
        ProjectStructureRuntimeReadRequest request, WorkflowStructureReadContext context, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var run = await RequireWorkflowReadAsync(context, cancellationToken);
        ProjectWriteAdmission target;
        ProjectStructureRuntimeReadResponse result;
        await using (var source = await workflowAuthority.AcquireReadAsync(run, cancellationToken)) {
            target = await source.RequireTargetAsync(projectId, cancellationToken);
            await source.RequireCurrentAsync([target], cancellationToken);
            result = await ReadStructureAsync(projectId, request, cancellationToken);
        }
        run = await RequireWorkflowReadAsync(context, cancellationToken);
        await using var disclosure = await workflowAuthority.AcquireReadAsync(run, cancellationToken);
        await disclosure.RequireCurrentAsync([target], cancellationToken);
        WorkflowStructureReadEvidence.Capture(context, request.NodeIds is { Count: > 0 }
            ? WorkflowProjectStructureOperation.ReadNode : WorkflowProjectStructureOperation.ReadTree, [target]);
        return result;
    }

    private async Task<WorkflowRunSnapshot> RequireWorkflowReadAsync(WorkflowStructureReadContext context, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CaptureReadEvidence is not null) {
            WorkflowStructureReadEvidence.RequireInvocation(context);
        }
        if (context.Occurrence is null || string.IsNullOrWhiteSpace(context.StepId.Value) ||
                WorkflowExecutorExecutionAuditScope.CurrentRunId != context.Occurrence.RunId ||
                WorkflowExecutorExecutionAuditScope.CurrentOrigin is not { } activeOrigin) {
            throw new InvalidOperationException("Workflow Structure reads require the actual admitted executor occurrence and source.");
        }
        var run = await workflowRuns.GetRunAsync(context.Occurrence.RunId, cancellationToken)
            ?? throw new InvalidOperationException("The admitted Workflow read has no persisted run.");
        if (run.VersionId != context.VersionId || run.State is not (WorkflowRunState.Running or WorkflowRunState.WaitingForInput or WorkflowRunState.Idle) ||
                run.Origin is null || JsonSerializer.Serialize(run.Origin, JsonOptions) != JsonSerializer.Serialize(activeOrigin, JsonOptions)) {
            throw new InvalidOperationException("The Workflow read no longer matches its original live run, version and source.");
        }
        return run;
    }
}
