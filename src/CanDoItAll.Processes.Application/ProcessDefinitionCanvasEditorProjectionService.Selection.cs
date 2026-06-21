using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed partial class ProcessDefinitionCanvasEditorProjectionService
{
    private static ProcessDefinitionCanvasSelectionProjection CreateSelection(
        ProcessDefinitionCanvasEditorNodeProjection node)
        => new(
            ResolveSelectionKind(node.Kind),
            node.NodeKey,
            EdgeKey: null,
            node.Title,
            node.Summary,
            ResolveNodeKeyText(node),
            [node.Kind.ToString(), .. node.Badges]);

    private static ProcessDefinitionCanvasSelectionProjection CreateSelection(
        ProcessDefinitionCanvasEdgeProjection edge)
        => new(
            edge.Kind == ProcessDefinitionCanvasEdgeKind.BranchRoute
                ? ProcessDefinitionCanvasSelectionKind.Route
                : ProcessDefinitionCanvasSelectionKind.Step,
            NodeKey: null,
            edge.EdgeKey,
            edge.Label,
            edge.Summary,
            edge.EdgeKey.Value,
            [edge.Kind.ToString(), edge.Tone]);

    private static ProcessDefinitionCanvasSelectionKind ResolveSelectionKind(
        ProcessDefinitionCanvasNodeKind nodeKind)
        => nodeKind switch
        {
            ProcessDefinitionCanvasNodeKind.Step => ProcessDefinitionCanvasSelectionKind.Step,
            ProcessDefinitionCanvasNodeKind.BranchRouter => ProcessDefinitionCanvasSelectionKind.Route,
            ProcessDefinitionCanvasNodeKind.Role => ProcessDefinitionCanvasSelectionKind.Role,
            ProcessDefinitionCanvasNodeKind.Artifact => ProcessDefinitionCanvasSelectionKind.Artifact,
            ProcessDefinitionCanvasNodeKind.SubprocessBoundary => ProcessDefinitionCanvasSelectionKind.SubprocessBoundary,
            _ => ProcessDefinitionCanvasSelectionKind.None
        };

    private static string ResolveNodeKeyText(ProcessDefinitionCanvasEditorNodeProjection node)
        => node.Kind == ProcessDefinitionCanvasNodeKind.Artifact && !string.IsNullOrWhiteSpace(node.ArtifactKey)
            ? node.ArtifactKey
            : node.StepKey?.Value ?? node.RoleKey?.Value ?? node.NodeKey.Value;

    private static ProcessDefinitionCanvasToolboxActionProjection CreateToolboxAction(
        ProcessTemplateDefinitionCanvasToolboxActionSummary action)
        => new(
            new ProcessDefinitionCanvasToolboxActionKey(action.ActionId),
            action.Kind switch
            {
                ProcessTemplateCanvasToolboxActionKind.BranchRouter => ProcessDefinitionCanvasToolboxActionKind.BranchRouter,
                ProcessTemplateCanvasToolboxActionKind.RoleBinding => ProcessDefinitionCanvasToolboxActionKind.RoleBinding,
                ProcessTemplateCanvasToolboxActionKind.ArtifactExpectation => ProcessDefinitionCanvasToolboxActionKind.ArtifactExpectation,
                ProcessTemplateCanvasToolboxActionKind.SubprocessBoundary => ProcessDefinitionCanvasToolboxActionKind.SubprocessBoundary,
                _ => ProcessDefinitionCanvasToolboxActionKind.Step
            },
            action.Label,
            action.Summary,
            ResolveToolboxIcon(action.Kind),
            IsEnabled: true,
            DisabledReason: null);

    private static string ResolveToolboxIcon(ProcessTemplateCanvasToolboxActionKind kind)
        => kind switch
        {
            ProcessTemplateCanvasToolboxActionKind.BranchRouter => "alt_route",
            ProcessTemplateCanvasToolboxActionKind.RoleBinding => "badge",
            ProcessTemplateCanvasToolboxActionKind.ArtifactExpectation => "inventory_2",
            ProcessTemplateCanvasToolboxActionKind.SubprocessBoundary => "account_tree",
            _ => "add"
        };

    private static IReadOnlyList<ProcessDefinitionCanvasCommandProjection> CreateCommands(
        ProcessDefinitionCanvasSnapshot snapshot)
        =>
        [
            new(
                ProcessDefinitionCanvasCommandKind.Recompose,
                "Recompose",
                "auto_fix_high",
                snapshot.Nodes.Count > 0,
                snapshot.Nodes.Count > 0 ? null : "The definition canvas has no nodes to recompose.")
        ];

    private static ProcessDefinitionCanvasToolboxActionProjection? ResolveToolboxAction(
        ProcessDefinitionCanvasSnapshot snapshot,
        ProcessDefinitionCanvasToolboxActionKey? actionKey)
    {
        if (actionKey is null)
        {
            return snapshot.ToolboxActions.FirstOrDefault(action => action.Kind == ProcessDefinitionCanvasToolboxActionKind.Step);
        }

        return snapshot.ToolboxActions.FirstOrDefault(action => action.ActionKey == actionKey);
    }

    private static ProcessDefinitionCanvasEditorNodeProjection? ResolveSelectedNode(
        ProcessDefinitionCanvasSnapshot snapshot,
        ProcessDefinitionCanvasNodeKey? nodeKey)
        => nodeKey is null
            ? null
            : snapshot.Nodes.FirstOrDefault(node => node.NodeKey == nodeKey);

    private static ProcessDefinitionCanvasEditorNodeProjection? FindLastStepNode(
        ProcessDefinitionCanvasSnapshot snapshot)
        => snapshot.Nodes.LastOrDefault(node => node.Kind == ProcessDefinitionCanvasNodeKind.Step);

    private static bool ShouldCreateBranchRouter(ProcessTemplateDefinitionCanvasStepSummary step)
        => step.BranchOutcomes.Count > 0 ||
           string.Equals(step.StepKind, "Decision", StringComparison.OrdinalIgnoreCase);

    private static bool IsSubprocessStep(ProcessTemplateDefinitionCanvasStepSummary step)
        => string.Equals(step.StepKind, "Subprocess", StringComparison.OrdinalIgnoreCase) ||
           !string.IsNullOrWhiteSpace(step.SubprocessProcessKey);

    private static double ResolveStepLane(
        ProcessTemplateDefinitionCanvasStepSummary step,
        int index)
    {
        if (step.Dependencies.Any(dependency => !string.IsNullOrWhiteSpace(dependency.DependsOnBranchOutcomeKey)))
        {
            return (index % 2 == 0 ? -110 : 130);
        }

        return 0;
    }

    private static string ResolveStepTone(string stepKind)
        => stepKind.Trim().ToLowerInvariant() switch
        {
            "decision" => "warning",
            "subprocess" => "info",
            "review" => "success",
            "end" => "neutral",
            _ => "info"
        };
}
