using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectsPageTests
{
    [Fact]
    public async Task Saves_project_from_wizard_first_flow()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.Find("[data-testid='project-name-input']").Change("Wizard Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Wizard Project", cut.Markup);
            Assert.Contains("Project saved", cut.Markup);
        });
    }

    [Fact]
    public async Task Shows_saved_project_as_card_with_dashboard_action()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-new-button']").Click();
        cut.Find("[data-testid='project-name-input']").Change("Card Modal Project");
        cut.Find("[data-testid='project-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Card Modal Project", cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='project-card']"));
            Assert.Contains("Open dashboard tab", cut.Markup);
        });
    }

    [Fact]
    public async Task Project_overview_modal_explains_that_header_uses_saved_project_name()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projectsService, "Explained Project");

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Explained Project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Explained Project", StringComparison.Ordinal));

        projectCard.QuerySelector("[data-testid='project-card-details-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-detail-modal']");

            Assert.Contains("Explained Project", modal.TextContent);
            Assert.Contains("This header uses the saved project name.", modal.TextContent);
            Assert.Contains("Edit name and details", modal.TextContent);
        });

        Assert.NotEqual(Guid.Empty, projectId);
    }

    [Fact]
    public async Task Filters_direct_subprojects_of_the_selected_project()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Alpha parent");
        var childProjectId = await CreateProjectAsync(projectsService, "Beta child");
        var unrelatedProjectId = await CreateProjectAsync(projectsService, "Gamma unrelated");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.NotEqual(Guid.Empty, unrelatedProjectId);

        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='hierarchy-filter-project']").Change(parentProjectId.ToString());
        cut.Find("[data-testid='hierarchy-filter-mode']").Change("children");

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("[data-testid='project-card']");

            Assert.Single(cards);
            Assert.Contains("Beta child", cards[0].TextContent);
        });
    }

    [Fact]
    public async Task Subprojects_modal_supports_recursive_drill_down()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parentProjectId = await CreateProjectAsync(projectsService, "Root project");
        var childProjectId = await CreateProjectAsync(projectsService, "Nested child");
        var grandchildProjectId = await CreateProjectAsync(projectsService, "Nested grandchild");

        Assert.True((await projectsService.AddSubprojectAsync(parentProjectId, childProjectId)).IsSuccess);
        Assert.True((await projectsService.AddSubprojectAsync(childProjectId, grandchildProjectId)).IsSuccess);

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Root project", cut.Markup));

        var parentCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Root project", StringComparison.Ordinal));
        parentCard.QuerySelector("[data-testid='project-card-subprojects-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-hierarchy-modal']");

            Assert.Contains("Nested child", modal.TextContent);
        });

        var childCard = cut.FindAll("[data-testid='hierarchy-subproject-card']")
            .Single(card => card.TextContent.Contains("Nested child", StringComparison.Ordinal));
        childCard.QuerySelector("[data-testid='hierarchy-card-subprojects-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-hierarchy-modal']");

            Assert.Contains("Nested grandchild", modal.TextContent);
        });
    }

    [Fact]
    public async Task Project_card_opens_mermaid_gantt_modal()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var workbenchService = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projectsService, "Gantt preview project");
        var prerequisiteNote = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Architect plan",
                "Draft note",
                "Create the plan before implementation starts.",
                $"project:{projectId}",
                420,
                240));
        var implementationTask = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Implement feature",
                "Execution",
                "Build the project-card Mermaid Gantt preview.",
                $"project:{projectId}",
                X: 700,
                Y: 260,
                ObjectSubtype: "task",
                DurationSeconds: 7200));
        await workbenchService.LinkObjectsAsync(projectId, implementationTask.Id, prerequisiteNote.Id, ProjectObjectLinkKind.DependsOn);

        var cut = harness.Context.RenderComponent<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.Contains("Gantt preview project", cut.Markup));
        var projectCard = cut.FindAll("[data-testid='project-card']")
            .Single(card => card.TextContent.Contains("Gantt preview project", StringComparison.Ordinal));

        projectCard.QuerySelector("[data-testid='project-card-gantt-button']")!.Click();

        cut.WaitForAssertion(() =>
        {
            var modal = cut.Find("[data-testid='projects-gantt-modal']");
            var diagram = cut.Find("[data-testid='projects-gantt-mermaid-diagram']");
            var source = cut.Find("[data-testid='projects-gantt-mermaid-source']");

            Assert.Contains("Gantt preview project", modal.TextContent);
            Assert.Contains("cda-mermaid", diagram.ClassList);
            Assert.NotNull(cut.Find("[data-testid='projects-gantt-copy-source-button']"));
            Assert.Contains("Architect plan", source.TextContent);
            Assert.Contains("Implement feature", source.TextContent);
            Assert.Contains("gantt", source.TextContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("after", source.TextContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Package_import_requires_a_package_path()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.RenderComponent<ProjectsPage>();

        cut.Find("[data-testid='projects-import-package-button']").Click();

        cut.WaitForAssertion(() =>
        {
            var message = cut.Find("[data-testid='projects-package-message']");

            Assert.Contains("Choose a project package path", message.TextContent);
        });
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


