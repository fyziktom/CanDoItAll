using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ResourcesPageTests
{
    private const string FolderConnectorPluginKey = "resource.folder";

    [Fact]
    public async Task Saves_repository_resource_through_typed_editor()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var project = await projectsService.GetAsync(null);
        project.Name = "Resource Project";
        var savedProject = await projectsService.SaveAsync(project);
        Assert.True(savedProject.IsSuccess);

        var cut = harness.Context.RenderComponent<ResourcesPage>();
        cut.Find("[data-testid='resource-project-select']").Change(savedProject.Value!.ToString());
        cut.Find("[data-testid='resource-name-input']").Change("Main repository");
        cut.Find("[data-testid='resource-primary-input']").Change("https://github.com/example/repo.git");
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Resource saved.", cut.Markup);
            Assert.Contains("Main repository", cut.Markup);
            Assert.Contains("https://github.com/example/repo.git", cut.Markup);
        });
    }

    [Fact]
    public async Task Saves_folder_resource_after_switching_connector_plugin()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var projectsService = harness.Context.Services.GetRequiredService<ProjectsService>();
        var resourcesService = harness.Context.Services.GetRequiredService<ResourcesService>();
        var project = await projectsService.GetAsync(null);
        project.Name = "Folder Resource Project";
        var savedProject = await projectsService.SaveAsync(project);
        Assert.True(savedProject.IsSuccess);

        var cut = harness.Context.RenderComponent<ResourcesPage>();
        cut.Find("[data-testid='resource-project-select']").Change(savedProject.Value!.ToString());
        cut.Find("[data-testid='resource-plugin-select']").Change(FolderConnectorPluginKey);
        cut.Find("[data-testid='resource-name-input']").Change("Working folder");
        cut.Find("[data-testid='resource-primary-input']").Change(@"C:\repositories\CanDoItAll\workspace");
        cut.Find("[data-testid='resource-save-button']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Resource saved.", cut.Markup);
            Assert.Contains("Working folder", cut.Markup);
            Assert.Contains(@"C:\repositories\CanDoItAll\workspace", cut.Markup);
        });

        var resources = await resourcesService.ListAsync();
        var resource = Assert.Single(resources, item => item.Name == "Working folder");
        Assert.Equal(FolderConnectorPluginKey, resource.ConnectorPluginKey);
    }
}


