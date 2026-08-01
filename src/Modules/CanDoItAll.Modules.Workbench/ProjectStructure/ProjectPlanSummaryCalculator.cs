using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal interface IProjectPlanScheduleFact
{
    DateTimeOffset? StartUtc { get; }

    DateTimeOffset? EndUtc { get; }
}

internal sealed record ProjectPlanTaskFact(
    string NodeId,
    string Title,
    string Status,
    int ProgressPercent,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string MetadataJson) : IProjectPlanScheduleFact;

internal sealed record ProjectPlanScheduleTaskFact(
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc) : IProjectPlanScheduleFact;

internal sealed record ProjectPlanLinkFact(
    string SourceNodeId,
    string TargetNodeId,
    ProjectObjectLinkKind Kind);

internal sealed record ProjectPlanResourceBindingFact(
    string TaskNodeId,
    ProjectPlanResourceGroup Group,
    string ResourceKey);

internal sealed record ProjectPlanSnapshot(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectPlanTaskFact> Tasks,
    IReadOnlyList<ProjectPlanLinkFact> Links,
    IReadOnlyList<ProjectPlanResourceBindingFact> ResourceBindings);

internal sealed record ProjectPlanManagerScheduleSnapshot(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectPlanScheduleTaskFact> Tasks);

internal sealed record ProjectPlanManagerForecastSnapshot(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectPlanTaskFact> Tasks,
    IReadOnlyList<ProjectPlanResourceBindingFact> ResourceBindings);

public sealed class ProjectPlanSummaryCalculator
{
    public const int MaximumTaskPreviewLimit = 100;
    public const int MaximumBlockingTaskIdPreview = 20;
    public const decimal MinimumHoursPerManDay = 1m;
    public const decimal MaximumHoursPerManDay = 24m;
    private const int CancellationCheckInterval = 256;

    private static readonly IReadOnlySet<ProjectPlanResourceGroup> EmptyResourceGroups =
        new HashSet<ProjectPlanResourceGroup>();
    internal ProjectPlanSummary Build(
        ProjectPlanSnapshot snapshot,
        ProjectPlanSummaryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedQuery = NormalizeQuery(query);
        var tasksById = BuildTaskIndex(snapshot.Tasks, cancellationToken);
        var prerequisitesByTask = BuildPrerequisiteIndex(snapshot.Links, tasksById, cancellationToken);
        var dependencyCycleAffectedTaskIds = FindDependencyCycleAffectedTasks(
            tasksById,
            prerequisitesByTask,
            cancellationToken);
        var resourceBindings = BuildResourceBindingIndex(
            snapshot.ResourceBindings,
            tasksById,
            cancellationToken);
        var (evaluations, completedTaskIds) = EvaluateTasks(
            tasksById,
            prerequisitesByTask,
            resourceBindings.GroupsByTask,
            normalizedQuery.AsOfUtc,
            normalizedQuery.HoursPerManDay,
            cancellationToken);

        return BuildSummary(
            snapshot,
            evaluations,
            prerequisitesByTask,
            resourceBindings,
            dependencyCycleAffectedTaskIds.Count,
            completedTaskIds,
            normalizedQuery,
            cancellationToken);
    }

    internal static void ValidateQuery(ProjectPlanSummaryQuery? query)
    {
        _ = NormalizeQuery(query);
    }

    internal static void ValidateManagerQuery(ProjectPlanManagerSummaryQuery query)
    {
        _ = NormalizeManagerQuery(query);
    }

    internal ProjectPlanManagerSummary BuildManagerSummary(
        ProjectPlanManagerScheduleSnapshot snapshot,
        ProjectPlanManagerSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedQuery = NormalizeManagerQuery(query);
        if (normalizedQuery.Mode != ProjectPlanManagerSummaryMode.ScheduleOnly)
        {
            throw new ArgumentException(
                "A schedule snapshot requires the schedule-only manager summary mode.",
                nameof(query));
        }

        var schedule = CalculateSchedule(snapshot.Tasks, cancellationToken);
        return new ProjectPlanManagerSummary(
            snapshot.ProjectId,
            snapshot.ProjectName,
            normalizedQuery.AsOfUtc,
            snapshot.Tasks.Count,
            schedule.Summary,
            [],
            [],
            UnscheduledFutureExpectedCostTaskCount: 0,
            BuildManagerWarnings(
                schedule.MissingTaskCount,
                schedule.InvalidTaskCount,
                invalidMetadataTaskCount: 0,
                missingExpectedCostTaskCount: 0,
                missingProgressTaskCount: 0,
                invalidProgressTaskCount: 0));
    }

