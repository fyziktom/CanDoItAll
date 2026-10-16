using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;
using CanDoItAll.Resources.UI;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

[Trait("Category", "HostPlatform")]
public sealed class ResourcesPageUrlStateTests {
    [Fact]
    public async Task Selecting_a_resource_pushes_it_and_keeps_the_project_seed_only_for_its_own_project() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var seed = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Seed project");
        var other = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Other project");
        var seeded = await SaveResourceAsync(services, seed, "Seeded resource");
        var foreign = await SaveResourceAsync(services, other, "Foreign resource");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/resources?projectId={seed.ProjectId:D}");
        var cut = harness.Context.Render<ResourcesPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".cda-selection-list-item__button").Count));
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => Item(cut, "Seeded resource").ClickAsync(new()));

        Assert.Equal($"/resources?resourceId={seeded:D}&projectId={seed.ProjectId:D}", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);
        cut.WaitForAssertion(() => Assert.Equal("Seeded resource", cut.Instance.Registry.Draft.Editor.Name));

        await cut.InvokeAsync(() => Item(cut, "Foreign resource").ClickAsync(new()));

        Assert.Equal($"/resources?resourceId={foreign:D}", Location(navigation));
        Assert.Equal(entries + 2, navigation.History.Count);
        cut.WaitForAssertion(() => Assert.Equal("Foreign resource", cut.Instance.Registry.Draft.Editor.Name));

        await cut.InvokeAsync(() => navigation.NavigateTo($"/resources?resourceId={seeded:D}&projectId={seed.ProjectId:D}"));

        cut.WaitForAssertion(() => Assert.Equal("Seeded resource", cut.Instance.Registry.Draft.Editor.Name));
    }

    [Fact]
    public async Task Browse_tab_is_addressable_filters_replace_and_reset_pushes() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var project = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Filtered resources project");
        await SaveResourceAsync(services, project, "Filtered resource");
        var navigation = Navigation(harness);
        navigation.NavigateTo("/resources");
        var cut = harness.Context.Render<ResourcesPage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".cda-selection-list-item__button")));
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => cut.Find("[data-testid='resources-tab-browse']").ClickAsync(new()));

        Assert.Equal("/resources?view=browse", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);

        await cut.InvokeAsync(() => navigation.NavigateTo("/resources"));

        cut.WaitForAssertion(() => Assert.Equal(0, cut.FindComponent<ResourcesWorkspaceSurface>().Instance.SelectedTab));
        entries = navigation.History.Count;
        await cut.InvokeAsync(() => cut.Instance.Registry.ProjectFilter = project.ProjectId);

        Assert.Equal($"/resources?project={project.ProjectId:D}", Location(navigation));
        Assert.Equal(entries, navigation.History.Count);

        await cut.InvokeAsync(() => cut.Instance.Registry.Search = "Filtered");

        cut.WaitForAssertion(() => Assert.Equal($"/resources?q=Filtered&project={project.ProjectId:D}", Location(navigation)), TimeSpan.FromSeconds(5));
        Assert.Equal(entries, navigation.History.Count);

        await cut.InvokeAsync(() => cut.Instance.Registry.ResetFilters());

        Assert.Equal("/resources", Location(navigation));
        Assert.Equal(entries + 1, navigation.History.Count);

        await cut.InvokeAsync(() => navigation.NavigateTo($"/resources?q=Missing&validation=invalid"));

        cut.WaitForAssertion(() => {
            Assert.Equal("Missing", cut.Instance.Registry.Search);
            Assert.Equal(ResourceValidationStatus.Invalid, cut.Instance.Registry.ValidationFilter);
            Assert.Empty(cut.Instance.Registry.FilteredResources);
        });
    }

    [Fact]
    public async Task Saving_a_new_resource_replaces_the_location_with_its_identity_and_deleting_it_removes_it() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var services = harness.Context.Services;
        var project = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Created resource project");
        var navigation = Navigation(harness);
        navigation.NavigateTo($"/resources?projectId={project.ProjectId:D}");
        var cut = harness.Context.Render<ResourcesPage>();
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Registry.CanMutate));
        var entries = navigation.History.Count;

        await cut.InvokeAsync(() => cut.Instance.Registry.ChangeConnectorAsync(ResourceConnectorPluginKeys.WebLink));
        await cut.InvokeAsync(() => {
            var editor = cut.Instance.Registry.Draft.Editor;
            editor.Name = "Created resource";
            editor.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://fixture.test/created");
            return cut.Instance.Registry.SaveAsync();
        });

        var created = cut.Instance.Registry.Draft.Editor.Id;
        Assert.NotNull(created);
        cut.WaitForAssertion(() => Assert.Equal($"/resources?resourceId={created:D}&projectId={project.ProjectId:D}", Location(navigation)));
        Assert.Equal(entries, navigation.History.Count);

        await cut.InvokeAsync(() => cut.Instance.Registry.DeleteAsync());

        cut.WaitForAssertion(() => Assert.Equal($"/resources?projectId={project.ProjectId:D}", Location(navigation)));
        Assert.Equal(entries, navigation.History.Count);
        Assert.Null(cut.Instance.Registry.Draft.Editor.Id);
    }

    private static async Task<Guid> SaveResourceAsync(IServiceProvider services, ProjectWriteAdmission project, string name) {
        var resources = services.GetRequiredService<ResourcesService>();
        var model = new ResourceEditorModel {
            ProjectId = project.ProjectId,
            ExpectedProjectAdmission = project,
            Name = name,
            ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink,
            ConfigSchemaVersion = resources.ListConnectorManifests().Single(m => m.PluginKey == ResourceConnectorPluginKeys.WebLink).ConfigurationSchema.Version
        };
        model.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, $"https://fixture.test/{Guid.NewGuid():N}");
        var result = await resources.SaveAsync(model);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static AngleSharp.Dom.IElement Item(IRenderedComponent<ResourcesPage> cut, string name)
        => cut.FindAll(".cda-selection-list-item__button").Single(item => item.TextContent.Contains(name, StringComparison.Ordinal));

    private static BunitNavigationManager Navigation(ComponentTestHarness harness)
        => Assert.IsType<BunitNavigationManager>(harness.Context.Services.GetRequiredService<NavigationManager>());

    private static string Location(NavigationManager navigation) => "/" + navigation.ToBaseRelativePath(navigation.Uri);
}
