using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectsAgentChatSelection(
    ProjectSummary Project);

public enum ProjectsAgentChatView
{
    Cards,
    Files
}

public static class ProjectsAgentChatContextBuilder
{
    public const string SourceKind = "projects";
    public const string WorkspaceSourceId = "projects";
    public const string PortfolioContributorId = "projects.portfolio";
    public const string SelectionContributorId = "projects.selection";
    private const int MaximumLabelLength = 160;

    public static AgentChatContextSource BuildSource(Guid? selectedProjectId)
    {
        if (selectedProjectId == Guid.Empty)
        {
            throw new ArgumentException("A selected project id cannot be empty.", nameof(selectedProjectId));
        }

        return new AgentChatContextSource(
            new AgentChatContextSourceKind(SourceKind),
            new AgentChatContextSourceId(
                selectedProjectId?.ToString("D") ?? WorkspaceSourceId));
    }

    public static AgentChatContextScope BuildScope(
        AgentChatContextScopeId scopeId,
        ProjectSummary? selectedProject,
        IEnumerable<AgentDefinition> agents,
        AgentChatContextAccessState accessState,
        ProjectsAgentChatView activeView = ProjectsAgentChatView.Cards)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (!Enum.IsDefined(accessState))
        {
            throw new ArgumentOutOfRangeException(nameof(accessState), accessState, "The context access state is undefined.");
        }

        var selectedProjectId = selectedProject?.Id;
        var access = ContextualAgentAccessResolver.Resolve(
                agents,
                ContextualAgentWorkspaceKind.ProjectStructure,
                selectedProjectId)
            .Select(item => new AgentChatContextAgentAccess(
                item.Agent.Id,
                ResolvePermissions(item),
                item.ScopeLabel))
            .ToArray();
        var displayName = selectedProject is null
            ? "Projects portfolio"
            : $"Projects · {NormalizeRequiredLabel(selectedProject.Name, nameof(selectedProject))}";
        var workspaceScope = selectedProject is null
            ? null
            : WorkspaceScopeDescriptor.Project(selectedProject.Id.ToString("D"));

        return new AgentChatContextScope(
            scopeId,
            BuildSource(selectedProjectId),
            displayName,
            workspaceScope,
            access,
            AgentChatContextScopeAccessMode.AllowListed,
            accessState,
            BuildPosition(selectedProject, activeView),
            completionRefreshMode: AgentChatContextCompletionRefreshMode.OnSuccessfulRun);
    }

    public static AgentChatSurfacePosition BuildPosition(
        ProjectSummary? selectedProject,
        ProjectsAgentChatView activeView = ProjectsAgentChatView.Cards)
    {
        if (!Enum.IsDefined(activeView))
        {
            throw new ArgumentOutOfRangeException(nameof(activeView), activeView, "The Projects agent-chat view is undefined.");
        }

        var selection = selectedProject is null
            ? null
            : new AgentChatContextEntityReference(
                "project",
                selectedProject.Id.ToString("D"),
                NormalizeRequiredLabel(selectedProject.Name, nameof(selectedProject)));
        var facts = selectedProject is null
            ? Array.Empty<AgentChatContextPositionFact>()
            :
            [
                new AgentChatContextPositionFact("status", selectedProject.Status.ToString()),
                new AgentChatContextPositionFact("phase", NormalizeOptionalLabel(selectedProject.CurrentPhase))
            ];

        return new AgentChatSurfacePosition(
            module: "projects",
            surface: "portfolio",
            view: activeView == ProjectsAgentChatView.Files ? "files" : "cards",
            route: "/projects",
            primarySelection: selection,
            facts: facts);
    }

    public static AgentChatContextFragment BuildPortfolioFragment()
    {
        const string content = """
Projects portfolio workspace (sanitized)
SelectedProject: None unless a separate selection fragment is present.
Create a standalone project only when the request does not identify a parent.
When the request asks for a project under or for the selected project, create a linked subproject of that selected project.
Read hierarchy and project content only through the authorized project tools; do not infer related projects from this context.
""";
        return new AgentChatContextFragment(
            new AgentChatContextContributorId(PortfolioContributorId),
            order: 50,
            content);
    }

    public static ProjectsAgentChatSelection BuildSelection(ProjectSummary selectedProject)
    {
        ArgumentNullException.ThrowIfNull(selectedProject);
        return new ProjectsAgentChatSelection(selectedProject);
    }

    public static AgentChatContextFragment BuildSelectionFragment(
        ProjectsAgentChatSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.Project);

        var content = new StringBuilder();
        content.AppendLine("Projects portfolio selection (sanitized)");
        content.Append("SelectedProjectId: ").AppendLine(selection.Project.Id.ToString("D"));
        content.Append("SelectedProjectName: ").AppendLine(NormalizeRequiredLabel(selection.Project.Name, nameof(selection)));
        content.Append("Status: ").AppendLine(selection.Project.Status.ToString());
        content.Append("CurrentPhase: ").AppendLine(NormalizeOptionalLabel(selection.Project.CurrentPhase));
        content.Append("PhaseCount: ").AppendLine(selection.Project.PhaseCount.ToString());

        return new AgentChatContextFragment(
            new AgentChatContextContributorId(SelectionContributorId),
            order: 100,
            content.ToString());
    }

    private static AgentChatContextPermission ResolvePermissions(
        ContextualAgentAccessSummary access)
    {
        var permissions = AgentChatContextPermission.Read;
        if (access.CanWrite ||
            access.CanWriteTasks ||
            access.CanWriteNonTaskStructure ||
            access.CanCreateProjects ||
            access.CanCreateSubprojects)
        {
            permissions |= AgentChatContextPermission.Mutate;
        }

        return permissions;
    }

    private static string NormalizeRequiredLabel(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = NormalizeLabel(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A Projects context label is required.", parameterName);
        }

        return normalized;
    }

    private static string NormalizeOptionalLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "None";
        }

        var normalized = NormalizeLabel(value);
        return string.IsNullOrWhiteSpace(normalized) ? "None" : normalized;
    }

    private static string NormalizeLabel(string value)
    {
        var content = new StringBuilder(Math.Min(value.Length, MaximumLabelLength));
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && content.Length > 0)
                {
                    content.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            if (content.Length >= MaximumLabelLength)
            {
                break;
            }

            content.Append(character);
            previousWasWhitespace = false;
        }

        return content.ToString().Trim();
    }
}
