using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectManagerSummarySnapshotCalculatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 28, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Historical_cost_sources_keep_disjoint_category_ownership()
    {
        var input = CreateInput(
            agent: new ProjectManagerAgentSummaryInput(
                KnownCostUsd: 2m,
                UnknownCostRunCount: 1,
                LegacyProjectAttributionRunCount: 0,
                InvalidProjectAttributionRunCount: 0,
                InvalidCorrelationRunCount: 0,
                DailyCost: [],
                Activities: []),
            workflow: new ProjectManagerWorkflowSummaryInput(
                TotalCount: 2,
                KnownCostUsd: 3m,
                DurationMilliseconds: 0L,
                UnknownCostRunCount: 2,
                DailyCost: [],
                Activities: []),
            process: new ProjectManagerProcessSummaryInput(
                RunCount: 3,
                ActualCostUsd: 4m,
                EstimatedCostUsd: 5m,
                UnknownCostRunCount: 3,
                DurationMilliseconds: 0L,
                DailyCost: [],
                Activities: []));

        var snapshot = ProjectManagerSummarySnapshotCalculator.Calculate(input);

        var costs = snapshot.CostBreakdown.ToDictionary(static item => item.Category);
        Assert.Equal(2m, costs[ProjectManagerCostCategory.ChatsAndAgents].HistoricalKnownUsd);
        Assert.Equal(3m, costs[ProjectManagerCostCategory.Workflows].HistoricalKnownUsd);
        Assert.Equal(4m, costs[ProjectManagerCostCategory.Processes].HistoricalKnownUsd);
        Assert.Equal(5m, costs[ProjectManagerCostCategory.Processes].HistoricalEstimatedUsd);
        Assert.Equal(0m, costs[ProjectManagerCostCategory.ChatsAndAgents].HistoricalEstimatedUsd);
        Assert.Equal(0m, costs[ProjectManagerCostCategory.Workflows].HistoricalEstimatedUsd);
        Assert.Equal(9m, snapshot.Costs.HistoricalKnownUsd);
        Assert.Equal(5m, snapshot.Costs.HistoricalEstimatedUsd);
        Assert.Equal(6, snapshot.Costs.UnknownHistoricalCostCount);
    }

    [Fact]
    public void Future_plan_costs_are_included_only_when_requested()
    {
        var plan = CreatePlan(
            costs:
            [
                new ProjectPlanExpectedResourceCostTotal(
                    ProjectPlanResourceGroup.Person,
                    "USD",
                    Amount: 7m,
                    PricedTaskCount: 1)
            ],
            trend:
            [
                new ProjectPlanExpectedCostTrendPoint(
                    DateOnly.FromDateTime(Now.UtcDateTime),
                    ProjectPlanResourceGroup.Person,
                    "USD",
                    Amount: 7m)
            ]);

        var historyOnly = ProjectManagerSummarySnapshotCalculator.Calculate(
            CreateInput(plans: [plan]));
        var historyAndFuture = ProjectManagerSummarySnapshotCalculator.Calculate(
            CreateInput(
                contentMode: ProjectManagerSummaryContentMode.HistoryAndFuture,
                plans: [plan]));

        Assert.Equal(0m, historyOnly.Costs.FuturePlannedUsd);
        Assert.Empty(historyOnly.ExpenseTrend);
        Assert.Equal(7m, historyAndFuture.Costs.FuturePlannedUsd);
        Assert.Equal(
            7m,
            historyAndFuture.CostBreakdown.Single(
                static item => item.Category == ProjectManagerCostCategory.Workforce)
                .FuturePlannedUsd);
        Assert.Equal(7m, Assert.Single(historyAndFuture.ExpenseTrend).FuturePlannedUsd);
    }

    [Fact]
    public void Latest_activity_merges_sources_and_keeps_only_newest_five()
    {
        var oldest = CreateActivity(1, Now.AddMinutes(-6), ProjectManagerActivityKind.Conversation);
        var activities = new[]
        {
            oldest,
            CreateActivity(2, Now.AddMinutes(-5), ProjectManagerActivityKind.Conversation),
            CreateActivity(3, Now.AddMinutes(-4), ProjectManagerActivityKind.Workflow),
            CreateActivity(4, Now.AddMinutes(-3), ProjectManagerActivityKind.Process),
            CreateActivity(5, Now.AddMinutes(-2), ProjectManagerActivityKind.Conversation),
            CreateActivity(6, Now.AddMinutes(-1), ProjectManagerActivityKind.Workflow)
        };
        var input = CreateInput(
            agent: EmptyAgent([activities[0], activities[1], activities[4]]),
            workflow: ProjectManagerWorkflowSummaryInput.Empty with
            {
                Activities = [activities[2], activities[5]]
            },
            process: ProjectManagerProcessSummaryInput.Empty with
            {
                Activities = [activities[3]]
            });

        var snapshot = ProjectManagerSummarySnapshotCalculator.Calculate(input);

        Assert.Equal(
            ["Activity 6", "Activity 5", "Activity 4", "Activity 3", "Activity 2"],
            snapshot.LatestActivities.Select(static activity => activity.Title));
        Assert.DoesNotContain(oldest, snapshot.LatestActivities);
    }

    [Fact]
    public void Expense_trend_merges_known_estimated_and_future_values_by_day()
    {
        var date = DateOnly.FromDateTime(Now.UtcDateTime);
        var input = CreateInput(
            contentMode: ProjectManagerSummaryContentMode.HistoryAndFuture,
            plans:
            [
                CreatePlan(
                    costs:
                    [
                        new ProjectPlanExpectedResourceCostTotal(
                            ProjectPlanResourceGroup.External,
                            "USD",
                            Amount: 5m,
                            PricedTaskCount: 1)
                    ],
                    trend:
                    [
                        new ProjectPlanExpectedCostTrendPoint(
                            date,
                            ProjectPlanResourceGroup.External,
                            "USD",
                            Amount: 5m)
                    ])
            ],
            agent: EmptyAgent(
                dailyCost: [new ProjectManagerKnownExpensePoint(date, 1m)]),
            workflow: ProjectManagerWorkflowSummaryInput.Empty with
            {
                DailyCost = [new ProjectManagerKnownExpensePoint(date, 2m)]
            },
            process: ProjectManagerProcessSummaryInput.Empty with
            {
                DailyCost = [new ProjectManagerProcessExpensePoint(date, 3m, 4m)]
            });

        var point = Assert.Single(
            ProjectManagerSummarySnapshotCalculator.Calculate(input).ExpenseTrend);

        Assert.Equal(date, point.Date);
        Assert.Equal(6m, point.HistoricalKnownUsd);
        Assert.Equal(4m, point.HistoricalEstimatedUsd);
        Assert.Equal(5m, point.FuturePlannedUsd);
    }

    [Fact]
    public void Non_usd_forecast_stays_separate_from_usd_totals_and_chart()
    {
        var plan = CreatePlan(
            costs:
            [
                new ProjectPlanExpectedResourceCostTotal(
                    ProjectPlanResourceGroup.External,
                    "eur",
                    Amount: 12.50m,
                    PricedTaskCount: 2)
            ],
            trend:
            [
                new ProjectPlanExpectedCostTrendPoint(
                    DateOnly.FromDateTime(Now.UtcDateTime),
                    ProjectPlanResourceGroup.External,
                    "EUR",
                    Amount: 12.50m)
            ]);

        var snapshot = ProjectManagerSummarySnapshotCalculator.Calculate(
            CreateInput(
                contentMode: ProjectManagerSummaryContentMode.HistoryAndFuture,
                plans: [plan]));

        Assert.Equal(0m, snapshot.Costs.FuturePlannedUsd);
        Assert.Empty(snapshot.ExpenseTrend);
        var otherCurrency = Assert.Single(snapshot.OtherCurrencyFutureCosts);
        Assert.Equal("EUR", otherCurrency.CurrencyCode);
        Assert.Equal(12.50m, otherCurrency.FuturePlannedAmount);
        Assert.Equal(2, otherCurrency.PricedTaskCount);
        Assert.Contains(
            snapshot.Warnings,
            static warning => warning.Contains(
                "Non-USD planned costs",
                StringComparison.Ordinal));
    }

    private static ProjectManagerSummaryCompositionInput CreateInput(
        ProjectManagerSummaryContentMode contentMode =
            ProjectManagerSummaryContentMode.HistoryOnly,
        IReadOnlyList<ProjectPlanManagerSummary>? plans = null,
        ProjectManagerAgentSummaryInput? agent = null,
        ProjectManagerWorkflowSummaryInput? workflow = null,
        ProjectManagerProcessSummaryInput? process = null)
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var options = new ProjectManagerSummaryOptions(
            contentMode,
            ProjectManagerSummaryScope.CurrentProject,
            ProjectManagerSummaryTimeRange.Month);
        var scope = new ProjectManagerSummaryScopeResolution(
            projectId,
            "Project",
            options.Scope,
            [projectId],
            DescendantCount: 0,
            RequiresConfirmation: false);
        return new ProjectManagerSummaryCompositionInput(
            scope,
            options,
            HistoryFromUtc: Now.AddMonths(-1),
            AsOfUtc: Now,
            GeneratedAtUtc: Now,
            plans ?? [],
            agent ?? EmptyAgent(),
            workflow ?? ProjectManagerWorkflowSummaryInput.Empty,
            process ?? ProjectManagerProcessSummaryInput.Empty);
    }

    private static ProjectPlanManagerSummary CreatePlan(
        IReadOnlyList<ProjectPlanExpectedResourceCostTotal>? costs = null,
        IReadOnlyList<ProjectPlanExpectedCostTrendPoint>? trend = null)
        => new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Project",
            Now,
            TotalTaskCount: 1,
            new ProjectPlanScheduleSummary(
                EarliestStartUtc: Now,
                LatestEndUtc: Now.AddHours(8),
                DeliveryLeadTimeHours: 8m,
                ScheduledTaskDurationHours: 8m),
            costs ?? [],
            trend ?? [],
            UnscheduledFutureExpectedCostTaskCount: 0,
            Warnings: []);

    private static ProjectManagerAgentSummaryInput EmptyAgent(
        IReadOnlyList<ProjectManagerActivity>? activities = null,
        IReadOnlyList<ProjectManagerKnownExpensePoint>? dailyCost = null)
        => new(
            KnownCostUsd: 0m,
            UnknownCostRunCount: 0,
            LegacyProjectAttributionRunCount: 0,
            InvalidProjectAttributionRunCount: 0,
            InvalidCorrelationRunCount: 0,
            dailyCost ?? [],
            activities ?? []);

    private static ProjectManagerActivity CreateActivity(
        int id,
        DateTimeOffset endedAtUtc,
        ProjectManagerActivityKind kind)
        => new(
            new ProjectManagerActivityId(new Guid(
                id,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0)),
            kind,
            $"Activity {id}",
            "Summary",
            ProjectManagerActivityStatus.Succeeded,
            endedAtUtc,
            DurationMilliseconds: 1_000L,
            KnownCostUsd: 0m,
            EstimatedCostUsd: 0m,
            HasUnknownCost: false,
            Tags: []);
}
