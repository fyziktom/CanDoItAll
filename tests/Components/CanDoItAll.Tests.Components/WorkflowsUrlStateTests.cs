using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowsUrlStateTests {
    private const string Path = "/agents/workflows";

    [Fact]
    public async Task Tabs_runs_and_definitions_push_entries_and_a_run_link_lands_on_history() {
        await using var fixture = await UrlFixture.CreateAsync();
        fixture.Navigation.NavigateTo($"{Path}?workflowId={fixture.First.Id.Value:D}");
        fixture.Render();

        await fixture.Tab(WorkflowTab.History);
        fixture.Cut.WaitForAssertion(() => Assert.NotNull(fixture.Surface.History.SelectedRun));
        Assert.Equal($"{Path}?tab=history&workflowId={fixture.First.Id.Value:D}", fixture.Location);

        await fixture.Emit(WorkflowAction.SelectRun, fixture.FirstRun.RunId.Value);
        var runLocation = $"{Path}?workflowId={fixture.First.Id.Value:D}&runId={fixture.FirstRun.RunId.Value:D}";
        Assert.Equal(runLocation, fixture.Location);

        await fixture.Select(fixture.Second);
        fixture.Cut.WaitForAssertion(() => Assert.Equal(fixture.Second.Id.Value, fixture.Surface.Catalog.Definition?.Id));
        Assert.Equal($"{Path}?tab=history&workflowId={fixture.Second.Id.Value:D}", fixture.Location);
        Assert.Equal(4, fixture.Navigation.History.Count);
        Assert.All(fixture.Navigation.History, entry => Assert.False(entry.Options.ReplaceHistoryEntry));

        fixture.Navigation.NavigateTo(runLocation);
        fixture.Cut.WaitForAssertion(() => {
            Assert.Equal(WorkflowTab.History, fixture.Surface.Presentation.ActiveTab);
            Assert.Equal(fixture.First.Id.Value, fixture.Surface.Catalog.Definition?.Id);
            Assert.Equal(fixture.FirstRun.RunId.Value, fixture.Surface.History.SelectedRun?.Id);
        });
    }

    [Fact]
    public async Task Tab_only_back_and_forward_switch_the_tab_without_reloading_the_page() {
        await using var fixture = await UrlFixture.CreateAsync();
        fixture.Navigation.NavigateTo($"{Path}?workflowId={fixture.First.Id.Value:D}");
        fixture.Render();
        await fixture.Tab(WorkflowTab.History);
        var history = fixture.Location;
        fixture.Cut.WaitForAssertion(() => Assert.NotNull(fixture.Surface.History.SelectedRun));
        var reads = fixture.CatalogReads;

        fixture.Navigation.NavigateTo($"{Path}?workflowId={fixture.First.Id.Value:D}");
        fixture.Cut.WaitForAssertion(() => Assert.Equal(WorkflowTab.Dashboard, fixture.Surface.Presentation.ActiveTab));
        fixture.Navigation.NavigateTo(history);
        fixture.Cut.WaitForAssertion(() => Assert.Equal(WorkflowTab.History, fixture.Surface.Presentation.ActiveTab));

        Assert.Equal($"{Path}?tab=history&workflowId={fixture.First.Id.Value:D}", history);
        Assert.Equal(reads, fixture.CatalogReads);
        Assert.Equal(fixture.First.Id.Value, fixture.Surface.Catalog.Definition?.Id);
        Assert.Equal(fixture.FirstRun.RunId.Value, fixture.Surface.History.SelectedRun?.Id);
    }

    [Fact]
    public async Task Run_details_are_an_overlay_that_closes_back_and_reopens_from_its_link() {
        await using var fixture = await UrlFixture.CreateAsync();
        var runLocation = $"{Path}?workflowId={fixture.First.Id.Value:D}&runId={fixture.FirstRun.RunId.Value:D}";
        fixture.Navigation.NavigateTo(runLocation);
        fixture.Render();
        fixture.Cut.WaitForAssertion(() => Assert.Equal(fixture.FirstRun.RunId.Value, fixture.Surface.History.SelectedRun?.Id));
        Assert.Equal(WorkflowTab.History, fixture.Surface.Presentation.ActiveTab);

        await fixture.Emit(WorkflowAction.RunDetails, fixture.FirstRun.RunId.Value);
        fixture.Cut.WaitForElement("[data-testid='workflows-run-detail-dialog']");
        var opened = fixture.Navigation.History.First();
        Assert.Equal(runLocation + "&dialog=run-details", fixture.Location);
        Assert.Equal("cda-nav-parent:" + runLocation, opened.Options.HistoryEntryState);

        await fixture.Cut.InvokeAsync(() => fixture.Cut.Find("[data-testid='workflows-run-detail-dialog'] [aria-label='Close']").ClickAsync(new()));
        fixture.Cut.WaitForAssertion(() => Assert.Equal(runLocation, fixture.Location));
        Assert.Equal(1, fixture.Back.BackCount);
        Assert.Empty(fixture.Cut.FindAll("[data-testid='workflows-run-detail-dialog']"));

        fixture.Navigation.NavigateTo(runLocation + "&dialog=run-details");
        fixture.Cut.WaitForElement("[data-testid='workflows-run-detail-dialog']");
        fixture.Navigation.NavigateTo(runLocation);
        fixture.Cut.WaitForAssertion(() => Assert.Empty(fixture.Cut.FindAll("[data-testid='workflows-run-detail-dialog']")));
        Assert.Equal(1, fixture.Back.BackCount);
    }

    [Fact]
    public async Task A_deep_linked_run_detail_opens_with_the_page_and_closing_replaces_the_location() {
        await using var fixture = await UrlFixture.CreateAsync();
        var runLocation = $"{Path}?workflowId={fixture.First.Id.Value:D}&runId={fixture.FirstRun.RunId.Value:D}";
        fixture.Navigation.NavigateTo(runLocation + "&dialog=run-details");
        fixture.Render();

        fixture.Cut.WaitForElement("[data-testid='workflows-run-detail-dialog']");
        await fixture.Cut.InvokeAsync(() => fixture.Cut.Find("[data-testid='workflows-run-detail-dialog'] [aria-label='Close']").ClickAsync(new()));

        fixture.Cut.WaitForAssertion(() => Assert.Equal(runLocation, fixture.Location));
        Assert.Single(fixture.Navigation.History);
        Assert.True(fixture.Navigation.History.First().Options.ReplaceHistoryEntry);
        Assert.Equal(0, fixture.Back.BackCount);
    }

    private sealed class UrlFixture : IAsyncDisposable {
        private IRenderedComponent<WorkflowsPage>? cut;

        private UrlFixture(CanDoItAllTestEnvironment environment, ComponentTestHarness harness, CountingCatalog catalog) {
            Environment = environment;
            Harness = harness;
            Back = (TestNavigationHistory)harness.Context.Services.GetRequiredService<IAppNavigationHistory>();
            Catalog = catalog;
            Navigation = (BunitNavigationManager)harness.Context.Services.GetRequiredService<NavigationManager>();
        }

        public CanDoItAllTestEnvironment Environment { get; }
        public ComponentTestHarness Harness { get; }
        public TestNavigationHistory Back { get; }
        private CountingCatalog Catalog { get; }
        public BunitNavigationManager Navigation { get; }
        public WorkflowDefinition First { get; private set; } = default!;
        public WorkflowDefinition Second { get; private set; } = default!;
        public WorkflowRunSnapshot FirstRun { get; private set; } = default!;
        public IRenderedComponent<WorkflowsPage> Cut => cut ?? throw new InvalidOperationException("The page is not rendered.");
        public WorkflowShellSurface Surface => Cut.FindComponent<WorkflowShellSurface>().Instance;
        public string Location => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);
        public int CatalogReads => Catalog.Reads;

        public static async Task<UrlFixture> CreateAsync() {
            var environment = CanDoItAllTestEnvironment.Create("workflow-url-state");
            var catalog = new CountingCatalog();
            var harness = await WorkflowsPageTests.CreateInMemoryWorkflowHarnessAsync(environment, services => {
                var descriptor = services.Last(item => item.ServiceType == typeof(IWorkflowCatalogService));
                services.AddScoped<IWorkflowCatalogService>(provider => catalog.Wrap(Resolve(provider, descriptor)));
                services.AddScoped<IAppNavigationHistory, TestNavigationHistory>();
            });
            var fixture = new UrlFixture(environment, harness, catalog);
            var service = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
            fixture.First = await WorkflowsPageTests.CreateHistoryDefinitionAsync(service);
            fixture.Second = await WorkflowsPageTests.CreateHistoryDefinitionAsync(service);
            var store = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
            fixture.FirstRun = new(new(Guid.NewGuid()), fixture.First.Id, fixture.First.VersionId, WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess, "url-run", "Completed URL run", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            await store.SaveRunAsync(fixture.FirstRun);
            return fixture;
        }

        public void Render() {
            cut = Harness.Context.Render<WorkflowsPage>();
            cut.WaitForAssertion(() => Assert.False(Surface.Presentation.IsLoading), TimeSpan.FromSeconds(10));
        }

        public Task Emit(WorkflowAction action, Guid? id = null)
            => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(Surface.Presentation.Revision, action, id)));

        public Task Select(WorkflowDefinition definition) => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(
            Surface.Presentation.Revision, WorkflowAction.SelectDefinition,
            TreeKey: new(WorkflowDefinitionTreeNodeBuilder.BuildDefinitionNodeId(definition.Id)))));

        public Task Tab(WorkflowTab tab) => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(
            Surface.Presentation.Revision, WorkflowAction.ChangeTab, Tab: tab)));

        public async ValueTask DisposeAsync() {
            await Harness.DisposeAsync();
            await Environment.DisposeAsync();
        }

        private static IWorkflowCatalogService Resolve(IServiceProvider provider, ServiceDescriptor descriptor)
            => (IWorkflowCatalogService)(descriptor.ImplementationFactory?.Invoke(provider)
                ?? descriptor.ImplementationInstance
                ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!));
    }

    private sealed class CountingCatalog {
        public int Reads { get; private set; }

        public IWorkflowCatalogService Wrap(IWorkflowCatalogService inner) {
            var proxy = System.Reflection.DispatchProxy.Create<IWorkflowCatalogService, CatalogProxy>();
            var recorder = (CatalogProxy)(object)proxy;
            recorder.Inner = inner;
            recorder.Listed = () => Reads++;
            return proxy;
        }
    }

    public class CatalogProxy : System.Reflection.DispatchProxy {
        public IWorkflowCatalogService Inner { get; set; } = default!;
        public Action Listed { get; set; } = default!;

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod!.Name == nameof(IWorkflowCatalogService.ListDefinitionsAsync)) {
                Listed();
            }
            return targetMethod.Invoke(Inner, args);
        }
    }

}