    internal ProjectPlanManagerSummary BuildManagerSummary(
        ProjectPlanManagerForecastSnapshot snapshot,
        ProjectPlanManagerSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedQuery = NormalizeManagerQuery(query);
        if (normalizedQuery.Mode != ProjectPlanManagerSummaryMode.ScheduleAndRemainingCosts)
        {
            throw new ArgumentException(
                "A forecast snapshot requires the schedule-and-remaining-costs manager summary mode.",
                nameof(query));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var tasksById = BuildTaskIndex(snapshot.Tasks, cancellationToken);
        var resourceBindings = BuildResourceBindingIndex(
            snapshot.ResourceBindings,
            tasksById,
            cancellationToken);
        var schedule = CalculateSchedule(snapshot.Tasks, cancellationToken);
        var futureCostTotals =
            new Dictionary<(ProjectPlanResourceGroup Group, string CurrencyCode), (decimal Amount, int TaskCount)>();
        var futureCostTrend =
            new Dictionary<(DateOnly Date, ProjectPlanResourceGroup Group, string CurrencyCode), decimal>();
        var invalidMetadataTaskCount = 0;
        var missingExpectedCostTaskCount = 0;
        var missingProgressTaskCount = 0;
        var invalidProgressTaskCount = 0;
        var unscheduledFutureExpectedCostTaskCount = 0;
        var cancellationCountdown = CancellationCheckInterval;

        foreach (var task in tasksById.Values)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            var normalizedStatus = NormalizeStatus(task.Status);
            if (ResolveTerminalState(task, normalizedStatus).HasValue)
            {
                continue;
            }

            var estimate = ParseEstimate(
                task.MetadataJson,
                ProjectTaskEstimatePolicy.DefaultHoursPerManDay);
            if (estimate.IsInvalid)
            {
                invalidMetadataTaskCount++;
            }

            if (!estimate.Estimate.ExpectedCostAmount.HasValue)
            {
                if (!estimate.IsInvalid)
                {
                    missingExpectedCostTaskCount++;
                }
                continue;
            }

            var progress = ParseProgress(task.ProgressPercent);
            if (progress.IsMissing)
            {
                missingProgressTaskCount++;
            }
            else if (progress.IsInvalid)
            {
                invalidProgressTaskCount++;
            }

            var amount = estimate.Estimate.ExpectedCostAmount.Value;
            var remainingAmount = progress.Percent.HasValue
                ? amount * (100m - progress.Percent.Value) / 100m
                : amount;
            if (remainingAmount <= 0m)
            {
                continue;
            }

            resourceBindings.GroupsByTask.TryGetValue(task.NodeId, out var resourceGroups);
            var resourceGroup = ResolveExpectedCostResourceGroup(
                resourceGroups ?? EmptyResourceGroups);
            var currencyCode = estimate.Estimate.ExpectedCostCurrencyCode;
            var costKey = (resourceGroup, currencyCode);
            futureCostTotals.TryGetValue(costKey, out var currentFutureCost);
            futureCostTotals[costKey] = (
                currentFutureCost.Amount + remainingAmount,
                currentFutureCost.TaskCount + 1);

            var trendDate = ResolveFutureCostTrendDate(task, normalizedQuery.AsOfUtc);
            if (!trendDate.HasValue)
            {
                unscheduledFutureExpectedCostTaskCount++;
                continue;
            }

            var trendKey = (
                trendDate.Value,
                resourceGroup,
                currencyCode);
            futureCostTrend.TryGetValue(trendKey, out var currentTrendCost);
            futureCostTrend[trendKey] = currentTrendCost + remainingAmount;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ProjectPlanManagerSummary(
            snapshot.ProjectId,
            snapshot.ProjectName,
            normalizedQuery.AsOfUtc,
            snapshot.Tasks.Count,
            schedule.Summary,
            futureCostTotals
                .OrderBy(static item => item.Key.CurrencyCode, StringComparer.Ordinal)
                .ThenBy(static item => item.Key.Group)
                .Select(static item => new ProjectPlanExpectedResourceCostTotal(
                    item.Key.Group,
                    item.Key.CurrencyCode,
                    item.Value.Amount,
                    item.Value.TaskCount))
                .ToArray(),
            futureCostTrend
                .OrderBy(static item => item.Key.Date)
                .ThenBy(static item => item.Key.CurrencyCode, StringComparer.Ordinal)
                .ThenBy(static item => item.Key.Group)
                .Select(static item => new ProjectPlanExpectedCostTrendPoint(
                    item.Key.Date,
                    item.Key.Group,
                    item.Key.CurrencyCode,
                    item.Value))
                .ToArray(),
            unscheduledFutureExpectedCostTaskCount,
            BuildManagerWarnings(
                schedule.MissingTaskCount,
                schedule.InvalidTaskCount,
                invalidMetadataTaskCount,
                missingExpectedCostTaskCount,
                missingProgressTaskCount,
                invalidProgressTaskCount));
    }

    private static Dictionary<string, ProjectPlanTaskFact> BuildTaskIndex(
        IReadOnlyList<ProjectPlanTaskFact> tasks,
        CancellationToken cancellationToken)
    {
        var tasksById = new Dictionary<string, ProjectPlanTaskFact>(tasks.Count, StringComparer.Ordinal);
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var task in tasks)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(task.NodeId);
            if (!tasksById.TryAdd(task.NodeId, task))
            {
                throw new InvalidOperationException($"Project plan contains duplicate task node id '{task.NodeId}'.");
            }
        }

