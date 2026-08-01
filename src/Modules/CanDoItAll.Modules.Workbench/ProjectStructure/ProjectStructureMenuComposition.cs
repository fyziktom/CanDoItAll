using CanDoItAll.Components.CanvasLib;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureMenuComposition
{
    private static readonly string[] FixedFirstRingActionIds =
    [
        "group-blocks",
        "group-assets",
        "group-work",
        "progress",
        "marker"
    ];

    public static IReadOnlyList<CanvasWorkbenchAction> OrderNodeContextActions(
        ProjectStructureNode node,
        IReadOnlyList<CanvasWorkbenchAction> actions)
    {
        if (actions.Count <= 1)
        {
            return actions;
        }

        var remaining = actions.ToList();
        var ordered = new List<CanvasWorkbenchAction>(actions.Count);

        foreach (var actionId in FixedFirstRingActionIds)
        {
            MoveIfPresent(remaining, ordered, actionId);
        }

        MoveIfPresent(remaining, ordered, ResolvePrimaryRingActionId(node, remaining));

        ordered.AddRange(remaining);
        return ordered;
    }

    private static string ResolvePrimaryRingActionId(
        ProjectStructureNode node,
        IReadOnlyList<CanvasWorkbenchAction> actions)
    {
        var preferredActions = new List<string>();

        if (node.ProjectRole != ProjectStructureProjectRole.None)
        {
            preferredActions.AddRange(
            [
                "project:open-structure",
                "project:add-subproject",
                "project:reconnect-subproject",
                ProjectStructureFileActions.BrowseFilesId,
                "open",
                "summary"
            ]);
        }

        switch (node.ObjectType)
        {
            case ProjectObjectType.Note:
                preferredActions.Add("note:convert-to-block");
                break;
            case ProjectObjectType.ProjectBlock:
                preferredActions.Add("block:change-type");
                break;
            case ProjectObjectType.PromptFlow:
                preferredActions.Add("wizard");
                break;
            case ProjectObjectType.Recording:
                preferredActions.Add("transcript:create");
                break;
            case ProjectObjectType.Transcript:
                preferredActions.Add("transcript-llm");
                break;
            case ProjectObjectType.ProcessDefinition:
                preferredActions.Add("start-process");
                preferredActions.Add("estimate-process");
                break;
            case ProjectObjectType.WorkflowDefinition:
                preferredActions.Add("start-workflow");
                break;
        }

        preferredActions.AddRange(
        [
            "runtime:open",
            "open-local",
            "open-new-tab",
            ProjectStructureFileActions.BrowseFilesId,
            "open",
            "summary",
            "add-process",
            "add-workflow",
            "validate",
            "test",
            "connect"
        ]);

        return preferredActions.FirstOrDefault(actionId => HasAction(actions, actionId)) ?? string.Empty;
    }

    private static bool HasAction(IEnumerable<CanvasWorkbenchAction> actions, string actionId)
        => actions.Any(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal));

    private static void MoveIfPresent(
        List<CanvasWorkbenchAction> remaining,
        List<CanvasWorkbenchAction> ordered,
        string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return;
        }

        var index = remaining.FindIndex(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        ordered.Add(remaining[index]);
        remaining.RemoveAt(index);
    }
}
