using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentFrameworkWorkflowsChatView
{
    Dashboard,
    Workflows,
    Editor,
    History,
    Analytics
}

public sealed record WorkflowAgentChatNodeSelection(
    WorkflowId DefinitionId,
    WorkflowNodeId NodeId,
    string Name,
    WorkflowNodeKind Kind);

public sealed record WorkflowAgentChatProjectSelection(
    Guid ProjectId,
    string Name);

public static class AgentFrameworkWorkflowsChatContextBuilder
{
    public const string SourceKind = "workflows";
    public const string Route = "/agents/workflows";
    public const string Module = "agent-framework";
    public const string Surface = "workflows";

    public static AgentFrameworkWorkflowsChatView ResolveView(int activeTabIndex)
        => activeTabIndex switch
        {
            0 => AgentFrameworkWorkflowsChatView.Dashboard,
            1 => AgentFrameworkWorkflowsChatView.Workflows,
            2 => AgentFrameworkWorkflowsChatView.Editor,
            3 => AgentFrameworkWorkflowsChatView.History,
            4 => AgentFrameworkWorkflowsChatView.Analytics,
            _ => throw new ArgumentOutOfRangeException(
                nameof(activeTabIndex),
                activeTabIndex,
                "The Workflows tab index is not supported.")
        };

    public static AgentChatContextSurface Build(
        AgentFrameworkWorkflowsChatView view,
        int definitionCount,
        WorkflowId? selectedDefinitionId,
        WorkflowCatalogItem? selectedDefinitionSummary,
        WorkflowDefinition? selectedDefinition,
        WorkflowRunSnapshot? selectedRun,
        bool historyLoaded,
        int historyRunTotalCount,
        int pendingRequestCount,
        int artifactCount,
        int validationIssueCount,
        WorkflowAgentChatNodeSelection? selectedNode = null,
        WorkflowAgentChatProjectSelection? selectedProject = null)
    {
        ValidateCount(definitionCount, nameof(definitionCount));
        ValidateCount(historyRunTotalCount, nameof(historyRunTotalCount));
        ValidateCount(pendingRequestCount, nameof(pendingRequestCount));
        ValidateCount(artifactCount, nameof(artifactCount));
        ValidateCount(validationIssueCount, nameof(validationIssueCount));
        if (!Enum.IsDefined(view))
        {
            throw new ArgumentOutOfRangeException(nameof(view), view, "The Workflows view is not supported.");
        }

        var viewToken = ResolveViewToken(view);
        var viewLabel = ResolveViewLabel(view);
        var definitionId = SupportsDefinitionSelection(view)
            ? selectedDefinitionId
            : null;
        var definitionSummary = definitionId.HasValue && selectedDefinitionSummary?.Id == definitionId.Value
            ? selectedDefinitionSummary
            : null;
        var definition = definitionId.HasValue && selectedDefinition?.Id == definitionId.Value
            ? selectedDefinition
            : null;
        var run = SupportsRunSelection(view) &&
                  definitionId.HasValue &&
                  selectedRun?.WorkflowId == definitionId.Value
            ? selectedRun
            : null;
        var node = view == AgentFrameworkWorkflowsChatView.Editor &&
                   definitionId.HasValue &&
                   selectedNode?.DefinitionId == definitionId.Value
            ? selectedNode
            : null;
        var primarySelection = definitionId.HasValue
            ? BuildDefinitionReference(
                definitionId.Value,
                definition?.Name ?? definitionSummary?.Name)
            : null;
        var project = selectedProject?.ProjectId == Guid.Empty
            ? null
            : selectedProject;
        var selectedEntities = BuildSelectedEntities(project, run, node);

        return new AgentChatContextSurface(
            new AgentChatContextSource(
                new AgentChatContextSourceKind(SourceKind),
                new AgentChatContextSourceId(
                    BuildSourceId(viewToken, project, definitionId))),
            $"Workflows · {viewLabel}",
            new AgentChatSurfacePosition(
                Module,
                Surface,
                viewToken,
                BuildRoute(project, definitionId, run),
                primarySelection,
                selectedEntities,
                BuildFacts(
                    definitionCount,
                    project,
                    definitionId,
                    definitionSummary,
                    definition,
                    run,
                    historyLoaded,
                    historyRunTotalCount,
                    pendingRequestCount,
                    artifactCount,
                    validationIssueCount,
                    node)),
            agentAccess:
            [
                new AgentChatContextAgentAccess(
                    WorkflowCuratorAgentIdentity.AgentId,
                    AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                    "Workflows")
            ],
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun);
    }