        return tasksById;
    }

    private static Dictionary<string, HashSet<string>> BuildPrerequisiteIndex(
        IReadOnlyList<ProjectPlanLinkFact> links,
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById,
        CancellationToken cancellationToken)
    {
        var prerequisitesByTask = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var link in links)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (!tasksById.ContainsKey(link.SourceNodeId) || !tasksById.ContainsKey(link.TargetNodeId))
            {
                continue;
            }

            switch (link.Kind)
            {
                case ProjectObjectLinkKind.DependsOn:
                    AddPrerequisite(prerequisitesByTask, link.SourceNodeId, link.TargetNodeId);
                    break;
                case ProjectObjectLinkKind.Blocks:
                    AddPrerequisite(prerequisitesByTask, link.TargetNodeId, link.SourceNodeId);
                    break;
            }
        }

        return prerequisitesByTask;
    }

    private static void AddPrerequisite(
        IDictionary<string, HashSet<string>> prerequisitesByTask,
        string taskId,
        string prerequisiteId)
    {
        if (!prerequisitesByTask.TryGetValue(taskId, out var prerequisiteIds))
        {
            prerequisiteIds = new HashSet<string>(StringComparer.Ordinal);
            prerequisitesByTask.Add(taskId, prerequisiteIds);
        }

        prerequisiteIds.Add(prerequisiteId);
    }

    private static HashSet<string> FindDependencyCycleAffectedTasks(
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById,
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask,
        CancellationToken cancellationToken)
    {
        var remainingPrerequisiteCount = new Dictionary<string, int>(tasksById.Count, StringComparer.Ordinal);
        var dependentsByTask = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var taskId in tasksById.Keys)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            remainingPrerequisiteCount.Add(
                taskId,
                prerequisitesByTask.TryGetValue(taskId, out var prerequisiteIds)
                    ? prerequisiteIds.Count
                    : 0);
        }

        foreach (var (taskId, prerequisiteIds) in prerequisitesByTask)
        {
            foreach (var prerequisiteId in prerequisiteIds)
            {
                CheckCancellation(ref cancellationCountdown, cancellationToken);
                if (!dependentsByTask.TryGetValue(prerequisiteId, out var dependentIds))
                {
                    dependentIds = [];
                    dependentsByTask.Add(prerequisiteId, dependentIds);
                }

                dependentIds.Add(taskId);
            }
        }

        var ready = new Queue<string>();
        foreach (var (taskId, prerequisiteCount) in remainingPrerequisiteCount)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (prerequisiteCount == 0)
            {
                ready.Enqueue(taskId);
            }
        }

        while (ready.TryDequeue(out var taskId))
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (!dependentsByTask.TryGetValue(taskId, out var dependentIds))
            {
                continue;
            }

            foreach (var dependentId in dependentIds)
            {
                CheckCancellation(ref cancellationCountdown, cancellationToken);
                var remaining = remainingPrerequisiteCount[dependentId] - 1;
                remainingPrerequisiteCount[dependentId] = remaining;
                if (remaining == 0)
                {
                    ready.Enqueue(dependentId);
                }
            }
        }

        var affected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (taskId, remaining) in remainingPrerequisiteCount)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (remaining > 0)
            {
                affected.Add(taskId);
            }
        }

        return affected;
    }

    private static ProjectPlanResourceBindingIndex BuildResourceBindingIndex(
        IReadOnlyList<ProjectPlanResourceBindingFact> bindings,
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById,
        CancellationToken cancellationToken)
    {
        var uniqueBindings = new HashSet<ProjectPlanResourceBindingFact>();
        var groupsByTask = new Dictionary<string, HashSet<ProjectPlanResourceGroup>>(StringComparer.Ordinal);
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var binding in bindings)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (!tasksById.ContainsKey(binding.TaskNodeId) ||
                !IsAssignableResourceGroup(binding.Group) ||
                string.IsNullOrWhiteSpace(binding.ResourceKey) ||
                !uniqueBindings.Add(binding))
            {
                continue;
            }

            if (!groupsByTask.TryGetValue(binding.TaskNodeId, out var groups))
            {
                groups = [];
                groupsByTask.Add(binding.TaskNodeId, groups);
            }

            groups.Add(binding.Group);
        }

        return new ProjectPlanResourceBindingIndex(uniqueBindings, groupsByTask);
    }

    private static (IReadOnlyList<ProjectPlanTaskEvaluation> Evaluations, IReadOnlySet<string> CompletedTaskIds) EvaluateTasks(
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById,
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask,
        IReadOnlyDictionary<string, HashSet<ProjectPlanResourceGroup>> resourceGroupsByTask,
        DateTimeOffset asOfUtc,
        decimal hoursPerManDay,
        CancellationToken cancellationToken)
    {
        var statuses = new Dictionary<string, ProjectPlanNormalizedStatus>(tasksById.Count, StringComparer.Ordinal);
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var task in tasksById.Values)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            var normalizedStatus = NormalizeStatus(task.Status);
            statuses.Add(
                task.NodeId,
                new ProjectPlanNormalizedStatus(
                    normalizedStatus,
                    ResolveTerminalState(task, normalizedStatus)));
        }

        var completedTaskIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (taskId, status) in statuses)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (status.TerminalState == ProjectPlanTaskState.Completed)
            {
                completedTaskIds.Add(taskId);
            }
        }

        var evaluations = new List<ProjectPlanTaskEvaluation>(tasksById.Count);
        foreach (var task in tasksById.Values)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            var estimate = ParseEstimate(task.MetadataJson, hoursPerManDay);
            var progress = ParseProgress(task.ProgressPercent);
            var blockingTaskCount = 0;
            if (prerequisitesByTask.TryGetValue(task.NodeId, out var prerequisiteIds))
            {
                foreach (var prerequisiteId in prerequisiteIds)
                {
                    CheckCancellation(ref cancellationCountdown, cancellationToken);
                    if (!completedTaskIds.Contains(prerequisiteId))
                    {
                        blockingTaskCount++;
                    }
                }
            }

            var normalizedStatus = statuses[task.NodeId];
            var state = normalizedStatus.TerminalState ?? ResolveActiveState(
                task,
                normalizedStatus.Value,
                blockingTaskCount > 0,
                asOfUtc);
            resourceGroupsByTask.TryGetValue(task.NodeId, out var resourceGroups);
            evaluations.Add(new ProjectPlanTaskEvaluation(
                task,
                state,
                progress.Percent,
                progress.IsMissing,
                progress.IsInvalid,
                estimate.Estimate,
                estimate.IsInvalid,
                blockingTaskCount,
                resourceGroups ?? EmptyResourceGroups));
        }

        return (evaluations, completedTaskIds);
    }

    private static ProjectPlanSummary BuildSummary(
        ProjectPlanSnapshot snapshot,
        IReadOnlyList<ProjectPlanTaskEvaluation> evaluations,
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask,
        ProjectPlanResourceBindingIndex resourceBindings,
        int dependencyCycleAffectedTaskCount,
        IReadOnlySet<string> completedTaskIds,
        NormalizedProjectPlanSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var stateCounts = Enum.GetValues<ProjectPlanTaskState>()
            .ToDictionary(state => state, _ => 0);
        var costTotals = new Dictionary<string, (decimal Amount, int TaskCount)>(StringComparer.Ordinal);
        var futureCostTotals = new Dictionary<(ProjectPlanResourceGroup Group, string CurrencyCode), (decimal Amount, int TaskCount)>();
        var futureCostTrend = new Dictionary<(DateOnly Date, ProjectPlanResourceGroup Group, string CurrencyCode), decimal>();
        DateTimeOffset? earliestStartUtc = null;
        DateTimeOffset? latestEndUtc = null;
        decimal scheduledTaskDurationHours = 0m;
        decimal totalExpectedEffortHours = 0m;
        decimal progressTotal = 0m;
        var progressTaskCount = 0;
        decimal effortWeightedProgressTotal = 0m;
        decimal effortWithProgressTotal = 0m;
        var missingScheduleTaskCount = 0;
        var missingEffortTaskCount = 0;
        var missingExpectedCostTaskCount = 0;
        var invalidMetadataTaskCount = 0;
        var unassignedTaskCount = 0;
        var mixedResourceTaskCount = 0;
        var missingProgressTaskCount = 0;
        var invalidProgressTaskCount = 0;
        var invalidScheduleTaskCount = 0;
        var unscheduledFutureExpectedCostTaskCount = 0;
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var evaluation in evaluations)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            stateCounts[evaluation.State]++;
            if (HasValidSchedule(evaluation.Task))
            {
                var taskStartUtc = evaluation.Task.StartUtc!.Value;
                var taskEndUtc = evaluation.Task.EndUtc!.Value;

                if (!earliestStartUtc.HasValue || taskStartUtc < earliestStartUtc.Value)
                {
                    earliestStartUtc = taskStartUtc;
                }
                if (!latestEndUtc.HasValue || taskEndUtc > latestEndUtc.Value)
                {
                    latestEndUtc = taskEndUtc;
                }
                scheduledTaskDurationHours += ToHours(taskEndUtc - taskStartUtc);
            }
            else
            {
                if (evaluation.Task.StartUtc.HasValue && evaluation.Task.EndUtc.HasValue)
                {
                    invalidScheduleTaskCount++;
                }
                else
                {
                    missingScheduleTaskCount++;
                }
            }

            if (evaluation.Estimate.ExpectedEffortHours.HasValue)
            {
                totalExpectedEffortHours += evaluation.Estimate.ExpectedEffortHours.Value;
            }
            else
            {
                missingEffortTaskCount++;
            }

            if (evaluation.Estimate.ExpectedCostAmount.HasValue)
            {
                var currencyCode = evaluation.Estimate.ExpectedCostCurrencyCode;
                var amount = evaluation.Estimate.ExpectedCostAmount.Value;
                costTotals.TryGetValue(currencyCode, out var currentCost);
                costTotals[currencyCode] = (
                    currentCost.Amount + amount,
                    currentCost.TaskCount + 1);

                if (evaluation.State is not (ProjectPlanTaskState.Completed or ProjectPlanTaskState.Cancelled))
                {
                    var remainingAmount = evaluation.ProgressPercent.HasValue
                        ? amount * (100m - evaluation.ProgressPercent.Value) / 100m
                        : amount;
                    if (remainingAmount > 0m)
                    {
                        var resourceGroup = ResolveExpectedCostResourceGroup(evaluation.ResourceGroups);
                        var costKey = (resourceGroup, currencyCode);
                        futureCostTotals.TryGetValue(costKey, out var currentFutureCost);
                        futureCostTotals[costKey] = (
                            currentFutureCost.Amount + remainingAmount,
                            currentFutureCost.TaskCount + 1);

                        var trendDate = ResolveFutureCostTrendDate(
                            evaluation.Task,
                            query.AsOfUtc);
                        if (trendDate.HasValue)
                        {
                            var trendKey = (
                                trendDate.Value,
                                resourceGroup,
                                currencyCode);
                            futureCostTrend.TryGetValue(trendKey, out var currentTrendCost);
                            futureCostTrend[trendKey] = currentTrendCost + remainingAmount;
                        }
                        else
                        {
                            unscheduledFutureExpectedCostTaskCount++;
                        }
                    }
                }
            }
            else
            {
                missingExpectedCostTaskCount++;
            }

            if (evaluation.ProgressPercent.HasValue)
            {
                progressTotal += evaluation.ProgressPercent.Value;
                progressTaskCount++;
                if (evaluation.Estimate.ExpectedEffortHours.HasValue)
                {
                    effortWeightedProgressTotal += evaluation.Estimate.ExpectedEffortHours.Value * evaluation.ProgressPercent.Value;
                    effortWithProgressTotal += evaluation.Estimate.ExpectedEffortHours.Value;
                }
            }
            else
            {
                if (evaluation.IsMissingProgress)
                {
                    missingProgressTaskCount++;
                }
                else if (evaluation.IsInvalidProgress)
                {
                    invalidProgressTaskCount++;
                }
            }

            if (evaluation.IsInvalidMetadata)
            {
                invalidMetadataTaskCount++;
            }
            if (evaluation.ResourceGroups.Count == 0)
            {
                unassignedTaskCount++;
            }
            else if (evaluation.ResourceGroups.Count > 1)
            {
                mixedResourceTaskCount++;
            }
        }

        decimal? deliveryLeadTimeHours = earliestStartUtc.HasValue && latestEndUtc.HasValue && latestEndUtc >= earliestStartUtc
            ? ToHours(latestEndUtc.Value - earliestStartUtc.Value)
            : null;
        var warnings = BuildWarnings(
            evaluations.Count,
            invalidMetadataTaskCount,
            invalidScheduleTaskCount,
            invalidProgressTaskCount,
            dependencyCycleAffectedTaskCount);
        return new ProjectPlanSummary
        {
            ProjectId = snapshot.ProjectId,
            ProjectName = snapshot.ProjectName,
            AsOfUtc = query.AsOfUtc,
            TotalTaskCount = evaluations.Count,
            TaskStates = BuildStateSummaries(stateCounts, evaluations.Count),
            Schedule = new ProjectPlanScheduleSummary(
                earliestStartUtc,
                latestEndUtc,
                deliveryLeadTimeHours,
                scheduledTaskDurationHours),
            TotalExpectedEffortHours = totalExpectedEffortHours,
            TotalExpectedEffortManDays = totalExpectedEffortHours / query.HoursPerManDay,
            TaskWeightedProgressPercent = progressTaskCount == 0
                ? null
                : decimal.Round(progressTotal / progressTaskCount, 2),
            EffortWeightedProgressPercent = effortWithProgressTotal == 0m
                ? null
                : decimal.Round(effortWeightedProgressTotal / effortWithProgressTotal, 2),
            ExpectedCostTotals = costTotals
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ProjectPlanExpectedCostTotal(item.Key, item.Value.Amount, item.Value.TaskCount))
                .ToArray(),
            FutureExpectedCostTotals = futureCostTotals
                .OrderBy(item => item.Key.CurrencyCode, StringComparer.Ordinal)
                .ThenBy(item => item.Key.Group)
                .Select(item => new ProjectPlanExpectedResourceCostTotal(
                    item.Key.Group,
                    item.Key.CurrencyCode,
                    item.Value.Amount,
                    item.Value.TaskCount))
                .ToArray(),
            FutureExpectedCostTrend = futureCostTrend
                .OrderBy(item => item.Key.Date)
                .ThenBy(item => item.Key.CurrencyCode, StringComparer.Ordinal)
                .ThenBy(item => item.Key.Group)
                .Select(item => new ProjectPlanExpectedCostTrendPoint(
                    item.Key.Date,
                    item.Key.Group,
                    item.Key.CurrencyCode,
                    item.Value))
                .ToArray(),
            UnscheduledFutureExpectedCostTaskCount = unscheduledFutureExpectedCostTaskCount,
            ResourceGroups = BuildResourceGroupSummaries(
                evaluations,
                resourceBindings,
                cancellationToken),
            RunningTasks = BuildTaskPreviews(
                evaluations,
                ProjectPlanTaskState.Running,
                query.TaskPreviewLimit,
                prerequisitesByTask,
                completedTaskIds,
                cancellationToken),
            BlockedTasks = BuildTaskPreviews(
                evaluations,
                ProjectPlanTaskState.Blocked,
                query.TaskPreviewLimit,
                prerequisitesByTask,
                completedTaskIds,
                cancellationToken),
            WaitingTasks = BuildTaskPreviews(
                evaluations,
                ProjectPlanTaskState.Waiting,
                query.TaskPreviewLimit,
                prerequisitesByTask,
                completedTaskIds,
                cancellationToken),
            Completeness = new ProjectPlanDataCompleteness(
                missingScheduleTaskCount,
                invalidScheduleTaskCount,
                missingEffortTaskCount,
                missingExpectedCostTaskCount,
                missingProgressTaskCount,
                unassignedTaskCount,
                invalidProgressTaskCount,
                invalidMetadataTaskCount,
                mixedResourceTaskCount,
                dependencyCycleAffectedTaskCount),
            Warnings = warnings
        };
    }

    private static ProjectPlanResourceGroup ResolveExpectedCostResourceGroup(
        IReadOnlyCollection<ProjectPlanResourceGroup> resourceGroups)
    {
        return resourceGroups.Count switch
        {
            0 => ProjectPlanResourceGroup.Unassigned,
            1 => resourceGroups.First(),
            _ => ProjectPlanResourceGroup.Mixed
        };
    }

    private static IReadOnlyList<ProjectPlanTaskStateSummary> BuildStateSummaries(
        IReadOnlyDictionary<ProjectPlanTaskState, int> stateCounts,
        int totalTaskCount)
    {
        var summaries = new List<ProjectPlanTaskStateSummary>(stateCounts.Count);
        foreach (var state in Enum.GetValues<ProjectPlanTaskState>())
        {
            var count = stateCounts[state];
            summaries.Add(new ProjectPlanTaskStateSummary(
                state,
                count,
                Percentage(count, totalTaskCount)));
        }
        return summaries;
    }

    private static IReadOnlyList<ProjectPlanResourceGroupSummary> BuildResourceGroupSummaries(
        IReadOnlyList<ProjectPlanTaskEvaluation> evaluations,
        ProjectPlanResourceBindingIndex bindingIndex,
        CancellationToken cancellationToken)
    {
        var totalBindings = bindingIndex.UniqueBindings.Count;
        var totalTasks = evaluations.Count;
        var summaries = new List<ProjectPlanResourceGroupSummary>();
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var group in Enum.GetValues<ProjectPlanResourceGroup>())
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            var bindingCount = 0;
            var coveredTaskCount = 0;
            var exclusiveTaskCount = 0;
            if (IsAssignableResourceGroup(group))
            {
                foreach (var binding in bindingIndex.UniqueBindings)
                {
                    CheckCancellation(ref cancellationCountdown, cancellationToken);
                    if (binding.Group == group)
                    {
                        bindingCount++;
                    }
                }
                foreach (var evaluation in evaluations)
                {
                    CheckCancellation(ref cancellationCountdown, cancellationToken);
                    if (evaluation.ResourceGroups.Contains(group))
                    {
                        coveredTaskCount++;
                        if (evaluation.ResourceGroups.Count == 1)
                        {
                            exclusiveTaskCount++;
                        }
                    }
                }
            }
            else if (group == ProjectPlanResourceGroup.Unassigned)
            {
                foreach (var evaluation in evaluations)
                {
                    CheckCancellation(ref cancellationCountdown, cancellationToken);
                    if (evaluation.ResourceGroups.Count == 0)
                    {
                        coveredTaskCount++;
                    }
                }
                exclusiveTaskCount = coveredTaskCount;
            }
            else
            {
                foreach (var evaluation in evaluations)
                {
                    CheckCancellation(ref cancellationCountdown, cancellationToken);
                    if (evaluation.ResourceGroups.Count > 1)
                    {
                        coveredTaskCount++;
                    }
                }
                exclusiveTaskCount = coveredTaskCount;
            }

            summaries.Add(new ProjectPlanResourceGroupSummary(
                group,
                bindingCount,
                Percentage(bindingCount, totalBindings),
                coveredTaskCount,
                Percentage(coveredTaskCount, totalTasks),
                exclusiveTaskCount));
        }

        return summaries;
    }

    private static IReadOnlyList<ProjectPlanTaskPreview> BuildTaskPreviews(
        IReadOnlyList<ProjectPlanTaskEvaluation> evaluations,
        ProjectPlanTaskState state,
        int limit,
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask,
        IReadOnlySet<string> completedTaskIds,
        CancellationToken cancellationToken)
    {
        if (limit == 0)
        {
            return [];
        }

        var matchingEvaluations = new List<ProjectPlanTaskEvaluation>();
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var evaluation in evaluations)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (evaluation.State == state)
            {
                matchingEvaluations.Add(evaluation);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var previews = matchingEvaluations
            .OrderBy(item => item.Task.StartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Task.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Task.NodeId, StringComparer.Ordinal)
            .Take(limit)
            .Select(item => new ProjectPlanTaskPreview(
                item.Task.NodeId,
                item.Task.Title,
                item.State,
                item.Task.Status,
                item.Task.StartUtc,
                item.Task.EndUtc,
                item.ProgressPercent,
                item.Estimate.ExpectedEffortHours,
                item.Estimate.ExpectedCostAmount,
                item.Estimate.ExpectedCostCurrencyCode,
                item.BlockingTaskCount,
                BuildBlockingTaskIdPreview(item, prerequisitesByTask, completedTaskIds),
                ResolvePreviewResourceGroups(item.ResourceGroups)))
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return previews;
    }

    private static IReadOnlyList<string> BuildBlockingTaskIdPreview(
        ProjectPlanTaskEvaluation evaluation,
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask,
        IReadOnlySet<string> completedTaskIds)
    {
        if (evaluation.BlockingTaskCount == 0 ||
            !prerequisitesByTask.TryGetValue(evaluation.Task.NodeId, out var prerequisiteIds))
        {
            return [];
        }

        var blockingTaskIds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var prerequisiteId in prerequisiteIds)
        {
            if (completedTaskIds.Contains(prerequisiteId))
            {
                continue;
            }

            blockingTaskIds.Add(prerequisiteId);
            if (blockingTaskIds.Count > MaximumBlockingTaskIdPreview)
            {
                blockingTaskIds.Remove(blockingTaskIds.Max!);
            }
        }

        return blockingTaskIds.ToArray();
    }

    private static IReadOnlyList<ProjectPlanResourceGroup> ResolvePreviewResourceGroups(
        IReadOnlySet<ProjectPlanResourceGroup> groups)
    {
        return groups.Count == 0
            ? [ProjectPlanResourceGroup.Unassigned]
            : groups.Order().ToArray();
    }

    private static ProjectPlanTaskState? ResolveTerminalState(
        ProjectPlanTaskFact task,
        string normalizedStatus)
    {
        if (normalizedStatus is "cancelled" or "canceled" or "archived" or "rejected" or "skipped")
        {
            return ProjectPlanTaskState.Cancelled;
        }

        if (task.ProgressPercent == 100)
        {
            return ProjectPlanTaskState.Completed;
        }

        return normalizedStatus switch
        {
            "complete" or "completed" or "done" or "delivered" or "closed" or "finished" => ProjectPlanTaskState.Completed,
            _ => null
        };
    }

    private static ProjectPlanTaskState ResolveActiveState(
        ProjectPlanTaskFact task,
        string normalizedStatus,
        bool isBlocked,
        DateTimeOffset asOfUtc)
    {
        if (isBlocked || normalizedStatus is "blocked" or "impeded")
        {
            return ProjectPlanTaskState.Blocked;
        }

        if (normalizedStatus is "waiting" or "on hold" or "on-hold" or "paused" or "stopped")
        {
            return ProjectPlanTaskState.Waiting;
        }

        if (normalizedStatus is "running" or "in progress" or "in-progress" or "active" or "started" ||
            task.ProgressPercent is > 0 and < 100 ||
            task.StartUtc <= asOfUtc && task.EndUtc > asOfUtc)
        {
            return ProjectPlanTaskState.Running;
        }

        if (!task.StartUtc.HasValue || !task.EndUtc.HasValue)
        {
            return ProjectPlanTaskState.Unscheduled;
        }

        return task.StartUtc <= asOfUtc
            ? ProjectPlanTaskState.Ready
            : ProjectPlanTaskState.Planned;
    }

    private static ProjectPlanEstimateParseResult ParseEstimate(string metadataJson, decimal hoursPerManDay)
    {
        try
        {
            var workItem = ProjectObjectMetadataSerializer.Parse(metadataJson).WorkItem;
            if (workItem is null)
            {
                return new ProjectPlanEstimateParseResult(ProjectTaskEstimate.Empty(), false);
            }

            var estimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(
                new ProjectTaskEstimate(
                    workItem.ExpectedEffortHours,
                    workItem.ExpectedEffortUnit,
                    workItem.ExpectedCostAmount,
                    workItem.ExpectedCostCurrencyCode),
                hoursPerManDay);
            return new ProjectPlanEstimateParseResult(estimate, false);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentOutOfRangeException or OverflowException)
        {
            return new ProjectPlanEstimateParseResult(ProjectTaskEstimate.Empty(), true);
        }
    }

    private static ProjectPlanScheduleCalculation CalculateSchedule(
        IReadOnlyList<IProjectPlanScheduleFact> tasks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset? earliestStartUtc = null;
        DateTimeOffset? latestEndUtc = null;
        decimal scheduledTaskDurationHours = 0m;
        var missingTaskCount = 0;
        var invalidTaskCount = 0;
        var cancellationCountdown = CancellationCheckInterval;
        foreach (var task in tasks)
        {
            CheckCancellation(ref cancellationCountdown, cancellationToken);
            if (!task.StartUtc.HasValue || !task.EndUtc.HasValue)
            {
                missingTaskCount++;
                continue;
            }

            if (task.EndUtc.Value < task.StartUtc.Value)
            {
                invalidTaskCount++;
                continue;
            }

            if (!earliestStartUtc.HasValue || task.StartUtc.Value < earliestStartUtc.Value)
            {
                earliestStartUtc = task.StartUtc.Value;
            }
            if (!latestEndUtc.HasValue || task.EndUtc.Value > latestEndUtc.Value)
            {
                latestEndUtc = task.EndUtc.Value;
            }

            scheduledTaskDurationHours += ToHours(task.EndUtc.Value - task.StartUtc.Value);
        }

        decimal? deliveryLeadTimeHours =
            earliestStartUtc.HasValue &&
            latestEndUtc.HasValue &&
            latestEndUtc.Value >= earliestStartUtc.Value
                ? ToHours(latestEndUtc.Value - earliestStartUtc.Value)
                : null;
        return new ProjectPlanScheduleCalculation(
            new ProjectPlanScheduleSummary(
                earliestStartUtc,
                latestEndUtc,
                deliveryLeadTimeHours,
                scheduledTaskDurationHours),
            missingTaskCount,
            invalidTaskCount);
    }

    private static IReadOnlyList<string> BuildManagerWarnings(
        int missingScheduleTaskCount,
        int invalidScheduleTaskCount,
        int invalidMetadataTaskCount,
        int missingExpectedCostTaskCount,
        int missingProgressTaskCount,
        int invalidProgressTaskCount)
    {
        var warnings = new List<string>(6);
        if (missingScheduleTaskCount > 0)
        {
            warnings.Add(
                $"{missingScheduleTaskCount} task(s) have an incomplete schedule and were excluded from schedule totals.");
        }
        if (invalidScheduleTaskCount > 0)
        {
            warnings.Add(
                $"{invalidScheduleTaskCount} task(s) end before they start and were excluded from schedule totals.");
        }
        if (invalidMetadataTaskCount > 0)
        {
            warnings.Add(
                $"{invalidMetadataTaskCount} open task(s) contain invalid estimate metadata and were excluded from remaining-cost totals.");
        }
        if (missingExpectedCostTaskCount > 0)
        {
            warnings.Add(
                $"{missingExpectedCostTaskCount} open task(s) have no expected cost and are not represented in the remaining-cost forecast.");
        }
        if (missingProgressTaskCount > 0)
        {
            warnings.Add(
                $"{missingProgressTaskCount} priced open task(s) have no tracked progress, so their full expected cost remains in the forecast.");
        }
        if (invalidProgressTaskCount > 0)
        {
            warnings.Add(
                $"{invalidProgressTaskCount} priced open task(s) have progress outside the supported 0-100 range, so their full expected cost remains in the forecast.");
        }

        return warnings;
    }

    private static IReadOnlyList<string> BuildWarnings(
        int taskCount,
        int invalidMetadataTaskCount,
        int invalidScheduleTaskCount,
        int invalidProgressTaskCount,
        int dependencyCycleAffectedTaskCount)
    {
        var warnings = new List<string>(6);
        if (taskCount > 0)
        {
            warnings.Add("Task lifecycle states are inferred from free-text status, progress, schedule, and explicit task dependencies until typed lifecycle persistence is introduced.");
            warnings.Add("Expected-cost totals include only explicit task estimates, never combine currencies, and do not yet derive prices from person rates or agent model-token usage.");
        }
        if (invalidMetadataTaskCount > 0)
        {
            warnings.Add($"{invalidMetadataTaskCount} task(s) contain invalid estimate metadata and were excluded from effort and expected-cost totals.");
        }
        if (invalidScheduleTaskCount > 0)
        {
            warnings.Add($"{invalidScheduleTaskCount} task(s) end before they start and were excluded from scheduled-duration totals.");
        }
        if (invalidProgressTaskCount > 0)
        {
            warnings.Add($"{invalidProgressTaskCount} task(s) have progress outside the supported 0-100 range and were excluded from progress aggregates.");
        }
        if (dependencyCycleAffectedTaskCount > 0)
        {
            warnings.Add($"{dependencyCycleAffectedTaskCount} task(s) are in or downstream from an explicit dependency cycle; unresolved prerequisites determine whether those tasks are currently blocked.");
        }
        return warnings;
    }

    private static NormalizedProjectPlanSummaryQuery NormalizeQuery(ProjectPlanSummaryQuery? query)
    {
        var value = query ?? new ProjectPlanSummaryQuery();
        if (value.TaskPreviewLimit is < 0 or > MaximumTaskPreviewLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                value.TaskPreviewLimit,
                $"Task preview limit must be between 0 and {MaximumTaskPreviewLimit}.");
        }

        if (value.HoursPerManDay is < MinimumHoursPerManDay or > MaximumHoursPerManDay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                value.HoursPerManDay,
                $"Hours per man-day must be between {MinimumHoursPerManDay} and {MaximumHoursPerManDay}.");
        }
        ProjectTaskEstimatePolicy.ToHours(1m, ProjectWorkItemEffortUnit.Hours, value.HoursPerManDay);
        return new NormalizedProjectPlanSummaryQuery(
            (value.AsOfUtc ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            value.TaskPreviewLimit,
            value.HoursPerManDay);
    }

    private static NormalizedProjectPlanManagerSummaryQuery NormalizeManagerQuery(
        ProjectPlanManagerSummaryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Enum.IsDefined(query.Mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                query.Mode,
                "The manager plan summary mode is not supported.");
        }

        return new NormalizedProjectPlanManagerSummaryQuery(
            query.Mode,
            (query.AsOfUtc ?? DateTimeOffset.UtcNow).ToUniversalTime());
    }

    private static ProjectPlanProgressParseResult ParseProgress(int progressPercent)
    {
        if (progressPercent == ProjectProgressPolicy.UntrackedPercent)
        {
            return new ProjectPlanProgressParseResult(null, IsMissing: true, IsInvalid: false);
        }

        return ProjectProgressPolicy.IsTrackedPercent(progressPercent)
            ? new ProjectPlanProgressParseResult(progressPercent, IsMissing: false, IsInvalid: false)
            : new ProjectPlanProgressParseResult(null, IsMissing: false, IsInvalid: true);
    }

    private static DateOnly? ResolveFutureCostTrendDate(
        IProjectPlanScheduleFact task,
        DateTimeOffset asOfUtc)
    {
        if (task.StartUtc is not { } startUtc)
        {
            return null;
        }

        var fallbackUtc = startUtc > asOfUtc
            ? startUtc
            : asOfUtc;
        var forecastUtc = task.EndUtc is { } endUtc &&
                          endUtc >= startUtc &&
                          endUtc > fallbackUtc
            ? endUtc
            : fallbackUtc;
        return DateOnly.FromDateTime(forecastUtc.UtcDateTime);
    }

    private static bool HasValidSchedule(ProjectPlanTaskFact task)
    {
        return task.StartUtc.HasValue && task.EndUtc.HasValue && task.EndUtc >= task.StartUtc;
    }

    private static decimal ToHours(TimeSpan value)
    {
        return (decimal)value.Ticks / TimeSpan.TicksPerHour;
    }

    private static decimal Percentage(int value, int total)
    {
        return total == 0 ? 0m : decimal.Round(value * 100m / total, 2);
    }

    private static string NormalizeStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static bool IsAssignableResourceGroup(ProjectPlanResourceGroup group)
    {
        return group is ProjectPlanResourceGroup.Person or
            ProjectPlanResourceGroup.Agent or
            ProjectPlanResourceGroup.Workflow or
            ProjectPlanResourceGroup.Process or
            ProjectPlanResourceGroup.External;
    }

    private static void CheckCancellation(
        ref int countdown,
        CancellationToken cancellationToken)
    {
        countdown--;
        if (countdown > 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        countdown = CancellationCheckInterval;
    }

    private sealed record ProjectPlanTaskEvaluation(
        ProjectPlanTaskFact Task,
        ProjectPlanTaskState State,
        int? ProgressPercent,
        bool IsMissingProgress,
        bool IsInvalidProgress,
        ProjectTaskEstimate Estimate,
        bool IsInvalidMetadata,
        int BlockingTaskCount,
        IReadOnlySet<ProjectPlanResourceGroup> ResourceGroups);

    private sealed record ProjectPlanResourceBindingIndex(
        IReadOnlySet<ProjectPlanResourceBindingFact> UniqueBindings,
        IReadOnlyDictionary<string, HashSet<ProjectPlanResourceGroup>> GroupsByTask);

    private readonly record struct ProjectPlanEstimateParseResult(ProjectTaskEstimate Estimate, bool IsInvalid);

    private readonly record struct ProjectPlanProgressParseResult(
        int? Percent,
        bool IsMissing,
        bool IsInvalid);

    private readonly record struct ProjectPlanScheduleCalculation(
        ProjectPlanScheduleSummary Summary,
        int MissingTaskCount,
        int InvalidTaskCount);

    private readonly record struct ProjectPlanNormalizedStatus(
        string Value,
        ProjectPlanTaskState? TerminalState);

    private sealed record NormalizedProjectPlanSummaryQuery(
        DateTimeOffset AsOfUtc,
        int TaskPreviewLimit,
        decimal HoursPerManDay);

    private sealed record NormalizedProjectPlanManagerSummaryQuery(
        ProjectPlanManagerSummaryMode Mode,
        DateTimeOffset AsOfUtc);
}
