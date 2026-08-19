using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureSubprojectTransferCoordinatorIntegrationTests
{
    [Fact]
    public async Task Move_descendants_creates_linked_child_and_moves_the_exact_branch()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var coordinator = scope.ServiceProvider.GetRequiredService<ProjectStructureSubprojectTransferCoordinator>();
        var sourceProjectId = await CreateProjectAsync(projects, "Coordinator source");
        var targetProjectId = Guid.NewGuid();
        var sourceAnchor = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Source anchor",
            $"project:{sourceProjectId:D}");
        var movedRoot = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Moved root",
            sourceAnchor.Id);
        var movedChild = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Moved child",
            movedRoot.Id);

        var result = await coordinator.MoveDescendantsToNewSubprojectAsync(
            sourceProjectId,
            targetProjectId,
            CreateEditor("Linked child"),
            sourceAnchor.Id);

        Assert.Equal(sourceProjectId, result.SourceProjectId);
        Assert.Equal(targetProjectId, result.TargetProjectId);
        Assert.Equal(
            new[] { movedRoot.Id, movedChild.Id }.OrderBy(id => id, StringComparer.Ordinal),
            result.Transfer.MovedNodeIds.OrderBy(id => id, StringComparer.Ordinal));

        var hierarchy = await projects.GetHierarchyAsync(sourceProjectId);
        var childProject = Assert.Single(hierarchy.ChildProjects, project => project.Id == targetProjectId);
        Assert.Equal("Linked child", childProject.Name);

        var sourceAfter = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceAfter.Nodes, node => node.Id == sourceAnchor.Id);
        Assert.DoesNotContain(sourceAfter.Nodes, node => node.Id == movedRoot.Id || node.Id == movedChild.Id);

        var targetAfter = await workbench.GetStructureAsync(targetProjectId);
        Assert.Equal($"project:{targetProjectId:D}", Assert.Single(targetAfter.Nodes, node => node.Id == movedRoot.Id).ParentId);
        Assert.Equal(movedRoot.Id, Assert.Single(targetAfter.Nodes, node => node.Id == movedChild.Id).ParentId);
    }

    [Fact]
    public async Task Empty_descendant_scope_removes_the_created_child_and_keeps_source_unchanged()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var coordinator = scope.ServiceProvider.GetRequiredService<ProjectStructureSubprojectTransferCoordinator>();
        var sourceProjectId = await CreateProjectAsync(projects, "Empty transfer source");
        var targetProjectId = Guid.NewGuid();
        var sourceAnchor = await CreateNodeAsync(
            workbench,
            sourceProjectId,
            "Leaf anchor",
            $"project:{sourceProjectId:D}");

        var exception = await Assert.ThrowsAsync<ProjectStructureCompensatedSubprojectTransferException>(() =>
            coordinator.MoveDescendantsToNewSubprojectAsync(
                sourceProjectId,
                targetProjectId,
                CreateEditor("Temporary child"),
                sourceAnchor.Id));

        Assert.Equal(targetProjectId, exception.RemovedProjectId);
        var projectsAfter = await projects.ListAsync();
        Assert.DoesNotContain(projectsAfter, project => project.Id == targetProjectId);
        var hierarchy = await projects.GetHierarchyAsync(sourceProjectId);
        Assert.DoesNotContain(hierarchy.ChildProjects, project => project.Id == targetProjectId);
        var sourceAfter = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceAfter.Nodes, node => node.Id == sourceAnchor.Id);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name)
    {
        var result = await projects.SaveAsync(CreateEditor(name));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectEditorModel CreateEditor(string name)
    {
        return new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description.",
            Objective = $"{name} objective.",
            CurrentPhase = "Execution",
            Status = ProjectStatus.Active
        };
    }

    private static Task<ProjectStructureNode> CreateNodeAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title,
        string parentNodeId)
    {
        return workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                title,
                string.Empty,
                $"{title} notes.",
                parentNodeId));
    }
}
