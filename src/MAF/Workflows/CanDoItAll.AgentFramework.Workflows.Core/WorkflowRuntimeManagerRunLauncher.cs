using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowRuntimeManagerRunLauncher(IWorkflowRuntimeManager runtimeManager) : IWorkflowRunLauncher
{
    public Task<WorkflowRunSnapshot> StartAsync(
        WorkflowResolvedRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startRequest = new WorkflowRunStartRequest(
            request.Definition.Id,
            request.Definition.VersionId,
            request.InputJson,
            request.Backend.Kind,
            SourceProcessRunId: null,
            SourceProcessAssignmentId: null)
        {
            PreviewSimulationPlan = request.PreviewSimulationPlan,
            Origin = request.Origin,
            Idempotency = request.Idempotency,
            RequestedRunId = request.RequestedRunId
        };

        return runtimeManager.StartAsync(request.Definition, startRequest, cancellationToken);
    }
}
