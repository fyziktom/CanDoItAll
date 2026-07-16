using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectPlanTaskFact(
    string NodeId,
    string Title,
    string Status,
    int ProgressPercent,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string MetadataJson);

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

public sealed class ProjectPlanSummaryCalculator
{
    public const int MaximumTaskPreviewLimit = 100;
    public const int MaximumBlockingTaskIdPreview = 20;
    public const decimal MinimumHoursPerManDay = 1m;
    public const decimal MaximumHoursPerManDay = 24m;

    private static readonly IReadOnlySet<ProjectPlanResourceGroup> EmptyResourceGroups =
        new HashSet<ProjectPlanResourceGroup>();
    internal ProjectPlanSummary Build(
        ProjectPlanSnapshot snapshot,
        ProjectPlanSummaryQuery? query = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalizedQuery = NormalizeQuery(query);
        var tasksById = BuildTaskIndex(snapshot.Tasks);
        var prerequisitesByTask = BuildPrerequisiteIndex(snapshot.Links, tasksById);
        var dependencyCycleAffectedTaskIds = FindDependencyCycleAffectedTasks(tasksById, prerequisitesByTask);
        var resourceBindings = BuildResourceBindingIndex(snapshot.ResourceBindings, tasksById);
        var (evaluations, completedTaskIds) = EvaluateTasks(
            tasksById,
            prerequisitesByTask,
            resourceBindings.GroupsByTask,
            normalizedQuery.AsOfUtc,
            normalizedQuery.HoursPerManDay);

        return BuildSummary(
            snapshot,
            evaluations,
            prerequisitesByTask,
            resourceBindings,
            dependencyCycleAffectedTaskIds.Count,
            completedTaskIds,
            normalizedQuery);
    }

    internal static void ValidateQuery(ProjectPlanSummaryQuery? query)
    {
        _ = NormalizeQuery(query);
    }

    private static Dictionary<string, ProjectPlanTaskFact> BuildTaskIndex(
        IReadOnlyList<ProjectPlanTaskFact> tasks)
    {
        var tasksById = new Dictionary<string, ProjectPlanTaskFact>(tasks.Count, StringComparer.Ordinal);
        foreach (var task in tasks)
        {
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
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById)
    {
        var prerequisitesByTask = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var link in links)
        {
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
        IReadOnlyDictionary<string, HashSet<string>> prerequisitesByTask)
    {
        var remainingPrerequisiteCount = new Dictionary<string, int>(tasksById.Count, StringComparer.Ordinal);
        var dependentsByTask = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var taskId in tasksById.Keys)
        {
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
            if (prerequisiteCount == 0)
            {
                ready.Enqueue(taskId);
            }
        }

        while (ready.TryDequeue(out var taskId))
        {
            if (!dependentsByTask.TryGetValue(taskId, out var dependentIds))
            {
                continue;
            }

            foreach (var dependentId in dependentIds)
            {
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
            if (remaining > 0)
            {
                affected.Add(taskId);
            }
        }

        return affected;
    }

    private static ProjectPlanResourceBindingIndex BuildResourceBindingIndex(
        IReadOnlyList<ProjectPlanResourceBindingFact> bindings,
        IReadOnlyDictionary<string, ProjectPlanTaskFact> tasksById)
    {
        var uniqueBindings = new HashSet<ProjectPlanResourceBindingFact>();
        var groupsByTask = new Dictionary<string, HashSet<ProjectPlanResourceGroup>>(StringComparer.Ordinal);

        foreach (var binding in bindings)
        {
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
        decimal hoursPerManDay)
    {
        var statuses = new Dictionary<string, ProjectPlanNormalizedStatus>(tasksById.Count, StringComparer.Ordinal);
        foreach (var task in tasksById.Values)
        {
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
            if (status.TerminalState == ProjectPlanTaskState.Completed)
            {
                completedTaskIds.Add(taskId);
            }
        }

        var evaluations = new List<ProjectPlanTaskEvaluation>(tasksById.Count);
        foreach (var task in tasksById.Values)
        {
            var estimate = ParseEstimate(task.MetadataJson, hoursPerManDay);
            var progress = ParseProgress(task.ProgressPercent);
            var blockingTaskCount = 0;
            if (prerequisitesByTask.TryGetValue(task.NodeId, out var prerequisiteIds))
            {
                foreach (var prerequisiteId in prerequisiteIds)
                {
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
        NormalizedProjectPlanSummaryQuery query)
    {
        var stateCounts = Enum.GetValues<ProjectPlanTaskState>()
            .ToDictionary(state => state, _ => 0);
        var costTotals = new Dictionary<string, (decimal Amount, int TaskCount)>(StringComparer.Ordinal);
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

        foreach (var evaluation in evaluations)
        {
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
                costTotals.TryGetValue(currencyCode, out var currentCost);
                costTotals[currencyCode] = (
                    currentCost.Amount + evaluation.Estimate.ExpectedCostAmount.Value,
                    currentCost.TaskCount + 1);
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
            ResourceGroups = BuildResourceGroupSummaries(evaluations, resourceBindings),
            RunningTasks = BuildTaskPreviews(evaluations, ProjectPlanTaskState.Running, query.TaskPreviewLimit, prerequisitesByTask, completedTaskIds),
            BlockedTasks = BuildTaskPreviews(evaluations, ProjectPlanTaskState.Blocked, query.TaskPreviewLimit, prerequisitesByTask, completedTaskIds),
            WaitingTasks = BuildTaskPreviews(evaluations, ProjectPlanTaskState.Waiting, query.TaskPreviewLimit, prerequisitesByTask, completedTaskIds),
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
        ProjectPlanResourceBindingIndex bindingIndex)
    {
        var totalBindings = bindingIndex.UniqueBindings.Count;
        var totalTasks = evaluations.Count;
        var summaries = new List<ProjectPlanResourceGroupSummary>();
        foreach (var group in Enum.GetValues<ProjectPlanResourceGroup>())
        {
            var bindingCount = 0;
            var coveredTaskCount = 0;
            var exclusiveTaskCount = 0;
            if (IsAssignableResourceGroup(group))
            {
                foreach (var binding in bindingIndex.UniqueBindings)
                {
                    if (binding.Group == group)
                    {
                        bindingCount++;
                    }
                }
                foreach (var evaluation in evaluations)
                {
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
                coveredTaskCount = evaluations.Count(item => item.ResourceGroups.Count == 0);
                exclusiveTaskCount = coveredTaskCount;
            }
            else
            {
                coveredTaskCount = evaluations.Count(item => item.ResourceGroups.Count > 1);
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
        IReadOnlySet<string> completedTaskIds)
    {
        if (limit == 0)
        {
            return [];
        }

        return evaluations
            .Where(item => item.State == state)
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
            ProjectPlanResourceGroup.Process;
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

    private readonly record struct ProjectPlanNormalizedStatus(
        string Value,
        ProjectPlanTaskState? TerminalState);

    private sealed record NormalizedProjectPlanSummaryQuery(
        DateTimeOffset AsOfUtc,
        int TaskPreviewLimit,
        decimal HoursPerManDay);
}
