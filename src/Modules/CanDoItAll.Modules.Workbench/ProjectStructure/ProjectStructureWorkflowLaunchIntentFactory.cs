using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowLaunchIntentFactory
{
    public WorkflowLaunchIntent Create(
        WorkflowDefinition definition,
        Guid projectId,
        string nodeId,
        ProjectStructureAgentContext agent,
        string inputJson,
        WorkflowRuntimeBackendKind? requestedBackend,
        WorkflowPreviewSimulationPlan simulationPlan,
        WorkflowRunId? previousRunId)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(simulationPlan);
        var idempotencyKey = BuildIdempotencyKey(projectId, nodeId, agent, previousRunId);
        return new WorkflowLaunchIntent(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            simulationPlan.HasSteps ? WorkflowLaunchMode.Preview : WorkflowLaunchMode.Production,
            new WorkflowLaunchOrigin.ProjectStructureNode(
                projectId,
                new WorkflowProjectStructureNodeId(nodeId),
                new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, agent.AgentId),
                new WorkflowLaunchSessionId(agent.SessionId),
                new WorkflowLaunchCorrelationId(idempotencyKey.Value)),
            inputJson,
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(idempotencyKey))
        {
            RequestedBackend = requestedBackend,
            PreviewSimulationPlan = simulationPlan
        };
    }

    private static WorkflowLaunchIdempotencyKey BuildIdempotencyKey(
        Guid projectId,
        string nodeId,
        ProjectStructureAgentContext agent,
        WorkflowRunId? previousRunId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id cannot be empty.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var material = string.Join(
            '\n',
            projectId.ToString("N"),
            nodeId.Trim(),
            agent.AgentId.Trim(),
            agent.SessionId.Trim(),
            previousRunId?.Value.ToString("N") ?? "initial");
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return new WorkflowLaunchIdempotencyKey($"project-structure:{projectId:N}:{digest}");
    }
}
