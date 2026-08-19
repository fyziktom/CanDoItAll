namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectManagerSummaryCompositionInput(
    ProjectManagerSummaryScopeResolution Scope,
    ProjectManagerSummaryOptions Options,
    DateTimeOffset? HistoryFromUtc,
    DateTimeOffset AsOfUtc,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ProjectPlanManagerSummary> Plans,
    ProjectManagerAgentSummaryInput Agent,
    ProjectManagerSimpleChatSummaryInput SimpleChat,
    ProjectManagerWorkflowSummaryInput Workflow,
    ProjectManagerProcessSummaryInput Process);

internal sealed record ProjectManagerAgentSummaryInput(
    decimal KnownCostUsd,
    int UnknownCostRunCount,
    int LegacyProjectAttributionRunCount,
    int InvalidProjectAttributionRunCount,
    int InvalidCorrelationRunCount,
    IReadOnlyList<ProjectManagerKnownExpensePoint> DailyCost,
    IReadOnlyList<ProjectManagerActivity> Activities);

internal sealed record ProjectManagerWorkflowSummaryInput(
    int TotalCount,
    decimal KnownCostUsd,
    long DurationMilliseconds,
    int UnknownCostRunCount,
    IReadOnlyList<ProjectManagerKnownExpensePoint> DailyCost,
    IReadOnlyList<ProjectManagerActivity> Activities)
{
    public static ProjectManagerWorkflowSummaryInput Empty { get; } = new(
        0,
        0m,
        0L,
        0,
        [],
        []);
}

internal sealed record ProjectManagerSimpleChatSummaryInput(
    int TotalCount,
    decimal KnownCostUsd,
    long DurationMilliseconds,
    int UnknownCostRunCount,
    IReadOnlyList<ProjectManagerKnownExpensePoint> DailyCost,
    IReadOnlyList<ProjectManagerActivity> Activities)
{
    public static ProjectManagerSimpleChatSummaryInput Empty { get; } = new(
        0,
        0m,
        0L,
        0,
        [],
        []);
}

internal sealed record ProjectManagerProcessSummaryInput(
    int RunCount,
    decimal ActualCostUsd,
    decimal EstimatedCostUsd,
    int UnknownCostRunCount,
    long DurationMilliseconds,
    IReadOnlyList<ProjectManagerProcessExpensePoint> DailyCost,
    IReadOnlyList<ProjectManagerActivity> Activities)
{
    public static ProjectManagerProcessSummaryInput Empty { get; } = new(
        0,
        0m,
        0m,
        0,
        0L,
        [],
        []);
}

internal readonly record struct ProjectManagerKnownExpensePoint(
    DateOnly Date,
    decimal KnownUsd);

internal readonly record struct ProjectManagerProcessExpensePoint(
    DateOnly Date,
    decimal ActualUsd,
    decimal EstimatedUsd);

internal static class ProjectManagerSummarySnapshotCalculator
{
    internal const int LatestActivityCount = 5;
    internal const int MaximumChartDayCount = 366;

