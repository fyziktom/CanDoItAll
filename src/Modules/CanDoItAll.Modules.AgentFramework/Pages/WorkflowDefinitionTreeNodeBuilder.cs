using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public static class WorkflowDefinitionTreeNodeBuilder
{
    private const string DefinitionNodePrefix = "workflow:";
    private const string StatusNodePrefix = "workflow-status:";

    public static IReadOnlyList<TreeViewNode> Build(
        IReadOnlyList<WorkflowCatalogItem> definitions,
        WorkflowId? selectedDefinitionId,
        IReadOnlySet<string> expandedNodeIds)
    {
        if (definitions.Count == 0)
        {
            return [];
        }

        return definitions
            .GroupBy(definition => definition.Status)
            .OrderBy(group => group.Key)
            .Select(group => BuildStatusNode(group, selectedDefinitionId, expandedNodeIds))
            .ToArray();
    }

    public static string BuildDefinitionNodeId(WorkflowId definitionId)
        => $"{DefinitionNodePrefix}{definitionId.Value:N}";

    public static bool TryReadDefinitionId(string nodeId, out WorkflowId definitionId)
    {
        definitionId = default;
        if (!nodeId.StartsWith(DefinitionNodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParseExact(nodeId[DefinitionNodePrefix.Length..], "N", out var value) || value == Guid.Empty)
        {
            return false;
        }

        definitionId = new WorkflowId(value);
        return true;
    }

    private static TreeViewNode BuildStatusNode(
        IGrouping<WorkflowLifecycleStatus, WorkflowCatalogItem> group,
        WorkflowId? selectedDefinitionId,
        IReadOnlySet<string> expandedNodeIds)
    {
        var statusNodeId = $"{StatusNodePrefix}{group.Key}";
        var groupItems = group.ToArray();
        var definitions = groupItems
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition => BuildDefinitionNode(definition, selectedDefinitionId))
            .ToArray();
        var containsSelectedDefinition = selectedDefinitionId.HasValue &&
            groupItems.Any(definition => definition.Id == selectedDefinitionId.Value);

        return new TreeViewNode
        {
            Id = statusNodeId,
            Text = $"{group.Key} workflows",
            Icon = ResolveStatusIcon(group.Key),
            BadgeText = definitions.Length.ToString(),
            Children = definitions,
            IsExpanded = expandedNodeIds.Contains(statusNodeId) ||
                group.Key == WorkflowLifecycleStatus.Active ||
                containsSelectedDefinition,
            IsSelectable = false,
            Tooltip = $"{definitions.Length} {group.Key} workflow definition(s)",
            DataTestId = $"workflows-tree-status-{group.Key}",
            ChildrenDataTestId = $"workflows-tree-status-children-{group.Key}"
        };
    }

    private static TreeViewNode BuildDefinitionNode(
        WorkflowCatalogItem definition,
        WorkflowId? selectedDefinitionId)
    {
        return new TreeViewNode
        {
            Id = BuildDefinitionNodeId(definition.Id),
            Text = definition.Name,
            Icon = "account_tree",
            Tooltip = BuildDefinitionTooltip(definition),
            BadgeText = definition.PreferredBackend.ToString(),
            IsSelected = selectedDefinitionId == definition.Id,
            DataTestId = "workflows-catalog-item"
        };
    }

    private static string BuildDefinitionTooltip(WorkflowCatalogItem definition)
    {
        var description = string.IsNullOrWhiteSpace(definition.Description)
            ? "No description"
            : definition.Description;

        return $"{definition.Name} - {definition.Status}, {definition.PreferredBackend}, {description}";
    }

    private static string ResolveStatusIcon(WorkflowLifecycleStatus status)
    {
        return status switch
        {
            WorkflowLifecycleStatus.Active => "task",
            WorkflowLifecycleStatus.Suspended => "pause_circle",
            WorkflowLifecycleStatus.Archived => "history",
            _ => "edit_note"
        };
    }
}
