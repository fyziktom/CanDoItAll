using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureWorkflowNodeKeys
{
    public const string WorkflowDefinitionPrefix = "workflow-definition:";
    public const string WorkflowRunPrefix = "workflow-run:";

    public static string BuildWorkflowDefinitionNodeKey(WorkflowId workflowId)
        => BuildWorkflowDefinitionNodeKey(workflowId.Value);

    public static string BuildWorkflowDefinitionNodeKey(Guid workflowId)
        => $"{WorkflowDefinitionPrefix}{workflowId:D}";

    public static string BuildWorkflowRunNodeKey(WorkflowRunId runId)
        => BuildWorkflowRunNodeKey(runId.Value);

    public static string BuildWorkflowRunNodeKey(Guid runId)
        => $"{WorkflowRunPrefix}{runId:D}";

    public static bool TryParseWorkflowDefinitionNodeKey(string? nodeKey, out WorkflowId workflowId)
    {
        if (TryParsePrefixedGuidNodeKey(nodeKey, WorkflowDefinitionPrefix, out var value))
        {
            workflowId = new WorkflowId(value);
            return true;
        }

        workflowId = default;
        return false;
    }

    public static bool TryParseWorkflowRunNodeKey(string? nodeKey, out WorkflowRunId runId)
    {
        if (TryParsePrefixedGuidNodeKey(nodeKey, WorkflowRunPrefix, out var value))
        {
            runId = new WorkflowRunId(value);
            return true;
        }

        runId = default;
        return false;
    }

    private static bool TryParsePrefixedGuidNodeKey(string? nodeKey, string prefix, out Guid value)
    {
        value = Guid.Empty;
        return !string.IsNullOrWhiteSpace(nodeKey) &&
               nodeKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(nodeKey[prefix.Length..], out value) &&
               value != Guid.Empty;
    }
}
