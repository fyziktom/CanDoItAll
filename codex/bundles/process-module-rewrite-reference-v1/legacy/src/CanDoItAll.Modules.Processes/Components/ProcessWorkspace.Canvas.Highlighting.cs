namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    private void HighlightDefinitionRoleClones(string? nodeId)
    {
        var sourceNodeId = string.IsNullOrWhiteSpace(nodeId)
            ? selectedCanvasNodeId
            : nodeId;
        var role = ResolveDefinitionRole(sourceNodeId);
        if (role is null)
        {
            SetError("Select a role node before highlighting role clones.");
            return;
        }

        if (role.Id is not Guid roleId)
        {
            SetError("Save the role before highlighting canvas clones for it.");
            return;
        }

        var nodeIds = ResolveDefinitionRoleVisualNodeIds(roleId);
        if (nodeIds.Count == 0)
        {
            SetError($"No role canvas nodes were found for '{ResolveRoleLabel(role)}'.");
            return;
        }

        HighlightDefinitionCanvasNodes(nodeIds);
        SetMessage($"Highlighted {nodeIds.Count} canvas node(s) for role '{ResolveRoleLabel(role)}'.");
    }

    private void HighlightDefinitionCanvasNodes(IReadOnlyList<string> nodeIds)
    {
        var highlightedNodeIds = nodeIds
            .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var uiState = CloneCanvasUiState(ResolveStoredCanvasUiState());
        uiState.SelectedNodeIds = [];
        uiState.HighlightedNodeIds = highlightedNodeIds;
        StoreCanvasUiState(uiState);

        selectedCanvasNodeId = NoCanvasSelection;
        RefreshCanvasSurface();
    }

    private IReadOnlyList<string> ResolveDefinitionRoleVisualNodeIds(Guid roleId)
    {
        var roleToken = roleId.ToString("D");
        var nodeIds = canvasSurface?.Nodes
            .Where(node =>
                ProcessCanvasBranching.TryResolveDefinitionRoleToken(node.Id, out var token) &&
                string.Equals(token, roleToken, StringComparison.Ordinal))
            .Select(node => node.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        if (nodeIds.Count > 0)
        {
            return nodeIds;
        }

        var role = editor.Roles.FirstOrDefault(candidate => candidate.Id == roleId);
        if (role is null)
        {
            return [];
        }

        var fallbackNodeIds = new List<string>
        {
            ProcessCanvasBranching.BuildDefinitionRoleNodeId(role)
        };
        fallbackNodeIds.AddRange(editor.Steps
            .Where(step =>
                step.Id.HasValue &&
                (step.DecisionRoleRequirementId == roleId ||
                    step.RoleAssignments.Any(assignment => assignment.RoleRequirementId == roleId)))
            .Select(step => ProcessCanvasBranching.BuildDefinitionRoleInstanceNodeId(role, step)));

        return fallbackNodeIds
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
