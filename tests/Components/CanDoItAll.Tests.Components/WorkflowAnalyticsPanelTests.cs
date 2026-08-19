using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowAnalyticsPanelTests
{
    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 12, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Panel_loads_lazily_and_uses_typed_all_then_selected_workflow_scope()
    {
        using var context = CreateContext();
        var queryService = new RecordingWorkflowAnalyticsQueryService(CreateSnapshot());
        var workflows = CreateWorkflowOptions();
        var cut = context.Render<WorkflowAnalyticsPanel>(parameters => parameters
            .Add(component => component.QueryService, queryService)
            .Add(component => component.Workflows, workflows)
            .Add(component => component.IsActive, false));

        Assert.Empty(queryService.Queries);

        cut.Render(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.WaitForAssertion(() =>
        {
            var query = Assert.Single(queryService.Queries);
            Assert.Null(query.WorkflowId);
            Assert.Equal(8, query.RecentTake);
        });

        cut.Find("[data-testid='workflow-analytics-scope']").Change("SelectedWorkflow");

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, queryService.Queries.Count);
            Assert.Equal(workflows[0].Id, queryService.Queries[^1].WorkflowId);
        });

        cut.Find("[data-testid='workflow-analytics-workflow']").Change(workflows[1].Id.Value.ToString("D"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(3, queryService.Queries.Count);
            Assert.Equal(workflows[1].Id, queryService.Queries[^1].WorkflowId);
        });
    }

    [Fact]
    public void Panel_renders_typed_totals_beyond_recent_window_and_preserves_pricing_duration_and_model_semantics()
    {
        using var context = CreateContext();
        var cut = context.Render<WorkflowAnalyticsPanel>(parameters => parameters
            .Add(component => component.QueryService, new RecordingWorkflowAnalyticsQueryService(CreateSnapshot()))
            .Add(component => component.Workflows, CreateWorkflowOptions())
            .Add(component => component.IsActive, true));

        cut.WaitForElement("[data-testid='workflow-analytics-content']");

        Assert.Contains("12", cut.Find("[data-testid='workflow-analytics-run-count']").TextContent, StringComparison.Ordinal);
        Assert.Equal(8, cut.FindAll("[data-testid='workflow-analytics-recent-run']").Count);
        Assert.Contains("24,000", cut.Find("[data-testid='workflow-analytics-total-tokens']").TextContent, StringComparison.Ordinal);
        Assert.Contains("10,000", cut.Find("[data-testid='workflow-analytics-input-tokens']").TextContent, StringComparison.Ordinal);
        Assert.Contains("2,000", cut.Find("[data-testid='workflow-analytics-cached-tokens']").TextContent, StringComparison.Ordinal);
        Assert.Contains("8,000", cut.Find("[data-testid='workflow-analytics-output-tokens']").TextContent, StringComparison.Ordinal);
        Assert.Contains("4,000", cut.Find("[data-testid='workflow-analytics-reasoning-tokens']").TextContent, StringComparison.Ordinal);
        Assert.Contains("$0.000000", cut.Find("[data-testid='workflow-analytics-known-cost']").TextContent, StringComparison.Ordinal);
        Assert.Contains("2", cut.Find("[data-testid='workflow-analytics-unknown-pricing']").TextContent, StringComparison.Ordinal);
        Assert.Contains("7 / 9 (77.8%)", cut.Find("[data-testid='workflow-analytics-completeness']").TextContent, StringComparison.Ordinal);
        Assert.Contains("01:30:00", cut.Find("[data-testid='workflow-analytics-duration-total']").TextContent, StringComparison.Ordinal);
        Assert.Contains("00:07:30", cut.Find("[data-testid='workflow-analytics-duration-average']").TextContent, StringComparison.Ordinal);
        Assert.Contains("00:00:30", cut.Find("[data-testid='workflow-analytics-duration-minimum']").TextContent, StringComparison.Ordinal);
        Assert.Contains("00:25:00", cut.Find("[data-testid='workflow-analytics-duration-maximum']").TextContent, StringComparison.Ordinal);
        Assert.Contains("10", cut.Find("[data-testid='workflow-analytics-duration-final']").TextContent, StringComparison.Ordinal);
        Assert.Contains("2", cut.Find("[data-testid='workflow-analytics-duration-active']").TextContent, StringComparison.Ordinal);
        Assert.Contains("OpenAi", cut.Find("[data-testid='workflow-analytics-provider-model']").TextContent, StringComparison.Ordinal);
        Assert.Contains("gpt-5", cut.Find("[data-testid='workflow-analytics-provider-model']").TextContent, StringComparison.Ordinal);
        Assert.Contains("Completed", cut.Find("[data-testid='workflow-analytics-distribution-grid']").TextContent, StringComparison.Ordinal);
        Assert.Contains("DurableTask", cut.Find("[data-testid='workflow-analytics-distribution-grid']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Panel_and_page_use_lazy_typed_projection_without_event_or_history_page_parsing()
    {
        var repositoryRoot = FindRepositoryRoot();
        var componentPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Pages",
            "Components",
            "WorkflowAnalyticsPanel.razor");
        var source = string.Concat(
            File.ReadAllText(componentPath),
            File.ReadAllText($"{componentPath}.cs"));

        Assert.Contains("IWorkflowAnalyticsQueryService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkflowEvent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", source, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonDocument", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HistoryRunPageSize", source, StringComparison.Ordinal);

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
        var analyticsTab = Slice(
            pageMarkup,
            "<TabsItem Text=\"Analytics\"",
            "</TabsItem>");

        Assert.Contains("WorkflowAnalyticsPanel", analyticsTab, StringComparison.Ordinal);
        Assert.Contains("QueryService=\"AnalyticsQueryService\"", analyticsTab, StringComparison.Ordinal);
        Assert.Contains("activeWorkflowTabIndex == AnalyticsTabIndex", analyticsTab, StringComparison.Ordinal);
        Assert.Contains("RefreshVersion=\"analyticsRefreshVersion\"", analyticsTab, StringComparison.Ordinal);
        Assert.DoesNotContain("runs.Count", analyticsTab, StringComparison.Ordinal);
        Assert.Contains("IWorkflowAnalyticsQueryService AnalyticsQueryService", pageCode, StringComparison.Ordinal);
        Assert.True(
            CountOccurrences(pageCode, "analyticsRefreshVersion++") >= 3,
            "Workflow definition/run mutations must mark an already-loaded analytics projection stale.");
    }

    [Fact]
    public void Panel_logs_actionable_scope_and_shows_safe_message_when_query_fails()
    {
        var logger = new RecordingLogger<WorkflowAnalyticsPanel>();
        using var context = CreateContext(logger);
        var cut = context.Render<WorkflowAnalyticsPanel>(parameters => parameters
            .Add(component => component.QueryService, new FailingWorkflowAnalyticsQueryService())
            .Add(component => component.Workflows, CreateWorkflowOptions())
            .Add(component => component.IsActive, true));

        cut.WaitForElement("[data-testid='workflow-analytics-error']");

        Assert.Contains(
            "Workflow analytics are temporarily unavailable. Retry the query.",
            cut.Find("[data-testid='workflow-analytics-error']").TextContent,
            StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-provider-payload", cut.Markup, StringComparison.Ordinal);
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("scope All", entry.Message, StringComparison.Ordinal);
        Assert.Contains("refresh version 0", entry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-provider-payload", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Panel_ignores_older_query_that_completes_after_newer_refresh()
    {
        using var context = CreateContext();
        var queryService = new ControllableWorkflowAnalyticsQueryService();
        var cut = context.Render<WorkflowAnalyticsPanel>(parameters => parameters
            .Add(component => component.QueryService, queryService)
            .Add(component => component.Workflows, CreateWorkflowOptions())
            .Add(component => component.IsActive, true)
            .Add(component => component.RefreshVersion, 1));
        cut.WaitForAssertion(() => Assert.Single(queryService.Requests));

        cut.Render(parameters => parameters
            .Add(component => component.RefreshVersion, 2));
        cut.WaitForAssertion(() => Assert.Equal(2, queryService.Requests.Count));

        queryService.Requests[1].Completion.SetResult(CreateSnapshot() with { RunCount = 22 });
        cut.WaitForAssertion(() => Assert.Contains(
            "22",
            cut.Find("[data-testid='workflow-analytics-run-count']").TextContent,
            StringComparison.Ordinal));

        queryService.Requests[0].Completion.SetResult(CreateSnapshot() with { RunCount = 11 });
        cut.WaitForAssertion(() => Assert.Contains(
            "22",
            cut.Find("[data-testid='workflow-analytics-run-count']").TextContent,
            StringComparison.Ordinal));
    }

    private static BunitContext CreateContext(ILogger<WorkflowAnalyticsPanel>? logger = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        if (logger is not null)
        {
            context.Services.RemoveAll<ILogger<WorkflowAnalyticsPanel>>();
            context.Services.AddSingleton(logger);
        }

        return context;
    }

    private static WorkflowAnalyticsSnapshot CreateSnapshot()
    {
        var runs = Enumerable.Range(1, 12)
            .Select(CreateRun)
            .ToArray();
        var usage = new WorkflowUsageAnalyticsTotals(
            ObservationCount: 9,
            UsageKnownObservationCount: 7,
            UsageUnknownObservationCount: 2,
            PricingKnownObservationCount: 7,
            PricingUnknownObservationCount: 2,
            InputTokens: 10_000,
            CachedInputTokens: 2_000,
            OutputTokens: 8_000,
            ReasoningTokens: 4_000,
            TotalTokens: 24_000,
            ToolCallCount: 5,
            KnownCostUsd: 0m);
        var providerUsage = usage with
        {
            ObservationCount = 4,
            UsageKnownObservationCount = 4,
            UsageUnknownObservationCount = 0,
            PricingKnownObservationCount = 3,
            PricingUnknownObservationCount = 1,
            TotalTokens = 12_000
        };

        return new WorkflowAnalyticsSnapshot(
            AsOfUtc,
            DefinitionCount: 2,
            ActiveDefinitionCount: 1,
            DefinitionsByStatus: new Dictionary<WorkflowLifecycleStatus, int>
            {
                [WorkflowLifecycleStatus.Active] = 1,
                [WorkflowLifecycleStatus.Draft] = 1
            },
            RunCount: 12,
            RunningRunCount: 1,
            WaitingForInputRunCount: 1,
            FailedRunCount: 2,
            RunsByState: new Dictionary<WorkflowRunState, int>
            {
                [WorkflowRunState.Completed] = 8,
                [WorkflowRunState.Failed] = 2,
                [WorkflowRunState.Running] = 1,
                [WorkflowRunState.WaitingForInput] = 1
            },
            RunsByBackend: new Dictionary<WorkflowRuntimeBackendKind, int>
            {
                [WorkflowRuntimeBackendKind.InProcess] = 7,
                [WorkflowRuntimeBackendKind.DurableTask] = 5
            },
            Usage: usage,
            Duration: new WorkflowDurationAnalyticsSummary(
                AvailableRunCount: 12,
                FinalRunCount: 10,
                ActiveRunCount: 2,
                UnavailableRunCount: 0,
                Total: TimeSpan.FromMinutes(90),
                Average: TimeSpan.FromMinutes(7.5),
                Minimum: TimeSpan.FromSeconds(30),
                Maximum: TimeSpan.FromMinutes(25)),
            Runs: runs.Select(run => new WorkflowRunAnalyticsRow(
                run,
                TimeSpan.FromMinutes(5),
                run.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled,
                usage)).ToArray(),
            ProviderModels:
            [
                new WorkflowProviderModelAnalyticsRow(
                    "OpenAi",
                    ProviderKind.OpenAi,
                    "gpt-5",
                    providerUsage)
            ],
            Nodes: [],
            RecentRuns: runs.Take(8).ToArray());
    }

    private static WorkflowRunSnapshot CreateRun(int index)
    {
        var state = index switch
        {
            1 => WorkflowRunState.Running,
            2 => WorkflowRunState.WaitingForInput,
            3 or 4 => WorkflowRunState.Failed,
            _ => WorkflowRunState.Completed
        };

        return new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            new WorkflowId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            WorkflowVersionId.New(),
            state,
            index % 2 == 0 ? WorkflowRuntimeBackendKind.DurableTask : WorkflowRuntimeBackendKind.InProcess,
            $"analytics-run-{index}",
            $"Analytics run {index}",
            AsOfUtc.AddMinutes(-index * 10),
            AsOfUtc.AddMinutes(-index))
        {
            TerminalAtUtc = state is WorkflowRunState.Completed or WorkflowRunState.Failed
                ? AsOfUtc.AddMinutes(-index)
                : null
        };
    }

    private static IReadOnlyList<WorkflowCatalogItem> CreateWorkflowOptions()
        =>
        [
            new WorkflowCatalogItem(
                new WorkflowId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                WorkflowVersionId.New(),
                "Primary workflow",
                "Primary analytics scope.",
                WorkflowLifecycleStatus.Active,
                WorkflowRuntimeBackendKind.InProcess,
                AsOfUtc),
            new WorkflowCatalogItem(
                new WorkflowId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
                WorkflowVersionId.New(),
                "Secondary workflow",
                "Secondary analytics scope.",
                WorkflowLifecycleStatus.Draft,
                WorkflowRuntimeBackendKind.DurableTask,
                AsOfUtc)
        ];

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the CanDoItAll repository root.");
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");
        return source[start..(end + endMarker.Length)];
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private sealed class RecordingWorkflowAnalyticsQueryService(WorkflowAnalyticsSnapshot snapshot) :
        IWorkflowAnalyticsQueryService
    {
        public List<WorkflowAnalyticsQuery> Queries { get; } = [];

        public Task<WorkflowAnalyticsSnapshot> QueryAsync(
            WorkflowAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FailingWorkflowAnalyticsQueryService : IWorkflowAnalyticsQueryService
    {
        public Task<WorkflowAnalyticsSnapshot> QueryAsync(
            WorkflowAnalyticsQuery query,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("sensitive-provider-payload");
    }

    private sealed class ControllableWorkflowAnalyticsQueryService : IWorkflowAnalyticsQueryService
    {
        public List<PendingAnalyticsQuery> Requests { get; } = [];

        public Task<WorkflowAnalyticsSnapshot> QueryAsync(
            WorkflowAnalyticsQuery query,
            CancellationToken cancellationToken = default)
        {
            var request = new PendingAnalyticsQuery(
                query,
                cancellationToken,
                new TaskCompletionSource<WorkflowAnalyticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously));
            Requests.Add(request);
            return request.Completion.Task;
        }
    }

    private sealed record PendingAnalyticsQuery(
        WorkflowAnalyticsQuery Query,
        CancellationToken CancellationToken,
        TaskCompletionSource<WorkflowAnalyticsSnapshot> Completion);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
