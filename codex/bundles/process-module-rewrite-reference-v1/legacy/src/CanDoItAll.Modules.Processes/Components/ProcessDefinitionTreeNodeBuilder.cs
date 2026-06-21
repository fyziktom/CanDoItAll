using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Modules.Processes;

public static class ProcessDefinitionTreeNodeBuilder
{
    private const string DefinitionNodePrefix = "process:";
    private const string GlobalScopeNodeId = "process-scope:global";
    private const string ProjectScopeNodePrefix = "process-scope:project:";

    public static IReadOnlyList<TreeViewNode> Build(
        IReadOnlyList<ProcessDefinitionListItem> definitions,
        Guid? selectedDefinitionId,
        IReadOnlySet<string> expandedNodeIds)
    {
        if (definitions.Count == 0)
        {
            return [];
        }

        return definitions
            .GroupBy(ResolveScopeKey)
            .OrderBy(group => group.Key.ProjectName, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildScopeNode(group, selectedDefinitionId, expandedNodeIds))
            .ToArray();
    }

    public static string BuildDefinitionNodeId(Guid definitionId)
        => $"{DefinitionNodePrefix}{definitionId:N}";

    public static bool TryReadDefinitionId(string nodeId, out Guid definitionId)
    {
        definitionId = Guid.Empty;
        return nodeId.StartsWith(DefinitionNodePrefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParseExact(nodeId[DefinitionNodePrefix.Length..], "N", out definitionId);
    }

    private static TreeViewNode BuildScopeNode(
        IGrouping<ProcessDefinitionScopeKey, ProcessDefinitionListItem> group,
        Guid? selectedDefinitionId,
        IReadOnlySet<string> expandedNodeIds)
    {
        var scopeNodeId = group.Key.ProjectId.HasValue
            ? $"{ProjectScopeNodePrefix}{group.Key.ProjectId.Value:N}"
            : GlobalScopeNodeId;
        var groupItems = group.ToArray();
        var definitions = groupItems
            .OrderBy(definition => definition.Status)
            .ThenBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(definition => BuildDefinitionNode(definition, selectedDefinitionId))
            .ToArray();
        var containsSelectedDefinition = selectedDefinitionId.HasValue &&
            groupItems.Any(definition => definition.Id == selectedDefinitionId.Value);

        return new TreeViewNode
        {
            Id = scopeNodeId,
            Text = group.Key.ProjectName,
            Icon = group.Key.ProjectId.HasValue ? "folder_open" : "public",
            BadgeText = definitions.Length.ToString(),
            Children = definitions,
            IsExpanded = expandedNodeIds.Contains(scopeNodeId) ||
                group.Key.ProjectId is null ||
                containsSelectedDefinition,
            IsSelectable = false,
            Tooltip = $"{group.Key.ProjectName} process definitions",
            DataTestId = $"processes-tree-scope-{NormalizeTestId(group.Key.ProjectName)}",
            ChildrenDataTestId = $"processes-tree-scope-children-{NormalizeTestId(group.Key.ProjectName)}"
        };
    }

    private static TreeViewNode BuildDefinitionNode(
        ProcessDefinitionListItem definition,
        Guid? selectedDefinitionId)
    {
        return new TreeViewNode
        {
            Id = BuildDefinitionNodeId(definition.Id),
            Text = definition.Name,
            Icon = definition.HasPublishedVersion ? "task" : "edit_note",
            Tooltip = BuildDefinitionTooltip(definition),
            BadgeText = BuildDefinitionBadge(definition),
            IsSelected = selectedDefinitionId == definition.Id,
            DataTestId = $"processes-tree-definition-{definition.Id:N}"
        };
    }

    private static ProcessDefinitionScopeKey ResolveScopeKey(ProcessDefinitionListItem definition)
    {
        return definition.ProjectId.HasValue
            ? new ProcessDefinitionScopeKey(definition.ProjectId, string.IsNullOrWhiteSpace(definition.ProjectName) ? "Project scoped" : definition.ProjectName)
            : new ProcessDefinitionScopeKey(null, "Global process library");
    }

    private static string BuildDefinitionTooltip(ProcessDefinitionListItem definition)
    {
        var summary = string.IsNullOrWhiteSpace(definition.Summary)
            ? "No summary"
            : definition.Summary;

        return $"{definition.Name} - {definition.Status}, {summary}";
    }

    private static string BuildDefinitionBadge(ProcessDefinitionListItem definition)
    {
        if (definition.ActiveRunCount > 0)
        {
            return $"{definition.ActiveRunCount} run";
        }

        if (definition.UnfilledRoleCount > 0)
        {
            return $"{definition.UnfilledRoleCount} open";
        }

        return definition.Status.ToString();
    }

    private static string NormalizeTestId(string value)
    {
        return string.Join(
            "-",
            value
                .ToLowerInvariant()
                .Split([' ', '/', '\\', ':', '.', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record ProcessDefinitionScopeKey(Guid? ProjectId, string ProjectName);
}