    private static IReadOnlyList<AgentChatContextPositionFact> BuildFacts(
        int definitionCount,
        WorkflowAgentChatProjectSelection? project,
        WorkflowId? definitionId,
        WorkflowCatalogItem? definitionSummary,
        WorkflowDefinition? definition,
        WorkflowRunSnapshot? run,
        bool historyLoaded,
        int historyRunTotalCount,
        int pendingRequestCount,
        int artifactCount,
        int validationIssueCount,
        WorkflowAgentChatNodeSelection? node)
    {
        var facts = new List<AgentChatContextPositionFact>
        {
            new("definition-count", definitionCount.ToString()),
            new("history-state", historyLoaded ? "loaded" : "deferred")
        };
        if (project is not null)
        {
            facts.Add(new AgentChatContextPositionFact("project-id", project.ProjectId.ToString("D")));
        }

        if (definitionId.HasValue)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-id", definitionId.Value.Value.ToString("D")));
        }

        var status = definition?.Status ?? definitionSummary?.Status;
        var backend = definition?.RuntimePolicy.PreferredBackend ?? definitionSummary?.PreferredBackend;
        if (status.HasValue)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-status", status.Value.ToString()));
        }

        if (backend.HasValue)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-backend", backend.Value.ToString()));
        }

        if (definition is not null)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-node-count", definition.Graph.Nodes.Count.ToString()));
            facts.Add(new AgentChatContextPositionFact("workflow-edge-count", definition.Graph.Edges.Count.ToString()));
            facts.Add(new AgentChatContextPositionFact("validation-issue-count", validationIssueCount.ToString()));
        }

        if (historyLoaded)
        {
            facts.Add(new AgentChatContextPositionFact("history-run-count", historyRunTotalCount.ToString()));
            facts.Add(new AgentChatContextPositionFact("pending-request-count", pendingRequestCount.ToString()));
            facts.Add(new AgentChatContextPositionFact("artifact-count", artifactCount.ToString()));
        }

        if (run is not null)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-run-id", run.RunId.Value.ToString("D")));
            facts.Add(new AgentChatContextPositionFact("run-state", run.State.ToString()));
            facts.Add(new AgentChatContextPositionFact("run-backend", run.Backend.ToString()));
        }

        if (node is not null)
        {
            facts.Add(new AgentChatContextPositionFact("workflow-node-kind", node.Kind.ToString()));
        }

        return facts;
    }

    private static IReadOnlyList<AgentChatContextEntityReference> BuildSelectedEntities(
        WorkflowAgentChatProjectSelection? project,
        WorkflowRunSnapshot? run,
        WorkflowAgentChatNodeSelection? node)
    {
        var selectedEntities = new List<AgentChatContextEntityReference>(2);
        if (project is not null)
        {
            selectedEntities.Add(new AgentChatContextEntityReference(
                "project",
                project.ProjectId.ToString("D"),
                BuildProjectDisplayName(project)));
        }

        if (run is not null)
        {
            selectedEntities.Add(BuildRunReference(run));
        }
        else if (node is not null)
        {
            selectedEntities.Add(BuildNodeReference(node));
        }

        return selectedEntities;
    }

    private static string BuildSourceId(
        string viewToken,
        WorkflowAgentChatProjectSelection? project,
        WorkflowId? definitionId)
    {
        if (!definitionId.HasValue)
        {
            return viewToken;
        }

        return project is null
            ? $"workflow:{definitionId.Value.Value:D}"
            : $"project:{project.ProjectId:D}:workflow:{definitionId.Value.Value:D}";
    }

    private static string BuildRoute(
        WorkflowAgentChatProjectSelection? project,
        WorkflowId? definitionId,
        WorkflowRunSnapshot? run)
    {
        if (!definitionId.HasValue)
        {
            return Route;
        }

        var projectQuery = project is null
            ? string.Empty
            : $"projectId={project.ProjectId:D}&";
        var runQuery = run is null
            ? string.Empty
            : $"&runId={run.RunId.Value:D}";
        return $"{Route}?{projectQuery}workflowId={definitionId.Value.Value:D}{runQuery}";
    }

    private static string BuildProjectDisplayName(WorkflowAgentChatProjectSelection project)
    {
        if (string.IsNullOrWhiteSpace(project.Name))
        {
            return $"Project {project.ProjectId:D}";
        }

        var trimmed = project.Name.Trim();
        return trimmed.Length <= AgentChatPositionLimits.MaximumLabelLength
            ? trimmed
            : trimmed[..AgentChatPositionLimits.MaximumLabelLength].TrimEnd();
    }

    private static AgentChatContextEntityReference BuildDefinitionReference(
        WorkflowId definitionId,
        string? name)
        => new(
            "workflow-definition",
            definitionId.Value.ToString("D"),
            BuildDefinitionDisplayName(definitionId, name));

    private static AgentChatContextEntityReference BuildRunReference(WorkflowRunSnapshot run)
        => new(
            "workflow-run",
            run.RunId.Value.ToString("D"),
            $"Run {run.RunId.Value:N}");

    private static AgentChatContextEntityReference BuildNodeReference(WorkflowAgentChatNodeSelection node)
        => new(
            "workflow-node",
            node.NodeId.Value,
            string.IsNullOrWhiteSpace(node.Name) ? node.NodeId.Value : node.Name.Trim());

    private static string BuildDefinitionDisplayName(
        WorkflowId definitionId,
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"Workflow {definitionId.Value:D}";
        }

        var trimmed = name.Trim();
        return trimmed.Length <= AgentChatPositionLimits.MaximumLabelLength
            ? trimmed
            : trimmed[..AgentChatPositionLimits.MaximumLabelLength].TrimEnd();
    }

    private static bool SupportsDefinitionSelection(AgentFrameworkWorkflowsChatView view)
        => view is AgentFrameworkWorkflowsChatView.Dashboard or
            AgentFrameworkWorkflowsChatView.Workflows or
            AgentFrameworkWorkflowsChatView.Editor or
            AgentFrameworkWorkflowsChatView.History;

    private static bool SupportsRunSelection(AgentFrameworkWorkflowsChatView view)
        => view is AgentFrameworkWorkflowsChatView.Dashboard or AgentFrameworkWorkflowsChatView.History;

    private static string ResolveViewToken(AgentFrameworkWorkflowsChatView view)
        => view switch
        {
            AgentFrameworkWorkflowsChatView.Dashboard => "dashboard",
            AgentFrameworkWorkflowsChatView.Workflows => "workflows",
            AgentFrameworkWorkflowsChatView.Editor => "editor",
            AgentFrameworkWorkflowsChatView.History => "history",
            AgentFrameworkWorkflowsChatView.Analytics => "analytics",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Workflows view is not supported.")
        };

    private static string ResolveViewLabel(AgentFrameworkWorkflowsChatView view)
        => view switch
        {
            AgentFrameworkWorkflowsChatView.Dashboard => "Dashboard",
            AgentFrameworkWorkflowsChatView.Workflows => "Workflows",
            AgentFrameworkWorkflowsChatView.Editor => "Editor",
            AgentFrameworkWorkflowsChatView.History => "History",
            AgentFrameworkWorkflowsChatView.Analytics => "Analytics",
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "The Workflows view is not supported.")
        };

    private static void ValidateCount(int value, string parameterName)
        => ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
}
