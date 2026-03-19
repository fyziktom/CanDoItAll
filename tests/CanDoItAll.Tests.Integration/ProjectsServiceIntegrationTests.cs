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
}
