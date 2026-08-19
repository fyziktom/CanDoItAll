using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectsServiceIntegrationTests
{
    [Fact]
    public async Task SaveAsync_writes_project_search_document()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
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
    public async Task CreateSubprojectAsync_creates_the_project_and_parent_link_together()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Quotation portfolio");
        var reservedSubprojectId = Guid.NewGuid();

        var result = await projectsService.CreateSubprojectAsync(
            parentProjectId,
            reservedSubprojectId,
            new ProjectEditorModel
            {
                Name = "Machines Details",
                Description = "Machine types and parameters from quotations.",
                Objective = "Keep structured machine specifications.",
                CurrentPhase = "Discovery",
                Status = ProjectStatus.Active
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(reservedSubprojectId, result.Value);
        var hierarchy = await projectsService.GetHierarchyAsync(parentProjectId);
        var child = Assert.Single(hierarchy.ChildProjects, project => project.Id == result.Value);
        Assert.Equal("Machines Details", child.Name);
        Assert.Equal(ProjectStatus.Active, child.Status);
    }

    [Fact]
    public async Task CreateAsync_uses_the_reserved_id_and_rejects_reuse()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var reservedProjectId = Guid.NewGuid();

        var created = await projectsService.CreateAsync(
            reservedProjectId,
            new ProjectEditorModel
            {
                Name = "Reserved project",
                Objective = "Prove grant-first project creation.",
                CurrentPhase = "Discovery"
            });
        var duplicate = await projectsService.CreateAsync(
            reservedProjectId,
            new ProjectEditorModel
            {
                Name = "Duplicate reserved project",
                Objective = "Must not overwrite the first project.",
                CurrentPhase = "Discovery"
            });

        Assert.True(created.IsSuccess);
        Assert.Equal(reservedProjectId, created.Value);
        Assert.True(duplicate.IsFailure);
        var saved = await projectsService.GetAsync(reservedProjectId);
        Assert.Equal("Reserved project", saved.Name);
    }

    [Fact]
    public async Task CreateAsync_returns_the_committed_project_when_search_projection_fails()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<ISearchIndexService>();
                services.AddSingleton<ISearchIndexService, ThrowingSearchIndexService>();
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var reservedProjectId = Guid.NewGuid();

        var result = await projectsService.CreateAsync(
            reservedProjectId,
            new ProjectEditorModel
            {
                Name = "Committed despite projection failure",
                Objective = "Keep authoritative project persistence independent from projections.",
                CurrentPhase = "Discovery"
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(reservedProjectId, result.Value);
        Assert.Equal(
            "Committed despite projection failure",
            (await projectsService.GetAsync(reservedProjectId)).Name);
    }

    [Fact]
    public async Task CreateSubprojectAsync_with_missing_parent_does_not_leave_an_orphan_project()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var result = await projectsService.CreateSubprojectAsync(
            Guid.NewGuid(),
            new ProjectEditorModel
            {
                Name = "Orphan candidate",
                Description = "Must not be stored.",
                Objective = "Validate atomic parent checking.",
                CurrentPhase = "Discovery"
            });

        Assert.True(result.IsFailure);
        Assert.DoesNotContain(
            await projectsService.ListAsync(),
            project => string.Equals(project.Name, "Orphan candidate", StringComparison.Ordinal));
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

    private sealed class ThrowingSearchIndexService : ISearchIndexService
    {
        public Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Expected search projection failure.");
        }

        public Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            int take = 12,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        }
    }
}
