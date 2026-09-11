using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class WorkbenchOwnerProjectionLimitTests {
    [Fact]
    public async Task Hierarchy_limit_rejects_a_large_reached_subtree_without_returning_a_partial_graph() {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(ProjectsModuleAssemblyMarker).Assembly]);
        var owners = new WorkbenchOwnerInMemoryFixture("hierarchy-limit");
        var root = new Project { Name = "Root" };
        await using (var context = await owners.ProjectsFactory.CreateDbContextAsync()) {
            context.Add(root);
            var children = Enumerable.Range(0, ProjectStructureProjectionQueryLimits.MaximumProjects)
                .Select(index => new Project { Name = $"Child {index:D4}" }).ToArray();
            context.AddRange(children);
            context.AddRange(children.Select(child => new ProjectHierarchyLink { ParentProjectId = root.Id, ChildProjectId = child.Id }));
            await context.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProjectStructureProjectionLimitException>(() => owners.Hierarchy.GetAsync(root.Id));
    }

    [Fact]
    public async Task Frontier_query_preserves_filter_before_limit_and_validates_explicit_bounds() {
        var owners = new WorkbenchOwnerInMemoryFixture("hierarchy-frontier");
        var parent = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await using (var context = await owners.ProjectsFactory.CreateDbContextAsync()) {
            context.AddRange(new ProjectHierarchyLink { ParentProjectId = parent, ChildProjectId = first },
                new ProjectHierarchyLink { ParentProjectId = parent, ChildProjectId = second });
            await context.SaveChangesAsync();
        }
        Assert.Equal([second], await owners.Hierarchy.GetChildIdsAsync([parent], [first], 1));
        await Assert.ThrowsAsync<ArgumentException>(() => owners.Hierarchy.GetChildIdsAsync([parent], [], 0));
        await Assert.ThrowsAsync<ArgumentException>(() => owners.Hierarchy.GetChildIdsAsync([Guid.Empty], [], 1));
        await Assert.ThrowsAsync<ArgumentException>(() => owners.Hierarchy.GetChildIdsAsync(
            Enumerable.Range(0, ProjectStructureProjectionQueryLimits.FrontierBatchSize + 1).Select(_ => Guid.NewGuid()).ToArray(), [], 1));
    }
}
