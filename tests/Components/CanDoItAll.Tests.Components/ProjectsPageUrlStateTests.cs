using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectsPageUrlStateTests {
    [Fact]
    public async Task Preview_pushes_the_project_and_close_returns_to_the_board_through_history() {
        await using var fixture = await UrlFixture.CreateAsync();
        var id = await fixture.CreateProjectAsync("Preview target");
        var cut = fixture.Render("/projects?q=Preview");
        var card = Card(cut, "Preview target");
        var entries = fixture.Navigation.History.Count;

        await cut.InvokeAsync(() => card.QuerySelector("[data-testid='project-card-details-button']")!.ClickAsync(new()));

        cut.WaitForElement("[data-testid='projects-detail-modal']");
        Assert.Equal($"/projects?projectId={id:D}&q=Preview", fixture.Location);
        Assert.Equal(entries + 1, fixture.Navigation.History.Count);
        Assert.False(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);

        await cut.InvokeAsync(() => cut.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='projects-detail-modal']")));
        Assert.Equal(1, fixture.Back.Calls);
        Assert.Equal("/projects?q=Preview", fixture.Location);
    }

    [Fact]
    public async Task Deep_linked_editor_restores_its_step_and_sections_replace_the_entry() {
        await using var fixture = await UrlFixture.CreateAsync();
        var id = await fixture.CreateProjectAsync("Deep editor");
        var cut = fixture.Render($"/projects?projectId={id:D}&mode=editor&step=stack&q=Deep");

        cut.WaitForElement("[data-testid='projects-editor-modal']");
        Assert.Equal(2, cut.FindComponent<ProjectModalHost>().Instance.WizardStep);
        var entries = fixture.Navigation.History.Count;

        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Next").ClickAsync(new()));

        Assert.Equal($"/projects?projectId={id:D}&mode=editor&step=links&q=Deep", fixture.Location);
        await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Back to details").ClickAsync(new()));

        cut.WaitForElement("[data-testid='projects-detail-modal']");
        Assert.Equal($"/projects?projectId={id:D}&q=Deep", fixture.Location);
        Assert.Equal(entries, fixture.Navigation.History.Count);

        await cut.InvokeAsync(() => cut.Find("[data-testid='project-modal-close-button']").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='projects-detail-modal']")));
        Assert.Equal(0, fixture.Back.Calls);
        Assert.Equal("/projects?q=Deep", fixture.Location);
        Assert.Equal(entries, fixture.Navigation.History.Count);
    }

    [Fact]
    public async Task Filters_replace_the_location_reset_pushes_and_a_location_echo_does_not_reload_the_portfolio() {
        var reads = new ReadCounter();
        await using var fixture = await UrlFixture.CreateAsync(services => Decorate<IDbContextFactory<ProjectsDbContext>>(services,
            inner => new CountingFactory(inner, reads)));
        await fixture.CreateProjectAsync("Filtered project");
        var cut = fixture.Render("/projects");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='project-card']")));
        var loaded = reads.Count;
        var entries = fixture.Navigation.History.Count;
        var board = cut.FindComponent<ProjectsBoard>();

        await cut.InvokeAsync(() => board.Instance.StatusFilterChanged.InvokeAsync(ProjectStatus.Active));

        Assert.Equal("/projects?status=active", fixture.Location);
        Assert.Equal(entries, fixture.Navigation.History.Count);
        cut.WaitForAssertion(() => Assert.Equal(ProjectStatus.Active, cut.FindComponent<ProjectsBoard>().Instance.StatusFilter));

        await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.ResetFilters.InvokeAsync());

        Assert.Equal("/projects", fixture.Location);
        Assert.Equal(entries + 1, fixture.Navigation.History.Count);

        await cut.InvokeAsync(() => fixture.Navigation.NavigateTo("/projects?status=draft&scope=children"));

        cut.WaitForAssertion(() => {
            var current = cut.FindComponent<ProjectsBoard>().Instance;
            Assert.Equal(ProjectStatus.Draft, current.StatusFilter);
            Assert.Equal(ProjectHierarchyFilterMode.Children, current.HierarchyFilterMode);
        });
        Assert.Equal(loaded, reads.Count);
    }

    [Fact]
    public async Task Portfolio_view_is_addressable_and_a_location_change_restores_the_cards() {
        await using var fixture = await UrlFixture.CreateAsync();
        await fixture.CreateProjectAsync("Viewed project");
        var cut = fixture.Render("/projects");
        cut.WaitForElement("[data-testid='project-card']");
        var entries = fixture.Navigation.History.Count;

        await cut.InvokeAsync(() => PortfolioTab(cut, "Files").ClickAsync(new()));

        Assert.Equal("/projects?view=files", fixture.Location);
        Assert.Equal(entries + 1, fixture.Navigation.History.Count);
        Assert.Equal(ProjectsAgentChatView.Files, cut.FindComponent<ProjectsBoard>().Instance.PortfolioView);

        await cut.InvokeAsync(() => fixture.Navigation.NavigateTo("/projects"));

        cut.WaitForAssertion(() => {
            Assert.Equal(ProjectsAgentChatView.Cards, cut.FindComponent<ProjectsBoard>().Instance.PortfolioView);
            Assert.Equal("true", PortfolioTab(cut, "Cards").GetAttribute("aria-selected"));
        });
    }

    [Fact]
    public async Task Dashboard_command_navigates_with_the_dedicated_workbench_tab_marker() {
        await using var fixture = await UrlFixture.CreateAsync();
        var id = await fixture.CreateProjectAsync("Dashboard project");
        var cut = fixture.Render("/projects?status=draft");
        var card = Card(cut, "Dashboard project");

        await cut.InvokeAsync(() => card.QuerySelector("button[aria-label='Open dashboard tab']")!.ClickAsync(new()));

        Assert.Equal($"/projects?projectId={id:D}", fixture.Location);
        Assert.Equal(WorkbenchNavigation.OpenTabHistoryState, fixture.Navigation.History.First().Options.HistoryEntryState);
        cut.WaitForElement("[data-testid='projects-detail-modal']");
    }

    [Fact]
    public async Task First_save_of_a_new_project_addresses_its_editor_without_a_new_entry() {
        await using var fixture = await UrlFixture.CreateAsync();
        var projects = fixture.Harness.Context.Services.GetRequiredService<ProjectsService>();
        var cut = fixture.Render("/projects");
        var entries = fixture.Navigation.History.Count;

        await cut.InvokeAsync(() => cut.Find("[data-testid='projects-new-button']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='project-name-input']");
        Assert.Equal("/projects", fixture.Location);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-name-input']").InputAsync(new() { Value = "Addressed draft" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-save-button']").ClickAsync(new()));

        var saved = Assert.Single(await projects.ListAsync());
        cut.WaitForAssertion(() => Assert.Equal($"/projects?projectId={saved.Id:D}&mode=editor", fixture.Location));
        Assert.Equal(entries, fixture.Navigation.History.Count);
        Assert.NotEmpty(cut.FindAll("[data-testid='projects-editor-modal']"));
    }

    [Fact]
    public async Task Hierarchy_drill_down_replaces_the_dialog_and_close_returns_to_the_board() {
        await using var fixture = await UrlFixture.CreateAsync();
        var projects = fixture.Harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = await fixture.CreateProjectAsync("Hierarchy root");
        var child = await fixture.CreateProjectAsync("Hierarchy child");
        Assert.True((await projects.AddSubprojectAsync(parent, child)).IsSuccess);
        var cut = fixture.Render("/projects");
        var card = Card(cut, "Hierarchy root");
        var entries = fixture.Navigation.History.Count;

        await cut.InvokeAsync(() => card.QuerySelector("[data-testid='project-card-subprojects-button']")!.ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Contains("Hierarchy child", cut.Find("[data-testid='projects-hierarchy-modal']").TextContent));
        Assert.Equal($"/projects?hierarchyId={parent:D}", fixture.Location);
        Assert.Equal(entries + 1, fixture.Navigation.History.Count);
        var childCard = cut.FindAll("[data-testid='hierarchy-subproject-card']")
            .Single(item => item.TextContent.Contains("Hierarchy child", StringComparison.Ordinal));

        await cut.InvokeAsync(() => childCard.QuerySelector("[data-testid='hierarchy-card-subprojects-button']")!.ClickAsync(new()));

        Assert.Equal($"/projects?hierarchyId={child:D}", fixture.Location);
        Assert.Equal(entries + 1, fixture.Navigation.History.Count);

        await cut.InvokeAsync(() => cut.Find("[data-testid='projects-hierarchy-modal']").QuerySelectorAll("button")
            .Single(button => button.TextContent.Trim() == "Close").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='projects-hierarchy-modal']")));
        Assert.Equal(1, fixture.Back.Calls);
        Assert.Equal("/projects", fixture.Location);
    }

    [Fact]
    public async Task Unknown_project_link_reports_the_project_as_unavailable_instead_of_opening_another_record() {
        await using var fixture = await UrlFixture.CreateAsync();
        await fixture.CreateProjectAsync("Existing project");
        var missing = Guid.NewGuid();

        var cut = fixture.Render($"/projects?projectId={missing:D}");

        cut.WaitForAssertion(() => Assert.Contains($"Unable to acquire project {missing:D}", cut.Find("[data-testid='projects-read-error']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='projects-detail-modal']"));
        Assert.Equal($"/projects?projectId={missing:D}", fixture.Location);
    }

    private static AngleSharp.Dom.IElement Card(IRenderedComponent<ProjectsPage> cut, string name) {
        cut.WaitForAssertion(() => Assert.Contains(cut.FindAll("[data-testid='project-card']"), card => card.TextContent.Contains(name, StringComparison.Ordinal)));
        return cut.FindAll("[data-testid='project-card']").Single(card => card.TextContent.Contains(name, StringComparison.Ordinal));
    }

    private static AngleSharp.Dom.IElement PortfolioTab(IRenderedComponent<ProjectsPage> cut, string label)
        => cut.Find("[data-testid='projects-portfolio-tabs']").QuerySelectorAll(".cad-tabs__tab")
            .Single(tab => tab.TextContent.Contains(label, StringComparison.Ordinal));

    private static void Decorate<T>(IServiceCollection services, Func<T, T> decorate) where T : class {
        var descriptor = services.Last(item => item.ServiceType == typeof(T));
        services.RemoveAll<T>();
        services.Add(ServiceDescriptor.Describe(typeof(T), provider => decorate((T)(descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!))), descriptor.Lifetime));
    }

    private sealed class ReadCounter {
        private int count;
        public int Count => Volatile.Read(ref count);
        public void Increment() => Interlocked.Increment(ref count);
    }

    private sealed class CountingFactory(IDbContextFactory<ProjectsDbContext> inner, ReadCounter reads) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => inner.CreateDbContext();

        public Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            reads.Increment();
            return inner.CreateDbContextAsync(cancellationToken);
        }
    }

    private sealed class UrlFixture : IAsyncDisposable {
        private UrlFixture(ComponentTestHarness harness, ScriptedBack back) {
            Harness = harness;
            Back = back;
            Navigation = Assert.IsType<BunitNavigationManager>(harness.Context.Services.GetRequiredService<NavigationManager>());
            back.Navigation = Navigation;
        }

        public ComponentTestHarness Harness { get; }
        public ScriptedBack Back { get; }
        public BunitNavigationManager Navigation { get; }
        public string Location => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

        public static async Task<UrlFixture> CreateAsync(Action<IServiceCollection>? configure = null) {
            var back = new ScriptedBack();
            var harness = await ComponentTestHarness.CreateAsync(services => {
                configure?.Invoke(services);
                services.AddSingleton<IAppNavigationHistory>(back);
            });
            return new(harness, back);
        }

        public async Task<Guid> CreateProjectAsync(string name) {
            var result = await Harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
                Name = name,
                Description = $"{name} description",
                Objective = $"{name} objective",
                CurrentPhase = "Discovery"
            });
            Assert.True(result.IsSuccess);
            return result.Value;
        }

        public IRenderedComponent<ProjectsPage> Render(string location) {
            Navigation.NavigateTo(location);
            return Harness.Context.Render<ProjectsPage>();
        }

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
    }

    // Browser Back: the previous entry becomes the location again, with the entry state it was created with.
    private sealed class ScriptedBack : IAppNavigationHistory {
        public BunitNavigationManager? Navigation { get; set; }
        public int Calls { get; private set; }

        public ValueTask BackAsync(CancellationToken cancellationToken = default) {
            Calls++;
            var navigation = Navigation ?? throw new InvalidOperationException("The navigation manager is not attached.");
            var previous = navigation.History.Skip(1).FirstOrDefault();
            navigation.NavigateTo(previous?.Uri ?? navigation.BaseUri, new NavigationOptions { HistoryEntryState = previous?.Options.HistoryEntryState });
            return ValueTask.CompletedTask;
        }
    }
}
