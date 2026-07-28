using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class WorkflowOverviewPanelTests
{
    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Panel_loads_lazily_and_renders_global_workflow_signals()
    {
        using var context = CreateContext();
        var workflows = CreateWorkflows();
        var queryService = new RecordingOverviewQueryService(CreateSnapshot(workflows));
        var cut = context.Render<WorkflowOverviewPanel>(parameters => parameters
            .Add(component => component.QueryService, queryService)
            .Add(component => component.IsActive, false));

        Assert.Empty(queryService.Queries);
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-overview-inactive']"));

        cut.Render(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.WaitForElement("[data-testid='workflow-overview-content']");
        var query = Assert.Single(queryService.Queries);
        Assert.Equal(6, query.RecentTake);
        Assert.Equal(5, query.TopWorkflowTake);
        Assert.Contains("2", cut.Find("[data-testid='workflow-overview-definition-count']").TextContent, StringComparison.Ordinal);
        Assert.Contains("12", cut.Find("[data-testid='workflow-overview-run-count']").TextContent, StringComparison.Ordinal);
        Assert.Contains("80.0%", cut.Find("[data-testid='workflow-overview-success-rate']").TextContent, StringComparison.Ordinal);
        Assert.Single(cut.FindAll("[data-testid='workflow-overview-top-workflow-row']"));
        Assert.Equal(2, cut.FindAll("[data-testid='workflow-overview-recent-run-row']").Count);
        Assert.Equal(2, cut.FindAll("[data-testid='workflow-overview-definition-card']").Count);
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-overview-run-state-chart']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='workflow-overview-backend-chart']"));
    }

    [Fact]
    public void Panel_requeries_only_when_active_refresh_version_changes()
    {
        using var context = CreateContext();
        var workflows = CreateWorkflows();
        var queryService = new RecordingOverviewQueryService(CreateSnapshot(workflows));
        var cut = context.Render<WorkflowOverviewPanel>(parameters => parameters
            .Add(component => component.QueryService, queryService)
            .Add(component => component.IsActive, true)
            .Add(component => component.RefreshVersion, 1));
        cut.WaitForAssertion(() => Assert.Single(queryService.Queries));

        cut.Render(parameters => parameters
            .Add(component => component.IsActive, false)
            .Add(component => component.RefreshVersion, 2));
        Assert.Single(queryService.Queries);

        cut.Render(parameters => parameters
            .Add(component => component.IsActive, true));
        cut.WaitForAssertion(() => Assert.Equal(2, queryService.Queries.Count));
    }

    [Fact]
    public void Panel_refresh_reflects_definition_status_count_and_name_changes()
    {
        using var context = CreateContext();
        var initialDefinition = CreateWorkflows()[1];
        IReadOnlyList<WorkflowCatalogItem> refreshedDefinitions =
        [
            initialDefinition with
            {
                Name = "Priority mail routing",
                Status = WorkflowLifecycleStatus.Active,
                UpdatedAtUtc = AsOfUtc.AddMinutes(1)
            },
            CreateWorkflows()[0] with
            {
                UpdatedAtUtc = AsOfUtc.AddMinutes(2)
            }
        ];
        var queryService = new RecordingOverviewQueryService(CreateDefinitionSnapshot([initialDefinition]));
        var cut = context.Render<WorkflowOverviewPanel>(parameters => parameters
            .Add(component => component.QueryService, queryService)
            .Add(component => component.IsActive, true));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("1", cut.Find("[data-testid='workflow-overview-definition-count']").TextContent, StringComparison.Ordinal);
            Assert.Contains("0", cut.Find("[data-testid='workflow-overview-active-count']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Mail triage", cut.Find("[data-testid='workflow-overview-recent-definitions']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Draft", cut.Find("[data-testid='workflow-overview-recent-definitions']").TextContent, StringComparison.Ordinal);
        });

        queryService.Snapshot = CreateDefinitionSnapshot(refreshedDefinitions);
        cut.Find("[data-testid='workflow-overview-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, queryService.Queries.Count);
            Assert.Contains("2", cut.Find("[data-testid='workflow-overview-definition-count']").TextContent, StringComparison.Ordinal);
            Assert.Contains("2", cut.Find("[data-testid='workflow-overview-active-count']").TextContent, StringComparison.Ordinal);
            Assert.Equal(2, cut.FindAll("[data-testid='workflow-overview-definition-card']").Count);
            Assert.Contains("Priority mail routing", cut.Find("[data-testid='workflow-overview-recent-definitions']").TextContent, StringComparison.Ordinal);
            Assert.Contains("Active", cut.Find("[data-testid='workflow-overview-recent-definitions']").TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("Mail triage", cut.Find("[data-testid='workflow-overview-recent-definitions']").TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Workflows_dashboard_uses_dedicated_typed_overview_panel()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageMarkup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "WorkflowsPage.razor"));
        var pageCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "WorkflowsPage.razor.cs"));
        var dashboardTab = Slice(pageMarkup, "<TabsItem Text=\"Dashboard\"", "</TabsItem>");

        Assert.Contains("WorkflowOverviewPanel", dashboardTab, StringComparison.Ordinal);
        Assert.Contains("QueryService=\"OverviewQueryService\"", dashboardTab, StringComparison.Ordinal);
        Assert.DoesNotContain("Workflows=", dashboardTab, StringComparison.Ordinal);
        Assert.Contains("IsActive=\"@(activeWorkflowTabIndex == DashboardTabIndex)\"", dashboardTab, StringComparison.Ordinal);
        Assert.Contains("RefreshVersion=\"analyticsRefreshVersion\"", dashboardTab, StringComparison.Ordinal);
        Assert.DoesNotContain("ListRunsAsync", dashboardTab, StringComparison.Ordinal);
        Assert.Contains("IWorkflowOverviewQueryService OverviewQueryService", pageCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflows_page_dashboard_executes_persistent_bounded_projection()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-overview-dashboard");
        var profile = environment.CreateInMemoryProfile("primary");
        await using var harness = await ComponentTestHarness.CreateAsync(options: new TestHarnessOptions
        {
            TestEnvironment = environment,
            ActiveProfile = profile,
            SchemaModules = TestSchemaBootstrapModules.Default
        });
        var runStore = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
        var workflow = CreateWorkflows()[0];
        await runStore.SaveRunAsync(CreateRun(workflow, WorkflowRunState.Completed, AsOfUtc.AddMinutes(-2)));
        await runStore.SaveRunAsync(CreateRun(workflow, WorkflowRunState.Failed, AsOfUtc.AddMinutes(-1)));
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/agents/workflows");

        var cut = harness.Context.Render<WorkflowsPage>();

        cut.WaitForElement("[data-testid='workflow-overview-content']", TimeSpan.FromSeconds(30));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(
                "2",
                cut.Find("[data-testid='workflow-overview-run-count']").TextContent,
                StringComparison.Ordinal);
            Assert.Equal(2, cut.FindAll("[data-testid='workflow-overview-recent-run-row']").Count);
        });
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        return context;
    }

    private static IReadOnlyList<WorkflowCatalogItem> CreateWorkflows()
        =>
        [
            new WorkflowCatalogItem(
                new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
                WorkflowVersionId.New(),
                "Invoice routing",
                "Route invoices through policy checks.",
                WorkflowLifecycleStatus.Active,
                WorkflowRuntimeBackendKind.InProcess,
                AsOfUtc.AddMinutes(-2)),
            new WorkflowCatalogItem(
                new WorkflowId(Guid.Parse("30000000-0000-0000-0000-000000000002")),
                WorkflowVersionId.New(),
                "Mail triage",
                "Classify incoming mail.",
                WorkflowLifecycleStatus.Draft,
                WorkflowRuntimeBackendKind.DurableTask,
                AsOfUtc.AddMinutes(-1))
        ];

    private static WorkflowOverviewSnapshot CreateSnapshot(IReadOnlyList<WorkflowCatalogItem> workflows)
    {
        var completedRun = CreateRun(workflows[0], WorkflowRunState.Completed, AsOfUtc.AddMinutes(-3));
        var failedRun = CreateRun(workflows[1], WorkflowRunState.Failed, AsOfUtc.AddMinutes(-4));
        return new WorkflowOverviewSnapshot(
            AsOfUtc,
            DefinitionCount: 2,
            ActiveDefinitionCount: 1,
            RunCount: 12,
            RunningRunCount: 1,
            WaitingForInputRunCount: 1,
            CompletedRunCount: 8,
            FailedRunCount: 2,
            SuccessRatePercent: 80m,
            DefinitionsByStatus: new Dictionary<WorkflowLifecycleStatus, int>
            {
                [WorkflowLifecycleStatus.Active] = 1,
                [WorkflowLifecycleStatus.Draft] = 1
            },
            RunsByState: new Dictionary<WorkflowRunState, int>
            {
                [WorkflowRunState.Running] = 1,
                [WorkflowRunState.WaitingForInput] = 1,
                [WorkflowRunState.Completed] = 8,
                [WorkflowRunState.Failed] = 2
            },
            RunsByBackend: new Dictionary<WorkflowRuntimeBackendKind, int>
            {
                [WorkflowRuntimeBackendKind.InProcess] = 8,
                [WorkflowRuntimeBackendKind.DurableTask] = 4
            },
            TopWorkflows:
            [
                new WorkflowOverviewWorkflowRow(
                    workflows[0].Id,
                    workflows[0].Name,
                    workflows[0].Status,
                    RunCount: 8,
                    FailedRunCount: 1,
                    LastRunAtUtc: completedRun.UpdatedAtUtc)
            ],
            RecentlyUpdatedDefinitions: workflows.Reverse().ToArray(),
            RecentRuns:
            [
                new WorkflowOverviewRecentRunRow(completedRun, workflows[0].Name),
                new WorkflowOverviewRecentRunRow(failedRun, workflows[1].Name)
            ]);
    }

    private static WorkflowOverviewSnapshot CreateDefinitionSnapshot(IReadOnlyList<WorkflowCatalogItem> workflows)
        => new(
            AsOfUtc,
            DefinitionCount: workflows.Count,
            ActiveDefinitionCount: workflows.Count(definition => definition.Status == WorkflowLifecycleStatus.Active),
            RunCount: 0,
            RunningRunCount: 0,
            WaitingForInputRunCount: 0,
            CompletedRunCount: 0,
            FailedRunCount: 0,
            SuccessRatePercent: null,
            DefinitionsByStatus: workflows
                .GroupBy(definition => definition.Status)
                .ToDictionary(group => group.Key, group => group.Count()),
            RunsByState: new Dictionary<WorkflowRunState, int>(),
            RunsByBackend: new Dictionary<WorkflowRuntimeBackendKind, int>(),
            TopWorkflows: [],
            RecentlyUpdatedDefinitions: workflows,
            RecentRuns: []);

    private static WorkflowRunSnapshot CreateRun(
        WorkflowCatalogItem workflow,
        WorkflowRunState state,
        DateTimeOffset updatedAtUtc)
        => new(
            WorkflowRunId.New(),
            workflow.Id,
            workflow.VersionId,
            state,
            workflow.PreferredBackend,
            $"backend-{Guid.NewGuid():N}",
            $"{workflow.Name} {state}",
            updatedAtUtc.AddMinutes(-1),
            updatedAtUtc);

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{startMarker}' was not found.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Marker '{endMarker}' was not found after '{startMarker}'.");
        return source[start..(end + endMarker.Length)];
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class RecordingOverviewQueryService(WorkflowOverviewSnapshot snapshot) : IWorkflowOverviewQueryService
    {
        public List<WorkflowOverviewQuery> Queries { get; } = [];

        public WorkflowOverviewSnapshot Snapshot { get; set; } = snapshot;

        public Task<WorkflowOverviewSnapshot> QueryAsync(
            WorkflowOverviewQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(Snapshot);
        }
    }
}
