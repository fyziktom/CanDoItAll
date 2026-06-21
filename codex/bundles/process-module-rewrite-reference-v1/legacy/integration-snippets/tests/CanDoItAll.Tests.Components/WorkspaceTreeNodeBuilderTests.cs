using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Components;

public sealed class WorkspaceTreeNodeBuilderTests
{
    [Fact]
    public void Project_tree_preserves_hierarchy_and_selected_leaf()
    {
        var parent = CreateProject("Platform");
        var child = CreateProject("Pilot", parentCount: 1);
        var nodes = ProjectPortfolioTreeNodeBuilder.Build(
            [parent, child],
            [new ProjectHierarchyLinkSummary(parent.Id, child.Id, DateTimeOffset.UtcNow)],
            child.Id,
            new HashSet<string>());

        var root = Assert.Single(nodes);
        Assert.Equal(parent.Name, root.Text);
        var childNode = Assert.Single(root.Children);
        Assert.Equal(child.Name, childNode.Text);
        Assert.True(childNode.IsSelected);
        Assert.True(root.IsExpanded);
        Assert.True(ProjectPortfolioTreeNodeBuilder.TryReadProjectId(childNode.Id, out var selectedId));
        Assert.Equal(child.Id, selectedId);
    }

    [Fact]
    public void Process_tree_groups_definitions_by_scope()
    {
        var projectId = Guid.NewGuid();
        var global = CreateProcess("Global intake", null, string.Empty);
        var scoped = CreateProcess("Project delivery", projectId, "Delivery");
        var nodes = ProcessDefinitionTreeNodeBuilder.Build([global, scoped], scoped.Id, new HashSet<string>());

        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, node => node.Text == "Global process library");
        var projectNode = Assert.Single(nodes, node => node.Text == "Delivery");
        var definitionNode = Assert.Single(projectNode.Children);
        Assert.True(definitionNode.IsSelected);
        Assert.True(projectNode.IsExpanded);
        Assert.True(ProcessDefinitionTreeNodeBuilder.TryReadDefinitionId(definitionNode.Id, out var selectedId));
        Assert.Equal(scoped.Id, selectedId);
    }

    [Fact]
    public void Workflow_tree_groups_definitions_by_lifecycle()
    {
        var active = CreateWorkflow("Review workflow", WorkflowLifecycleStatus.Active);
        var draft = CreateWorkflow("Draft workflow", WorkflowLifecycleStatus.Draft);
        var nodes = WorkflowDefinitionTreeNodeBuilder.Build([active, draft], active.Id, new HashSet<string>());

        Assert.Equal(2, nodes.Count);
        var activeGroup = Assert.Single(nodes, node => node.Text == "Active workflows");
        Assert.True(activeGroup.IsExpanded);
        var activeNode = Assert.Single(activeGroup.Children);
        Assert.True(activeNode.IsSelected);
        Assert.True(WorkflowDefinitionTreeNodeBuilder.TryReadDefinitionId(activeNode.Id, out var selectedId));
        Assert.Equal(active.Id, selectedId);
    }

    private static ProjectSummary CreateProject(string name, int parentCount = 0)
    {
        return new ProjectSummary(
            Guid.NewGuid(),
            name,
            ProjectStatus.Active,
            "Execution",
            2,
            parentCount,
            parentCount == 0 ? 1 : 0,
            DateTimeOffset.UtcNow);
    }

    private static ProcessDefinitionListItem CreateProcess(string name, Guid? projectId, string projectName)
    {
        return new ProcessDefinitionListItem(
            Guid.NewGuid(),
            projectId,
            name,
            ProcessDefinitionStatus.Draft,
            1,
            false,
            2,
            3,
            0,
            0,
            "Process summary",
            "Process value",
            projectName,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowCatalogItem CreateWorkflow(string name, WorkflowLifecycleStatus status)
    {
        return new WorkflowCatalogItem(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            name,
            "Workflow summary",
            status,
            WorkflowRuntimeBackendKind.DurableTask,
            DateTimeOffset.UtcNow);
    }
}
