using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectsAgentChatContextBuilderTests
{
    [Fact]
    public void Selection_fragment_contains_only_the_selected_project_fields()
    {
        var selected = CreateProject(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Selected project",
            parentCount: 2,
            childCount: 2);

        var selection = ProjectsAgentChatContextBuilder.BuildSelection(selected);
        var fragment = ProjectsAgentChatContextBuilder.BuildSelectionFragment(selection);
        var scope = ProjectsAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            selected,
            agents: [],
            AgentChatContextAccessState.Ready);

        Assert.Contains($"SelectedProjectId: {selected.Id:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("SelectedProjectName: Selected project", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Status: Active", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("CurrentPhase: Delivery", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("PhaseCount: 2", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Parent", fragment.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Subproject", fragment.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(selected.Id.ToString("D"), scope.Source.Id.Value);
        Assert.Equal(
            WorkspaceScopeDescriptor.Project(selected.Id.ToString("D")),
            scope.WorkspaceScope);
    }

    [Fact]
    public void Portfolio_context_is_static_and_does_not_disclose_portfolio_counts()
    {
        var fragment = ProjectsAgentChatContextBuilder.BuildPortfolioFragment();
        var scope = ProjectsAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            selectedProject: null,
            agents: [],
            AgentChatContextAccessState.Ready);

        Assert.Contains("SelectedProject: None", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("create a linked subproject", fragment.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProjectCount", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("VisibleProjectCount", fragment.Content, StringComparison.Ordinal);
        Assert.Equal("projects", scope.Source.Kind.Value);
        Assert.Equal(ProjectsAgentChatContextBuilder.WorkspaceSourceId, scope.Source.Id.Value);
        Assert.Null(scope.WorkspaceScope);
    }

    private static ProjectSummary CreateProject(
        Guid id,
        string name,
        int parentCount = 0,
        int childCount = 0)
    {
        return new ProjectSummary(
            id,
            name,
            ProjectStatus.Active,
            "Delivery",
            PhaseCount: 2,
            ParentCount: parentCount,
            ChildCount: childCount,
            UpdatedAtUtc: DateTimeOffset.Parse("2026-07-17T10:00:00Z"));
    }
}
