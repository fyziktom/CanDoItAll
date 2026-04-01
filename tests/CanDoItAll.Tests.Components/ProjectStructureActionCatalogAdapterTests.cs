using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureActionCatalogAdapterTests
{
    [Fact]
    public void Prompt_flow_context_actions_include_wizard_and_create_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode("flow", ProjectObjectType.PromptFlow, "Flow", 0, 0);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "wizard");
        Assert.Contains(actions, action => action.ActionId == "copy-info");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
        Assert.Contains(actions, action => action.ActionId.StartsWith("add-", StringComparison.Ordinal));
    }

    [Fact]
    public void Group_context_actions_expose_border_and_shared_status_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();

        var actions = adapter.BuildGroupContextActions();

        Assert.Contains(actions, action => action.ActionId == "group-frame");
        Assert.Contains(actions, action => action.ActionId == "group-clear-frame");
        Assert.Contains(actions, action => action.ActionId == "progress");
        Assert.Contains(actions, action => action.ActionId == "marker");
        Assert.Contains(actions, action => action.ActionId == "priority");
    }

    [Fact]
    public void Project_nodes_keep_project_actions_and_expose_create_tools()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "project-child:11111111-1111-1111-1111-111111111111",
            ProjectObjectType.ProjectRoot,
            "Project child",
            0,
            0,
            projectRole: ProjectStructureProjectRole.Subproject,
            relatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            parentProjectCount: 2);

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "open");
        Assert.Contains(actions, action => action.ActionId == "copy-info");
        Assert.Contains(actions, action => action.ActionId == "project:open-structure");
        Assert.Contains(actions, action => action.ActionId == "project:add-subproject");
        Assert.Contains(actions, action => action.ActionId == "project:reconnect-subproject");
        Assert.Contains(actions, action => action.ActionId == "add-note");
        Assert.Contains(actions, action => action.ActionId == "group-blocks");
        Assert.DoesNotContain(actions, action => action.ActionId == "reconnect");
        Assert.DoesNotContain(actions, action => action.ActionId == "disconnect");
        Assert.DoesNotContain(actions, action => action.ActionId == "delete");
    }

    [Fact]
    public void Additional_parent_project_nodes_remain_read_only()
    {
        var adapter = new ProjectStructureActionCatalogAdapter();
        var node = CreateNode(
            "project-related-parent:11111111-1111-1111-1111-111111111111",
            ProjectObjectType.ProjectRoot,
            "Shared parent",
            0,
            0,
            projectRole: ProjectStructureProjectRole.AdditionalParentProject,
            relatedProjectId: Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var actions = adapter.BuildNodeContextActions(node);

        Assert.Contains(actions, action => action.ActionId == "open");
        Assert.DoesNotContain(actions, action => action.ActionId.StartsWith("add-", StringComparison.Ordinal));
        Assert.DoesNotContain(actions, action => action.ActionId.StartsWith("group-", StringComparison.Ordinal));
    }

    private static ProjectStructureNode CreateNode(
        string id,
        ProjectObjectType objectType,
        string title,
        double x,
        double y,
        ProjectStructureProjectRole projectRole = ProjectStructureProjectRole.None,
        Guid? relatedProjectId = null,
        int parentProjectCount = 0)
        => new(
            id,
            null,
            objectType,
            string.Empty,
            title,
            string.Empty,
            "Draft",
            string.Empty,
            $"/projects/1/{id}",
            title,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            x,
            y,
            new ProjectObjectVisualProfile("rect", "#2563eb", "ID", title),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            ProjectRole: projectRole,
            RelatedProjectId: relatedProjectId,
            ParentProjectCount: parentProjectCount);
}


