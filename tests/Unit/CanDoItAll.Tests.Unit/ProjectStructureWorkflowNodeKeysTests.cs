using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureWorkflowNodeKeysTests
{
    [Fact]
    public void Workflow_node_keys_round_trip_typed_ids()
    {
        var workflowId = WorkflowId.New();
        var runId = WorkflowRunId.New();

        var definitionKey = ProjectStructureWorkflowNodeKeys.BuildWorkflowDefinitionNodeKey(workflowId);
        var runKey = ProjectStructureWorkflowNodeKeys.BuildWorkflowRunNodeKey(runId);

        Assert.True(ProjectStructureWorkflowNodeKeys.TryParseWorkflowDefinitionNodeKey(definitionKey, out var parsedWorkflowId));
        Assert.True(ProjectStructureWorkflowNodeKeys.TryParseWorkflowRunNodeKey(runKey, out var parsedRunId));
        Assert.Equal(workflowId, parsedWorkflowId);
        Assert.Equal(runId, parsedRunId);
        Assert.False(ProjectStructureWorkflowNodeKeys.TryParseWorkflowDefinitionNodeKey("workflow-definition:not-a-guid", out _));
        Assert.False(ProjectStructureWorkflowNodeKeys.TryParseWorkflowRunNodeKey("process-run:11111111-1111-1111-1111-111111111111", out _));
    }
}