    public static ProjectManagerSummarySnapshot Calculate(
        ProjectManagerSummaryCompositionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var costs = Enum.GetValues<ProjectManagerCostCategory>()
            .ToDictionary(
                static category => category,
                static _ => new CostAccumulator());
        var expense = new SortedDictionary<DateOnly, ExpenseAccumulator>();
        AddAgentCosts(costs, expense, input.Agent);
        AddSimpleChatCosts(costs, expense, input.SimpleChat);
        AddWorkflowCosts(costs, expense, input.Workflow);
        AddProcessCosts(costs, expense, input.Process);

        var otherCurrencyCosts = new Dictionary<string, CurrencyCostAccumulator>(
            StringComparer.OrdinalIgnoreCase);
        if (input.Options.ContentMode == ProjectManagerSummaryContentMode.HistoryAndFuture)
        {
            AddFuturePlanCosts(costs, expense, otherCurrencyCosts, input.Plans);
        }

        var costBreakdown = costs
            .OrderBy(static item => item.Key)
            .Select(static item => item.Value.Build(item.Key))
            .ToArray();
        var costTotals = new ProjectManagerCostTotals(
            costBreakdown.Sum(static item => item.HistoricalKnownUsd),
            costBreakdown.Sum(static item => item.HistoricalEstimatedUsd),
            costBreakdown.Sum(static item => item.FuturePlannedUsd),
            costBreakdown.Sum(static item => item.UnknownHistoricalCostCount));
        var latestActivities = input.Agent.Activities
            .Concat(input.SimpleChat.Activities)
            .Concat(input.Workflow.Activities)
            .Concat(input.Process.Activities)
            .OrderByDescending(static activity => activity.ActivityAtUtc)
            .ThenBy(static activity => activity.Id.Value)
            .Take(LatestActivityCount)
            .ToArray();

        return new ProjectManagerSummarySnapshot(
            input.Scope.RootProjectId,
            input.Scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity
                ? "Uncategorized agent activity"
                : input.Scope.RootProjectName,
            input.Options,
            input.Scope,
            input.HistoryFromUtc,
            input.AsOfUtc,
            input.GeneratedAtUtc,
            BuildTaskSchedule(input.Plans),
            costTotals,
            costBreakdown,
            otherCurrencyCosts
                .OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static item => new ProjectManagerCurrencyCostTotal(
                    item.Key.ToUpperInvariant(),
                    item.Value.Amount,
                    item.Value.PricedTaskCount))
                .ToArray(),
            expense
                .Select(static item => item.Value.Build(item.Key))
                .ToArray(),
            latestActivities,
            BuildWarnings(
                input.Scope,
                input.Options,
                input.Plans,
                costTotals,
                otherCurrencyCosts.Count,
                input.Agent));
    }

    private static void AddAgentCosts(
        IReadOnlyDictionary<ProjectManagerCostCategory, CostAccumulator> costs,
        IDictionary<DateOnly, ExpenseAccumulator> expense,
        ProjectManagerAgentSummaryInput input)
    {
        costs[ProjectManagerCostCategory.ChatsAndAgents].AddHistory(
            input.KnownCostUsd,
            estimatedUsd: 0m,
            input.UnknownCostRunCount);
        AddKnownExpense(expense, input.DailyCost);
    }

    private static void AddWorkflowCosts(
        IReadOnlyDictionary<ProjectManagerCostCategory, CostAccumulator> costs,
        IDictionary<DateOnly, ExpenseAccumulator> expense,
        ProjectManagerWorkflowSummaryInput input)
    {
        costs[ProjectManagerCostCategory.Workflows].AddHistory(
            input.KnownCostUsd,
            estimatedUsd: 0m,
            input.UnknownCostRunCount);
        AddKnownExpense(expense, input.DailyCost);
    }

    private static void AddSimpleChatCosts(
        IReadOnlyDictionary<ProjectManagerCostCategory, CostAccumulator> costs,
        IDictionary<DateOnly, ExpenseAccumulator> expense,
        ProjectManagerSimpleChatSummaryInput input)
    {
        costs[ProjectManagerCostCategory.ChatsAndAgents].AddHistory(
            input.KnownCostUsd,
            estimatedUsd: 0m,
            input.UnknownCostRunCount);
        AddKnownExpense(expense, input.DailyCost);
    }

    private static void AddProcessCosts(
        IReadOnlyDictionary<ProjectManagerCostCategory, CostAccumulator> costs,
        IDictionary<DateOnly, ExpenseAccumulator> expense,
        ProjectManagerProcessSummaryInput input)
    {
        costs[ProjectManagerCostCategory.Processes].AddHistory(
            input.ActualCostUsd,
            input.EstimatedCostUsd,
            input.UnknownCostRunCount);
        foreach (var point in input.DailyCost)
        {
            var dailyExpense = GetExpense(expense, point.Date);
            dailyExpense.HistoricalKnownUsd += point.ActualUsd;
            dailyExpense.HistoricalEstimatedUsd += point.EstimatedUsd;
        }
    }

    private static void AddKnownExpense(
        IDictionary<DateOnly, ExpenseAccumulator> target,
        IReadOnlyList<ProjectManagerKnownExpensePoint> points)
    {
        foreach (var point in points)
        {
            var expense = GetExpense(target, point.Date);
            expense.HistoricalKnownUsd += point.KnownUsd;
        }
    }

    private static void AddFuturePlanCosts(
        IReadOnlyDictionary<ProjectManagerCostCategory, CostAccumulator> costs,
        IDictionary<DateOnly, ExpenseAccumulator> expense,
        IDictionary<string, CurrencyCostAccumulator> otherCurrencyCosts,
        IReadOnlyList<ProjectPlanManagerSummary> plans)
    {
        foreach (var plan in plans)
        {
            foreach (var total in plan.FutureExpectedCostTotals)
            {
                if (IsUsd(total.CurrencyCode))
                {
                    costs[MapResourceGroup(total.Group)].AddFuture(total.Amount);
                    continue;
                }

                var currency = NormalizeCurrency(total.CurrencyCode);
                if (!otherCurrencyCosts.TryGetValue(currency, out var accumulator))
                {
                    accumulator = new CurrencyCostAccumulator();
                    otherCurrencyCosts.Add(currency, accumulator);
                }

                accumulator.Amount += total.Amount;
                accumulator.PricedTaskCount += total.PricedTaskCount;
            }

            foreach (var point in plan.FutureExpectedCostTrend.Where(
                         static point => IsUsd(point.CurrencyCode)))
            {
                GetExpense(expense, point.Date).FuturePlannedUsd += point.Amount;
            }
        }
    }

    private static ProjectManagerTaskSchedule BuildTaskSchedule(
        IReadOnlyList<ProjectPlanManagerSummary> plans)
    {
        var starts = plans
            .Select(static plan => plan.Schedule.EarliestStartUtc)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToArray();
        var ends = plans
            .Select(static plan => plan.Schedule.LatestEndUtc)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToArray();
        DateTimeOffset? startUtc = starts.Length == 0 ? null : starts.Min();
        DateTimeOffset? endUtc = ends.Length == 0 ? null : ends.Max();
        decimal? leadTimeHours = startUtc.HasValue && endUtc.HasValue && endUtc >= startUtc
            ? (decimal)(endUtc.Value - startUtc.Value).TotalHours
            : null;
        return new ProjectManagerTaskSchedule(
            plans.Sum(static plan => plan.TotalTaskCount),
            startUtc,
            endUtc,
            leadTimeHours,
            plans.Sum(static plan => plan.Schedule.ScheduledTaskDurationHours));
    }

    private static IReadOnlyList<string> BuildWarnings(
        ProjectManagerSummaryScopeResolution scope,
        ProjectManagerSummaryOptions options,
        IReadOnlyList<ProjectPlanManagerSummary> plans,
        ProjectManagerCostTotals costs,
        int otherCurrencyCount,
        ProjectManagerAgentSummaryInput agent)
    {
        var warnings = new List<string>();
        if (scope.Scope == ProjectManagerSummaryScope.UncategorizedAgentActivity)
        {
            warnings.Add(
                "Uncategorized means direct runs from the Agents and generic workspace surfaces. Project- and process-attributed runs are excluded.");
        }
        else
        {
            warnings.Add(
                "Historical workforce and external charges are not included because there is no canonical actual-cost ledger for them yet.");
        }

        if (costs.UnknownHistoricalCostCount > 0)
        {
            warnings.Add(
                $"{costs.UnknownHistoricalCostCount:N0} historical run(s) have incomplete pricing. Known totals do not silently estimate those runs.");
        }

        if (options.TimeRange == ProjectManagerSummaryTimeRange.All)
        {
            warnings.Add(
                $"Totals are all-time. Historical daily expenses are bounded to the latest {MaximumChartDayCount:N0} days; remaining plan milestones can extend beyond today.");
        }

        var unscheduledFutureCostTaskCount = plans.Sum(
            static plan => plan.UnscheduledFutureExpectedCostTaskCount);
        if (options.ContentMode == ProjectManagerSummaryContentMode.HistoryAndFuture &&
            unscheduledFutureCostTaskCount > 0)
        {
            warnings.Add(
                $"{unscheduledFutureCostTaskCount:N0} task(s) have remaining expected cost but no start date, so they are included in totals but not the expense timeline.");
        }

        if (options.ContentMode == ProjectManagerSummaryContentMode.HistoryAndFuture)
        {
            warnings.Add(
                "Remaining plan cost applies each task's recorded progress to its expected cost. Open tasks without valid progress retain their full estimate and are covered by the plan completeness warning.");
        }

        if (otherCurrencyCount > 0)
        {
            warnings.Add(
                "Non-USD planned costs remain in their source currencies and are not converted into the USD total or chart.");
        }

        if (agent.LegacyProjectAttributionRunCount > 0)
        {
            warnings.Add(
                $"{agent.LegacyProjectAttributionRunCount:N0} conversation run(s) use a compatible legacy project attribution. New runs use the recorded typed project scope.");
        }

        if (agent.InvalidProjectAttributionRunCount > 0)
        {
            warnings.Add(
                $"{agent.InvalidProjectAttributionRunCount:N0} conversation run(s) have malformed project attribution and are excluded from both project and uncategorized costs.");
        }

        if (agent.InvalidCorrelationRunCount > 0)
        {
            warnings.Add(
                $"{agent.InvalidCorrelationRunCount:N0} conversation run(s) have malformed process or workflow correlation identifiers and are excluded to prevent cost double counting.");
        }

        var incompletePlanCount = plans.Count(static plan => plan.Warnings.Count > 0);
        if (incompletePlanCount > 0)
        {
            warnings.Add(
                $"{incompletePlanCount:N0} project plan(s) report data-completeness warnings. Review their task planning data before treating projections as a forecast.");
        }

        return warnings;
    }

    private static ProjectManagerCostCategory MapResourceGroup(
        ProjectPlanResourceGroup group)
        => group switch
        {
            ProjectPlanResourceGroup.Person => ProjectManagerCostCategory.Workforce,
            ProjectPlanResourceGroup.Agent => ProjectManagerCostCategory.ChatsAndAgents,
            ProjectPlanResourceGroup.Workflow => ProjectManagerCostCategory.Workflows,
            ProjectPlanResourceGroup.Process => ProjectManagerCostCategory.Processes,
            ProjectPlanResourceGroup.External => ProjectManagerCostCategory.External,
            ProjectPlanResourceGroup.Unassigned => ProjectManagerCostCategory.Other,
            ProjectPlanResourceGroup.Mixed => ProjectManagerCostCategory.Other,
            _ => throw new ArgumentOutOfRangeException(
                nameof(group),
                group,
                "The project plan resource group is not supported.")
        };

    private static ExpenseAccumulator GetExpense(
        IDictionary<DateOnly, ExpenseAccumulator> target,
        DateOnly date)
    {
        if (target.TryGetValue(date, out var expense))
        {
            return expense;
        }

        expense = new ExpenseAccumulator();
        target.Add(date, expense);
        return expense;
    }

    private static bool IsUsd(string currencyCode)
        => string.Equals(
            NormalizeCurrency(currencyCode),
            "USD",
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string currencyCode)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? "UNSPECIFIED"
            : currencyCode.Trim();

    private sealed class CostAccumulator
    {
        public decimal HistoricalKnownUsd { get; private set; }

        public decimal HistoricalEstimatedUsd { get; private set; }

        public decimal FuturePlannedUsd { get; private set; }

        public int UnknownHistoricalCostCount { get; private set; }

        public void AddHistory(
            decimal knownUsd,
            decimal estimatedUsd,
            int unknownCostCount)
        {
            HistoricalKnownUsd += knownUsd;
            HistoricalEstimatedUsd += estimatedUsd;
            UnknownHistoricalCostCount += unknownCostCount;
        }

        public void AddFuture(decimal plannedUsd)
            => FuturePlannedUsd += plannedUsd;

        public ProjectManagerCostBreakdown Build(ProjectManagerCostCategory category)
            => new(
                category,
                HistoricalKnownUsd,
                HistoricalEstimatedUsd,
                FuturePlannedUsd,
                UnknownHistoricalCostCount);
    }

    private sealed class ExpenseAccumulator
    {
        public decimal HistoricalKnownUsd { get; set; }

        public decimal HistoricalEstimatedUsd { get; set; }

        public decimal FuturePlannedUsd { get; set; }

        public ProjectManagerExpensePoint Build(DateOnly date)
            => new(
                date,
                HistoricalKnownUsd,
                HistoricalEstimatedUsd,
                FuturePlannedUsd);
    }

    private sealed class CurrencyCostAccumulator
    {
        public decimal Amount { get; set; }

        public int PricedTaskCount { get; set; }
    }
}
