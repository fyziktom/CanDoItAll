using CanDoItAll.Components.Gantt;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal static class ProjectStructureGanttTaskMetricProjection
{
    internal static ProjectStructureGanttTaskMetrics Build(
        ProjectStructureNode node,
        ProjectStructureGanttProjectionOptions options,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        var taskId = new GanttTaskId(node.Id);
        var progressPercent = BuildProgressPercent(node.ProgressPercent, taskId, issues);
        ProjectWorkItemMetadata? workItem;
        try
        {
            workItem = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).WorkItem;
        }
        catch (InvalidOperationException)
        {
            issues.Add(Warning(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskEstimate,
                $"Task '{taskId}' has invalid metadata; its expected effort and cost are omitted.",
                taskId));
            return ProjectStructureGanttTaskMetrics.Empty(progressPercent);
        }

        if (workItem is null)
        {
            return ProjectStructureGanttTaskMetrics.Empty(progressPercent);
        }

        try
        {
            var estimate = ProjectTaskEstimatePolicy.ValidateAndNormalize(
                new ProjectTaskEstimate(
                    workItem.ExpectedEffortHours,
                    workItem.ExpectedEffortUnit,
                    workItem.ExpectedCostAmount,
                    workItem.ExpectedCostCurrencyCode),
                options.HoursPerManDay);
            TimeSpan? expectedEffort = estimate.ExpectedEffortHours.HasValue
                ? TimeSpan.FromHours((double)estimate.ExpectedEffortHours.Value)
                : null;
            return new ProjectStructureGanttTaskMetrics(
                progressPercent,
                expectedEffort,
                estimate.ExpectedCostAmount,
                estimate.ExpectedCostCurrencyCode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException or OverflowException)
        {
            issues.Add(Warning(
                ProjectStructureGanttProjectionIssueCode.InvalidTaskEstimate,
                $"Task '{taskId}' has an invalid expected effort or cost: {exception.Message}",
                taskId));
            return ProjectStructureGanttTaskMetrics.Empty(progressPercent);
        }
    }

    internal static IReadOnlyList<ProjectStructureGanttExpectedCostTotal> BuildExpectedCostTotals(
        IEnumerable<ProjectStructureGanttTaskMetrics> metrics)
    {
        return metrics
            .Where(static item => item.ExpectedCostAmount.HasValue)
            .GroupBy(static item => item.ExpectedCostCurrencyCode, StringComparer.Ordinal)
            .Select(group => new ProjectStructureGanttExpectedCostTotal(
                group.Key,
                group.Sum(item => item.ExpectedCostAmount!.Value)))
            .OrderBy(static total => total.CurrencyCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static int? BuildProgressPercent(
        int progressPercent,
        GanttTaskId taskId,
        ICollection<ProjectStructureGanttProjectionIssue> issues)
    {
        if (progressPercent == -1)
        {
            return null;
        }

        if (progressPercent is >= 0 and <= 100)
        {
            return progressPercent;
        }

        issues.Add(Warning(
            ProjectStructureGanttProjectionIssueCode.InvalidTaskProgress,
            $"Task '{taskId}' has progress outside the supported 0-100 range; its progress decoration is omitted.",
            taskId));
        return null;
    }

    private static ProjectStructureGanttProjectionIssue Warning(
        ProjectStructureGanttProjectionIssueCode code,
        string message,
        GanttTaskId taskId)
    {
        return new ProjectStructureGanttProjectionIssue(
            code,
            ProjectStructureGanttProjectionIssueSeverity.Warning,
            message,
            taskId);
    }
}

internal sealed record ProjectStructureGanttTaskMetrics(
    int? ProgressPercent,
    TimeSpan? ExpectedEffort,
    decimal? ExpectedCostAmount,
    string ExpectedCostCurrencyCode)
{
    internal static ProjectStructureGanttTaskMetrics Empty(int? progressPercent)
        => new(progressPercent, null, null, string.Empty);
}
