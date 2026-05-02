using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsServiceIntegrationTests
{
    [Fact]
    public async Task SaveAsync_writes_project_search_document_and_activity_entry()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var activityService = scope.ServiceProvider.GetRequiredService<ActivityService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();

        var editor = await projectsService.GetAsync(null);
        editor.Name = "Integration Project";
        editor.Description = "Project description";
        editor.Objective = "Ship the feature";
        editor.CurrentPhase = "Build";
        editor.Phases.Add(new ProjectPhaseEditorModel { Name = "Build", Goal = "Implement the first slice", Status = ProjectPhaseStatus.Active });
        editor.Options.First(option => option.Category == ProjectOptionCategory.Language).OptionName = "C#";

        var result = await projectsService.SaveAsync(editor);

        Assert.True(result.IsSuccess);

        var searchResults = await searchIndexService.SearchAsync("Integration Project");
        Assert.Contains(searchResults, item => item.Route.Contains("/projects?projectId=", StringComparison.Ordinal));

        var activityEntries = await activityService.ListRecentAsync();
        Assert.Contains(activityEntries, item => item.Title == "Created project" && item.Description == "Integration Project");
    }

    [Fact]
    public async Task Hierarchy_queries_include_parent_and_child_counts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var parentProjectId = await CreateProjectAsync(projectsService, "Parent project");
        var sharedParentProjectId = await CreateProjectAsync(projectsService, "Shared parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Child project");
        var siblingProjectId = await CreateProjectAsync(projectsService, "Sibling project");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(sharedParentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, siblingProjectId)).IsSuccess);

        var projectSummaries = await projectsService.ListAsync();
        var childSummary = Assert.Single(projectSummaries, project => project.Id == childProjectId);
        var parentSummary = Assert.Single(projectSummaries, project => project.Id == parentProjectId);

        Assert.Equal(2, childSummary.ParentCount);
        Assert.Equal(2, parentSummary.ChildCount);

        var hierarchyLinks = await projectsService.ListHierarchyLinksAsync();
        Assert.Contains(hierarchyLinks, link => link.ParentProjectId == parentProjectId && link.ChildProjectId == childProjectId);
        Assert.Contains(hierarchyLinks, link => link.ParentProjectId == sharedParentProjectId && link.ChildProjectId == childProjectId);

        var hierarchy = await projectsService.GetHierarchyAsync(childProjectId);

        Assert.Equal(childProjectId, hierarchy.ProjectId);
        Assert.Equal(2, hierarchy.ParentProjects.Count);
        Assert.Empty(hierarchy.ChildProjects);
        Assert.Contains(hierarchy.ParentProjects, project => project.Id == parentProjectId);
        Assert.Contains(hierarchy.ParentProjects, project => project.Id == sharedParentProjectId);
    }

    [Fact]
    public async Task SaveAsync_normalizes_date_only_editor_values_to_utc()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var editor = await projectsService.GetAsync(null);
        editor.Name = "Date normalization project";
        editor.Description = "Project date normalization validation";
        editor.Objective = "Save browser date input values without provider-specific timestamp failures.";
        editor.CurrentPhase = "Planning";
        editor.TargetDateUtc = new DateTime(2026, 5, 8);
        editor.Phases.Add(new ProjectPhaseEditorModel
        {
            Name = "Planning",
            Goal = "Validate project phase date normalization",
            StartDateUtc = new DateTime(2026, 5, 1),
            EndDateUtc = new DateTime(2026, 5, 8)
        });

        var result = await projectsService.SaveAsync(editor);

        Assert.True(result.IsSuccess);

        var saved = await projectsService.GetAsync(result.Value);
        Assert.Equal(DateTimeKind.Utc, saved.TargetDateUtc?.Kind);
        var phase = Assert.Single(saved.Phases);
        Assert.Equal(DateTimeKind.Utc, phase.StartDateUtc?.Kind);
        Assert.Equal(DateTimeKind.Utc, phase.EndDateUtc?.Kind);
    }

    [Fact]
    public async Task AddSubprojectAsync_rejects_cycles_and_ReconnectSubprojectAsync_moves_the_selected_parent_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var rootProjectId = await CreateProjectAsync(projectsService, "Root project");
        var middleProjectId = await CreateProjectAsync(projectsService, "Middle project");
        var leafProjectId = await CreateProjectAsync(projectsService, "Leaf project");
        var replacementParentProjectId = await CreateProjectAsync(projectsService, "Replacement parent");

        Assert.True((await projectsService.AddSubprojectAsync(rootProjectId, middleProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(middleProjectId, leafProjectId)).IsSuccess);

        var cycleAttempt = await projectsService.AddSubprojectAsync(leafProjectId, rootProjectId);

        Assert.True(cycleAttempt.IsFailure);
        Assert.Contains(cycleAttempt.Errors, error => error.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase));

        var reconnectResult = await projectsService.ReconnectSubprojectAsync(
            leafProjectId,
            middleProjectId,
            replacementParentProjectId);

        Assert.True(reconnectResult.IsSuccess);

        var hierarchyLinks = await projectsService.ListHierarchyLinksAsync();
        Assert.DoesNotContain(hierarchyLinks, link => link.ParentProjectId == middleProjectId && link.ChildProjectId == leafProjectId);
        Assert.Contains(hierarchyLinks, link => link.ParentProjectId == replacementParentProjectId && link.ChildProjectId == leafProjectId);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
