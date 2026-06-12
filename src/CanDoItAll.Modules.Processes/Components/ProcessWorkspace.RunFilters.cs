namespace CanDoItAll.Modules.Processes;

public partial class ProcessWorkspace
{
    public enum ProcessRunUpdatedTimeFilter
    {
        All,
        Last24Hours,
        Last7Days,
        Last30Days,
        OlderThan30Days
    }

    public enum ProcessImprovementSignalFilter
    {
        All,
        TrainingOpportunity,
        GovernanceReview
    }

    public sealed record ProcessWorkspaceTagViewModel(string Text, string Tone);

    private static readonly IReadOnlyList<ProcessRunStatus> ProcessRunStatusFilterOptions = Enum.GetValues<ProcessRunStatus>();
    private static readonly IReadOnlyList<ProcessOperatingMode> ProcessOperatingModeFilterOptions = Enum.GetValues<ProcessOperatingMode>();
    private static readonly IReadOnlyList<ProcessRunUpdatedTimeFilter> ProcessRunUpdatedTimeFilterOptions = Enum.GetValues<ProcessRunUpdatedTimeFilter>();
    private static readonly IReadOnlyList<ProcessImprovementStatus> ProcessImprovementStatusFilterOptions = Enum.GetValues<ProcessImprovementStatus>();
    private static readonly IReadOnlyList<ProcessImprovementSignalFilter> ProcessImprovementSignalFilterOptions = Enum.GetValues<ProcessImprovementSignalFilter>();

    private sealed class ProcessRunListFilterState
    {
        public string Search { get; set; } = string.Empty;

        public ProcessRunStatus? Status { get; set; }

        public ProcessOperatingMode? OperatingMode { get; set; }

        public ProcessRunUpdatedTimeFilter UpdatedTime { get; set; }

        public string Tag { get; set; } = string.Empty;

        public void Clear()
        {
            Search = string.Empty;
            Status = null;
            OperatingMode = null;
            UpdatedTime = ProcessRunUpdatedTimeFilter.All;
            Tag = string.Empty;
        }
    }

    private sealed class ProcessImprovementFilterState
    {
        public string Search { get; set; } = string.Empty;

        public ProcessImprovementStatus? Status { get; set; }

        public ProcessImprovementSignalFilter Signal { get; set; }

        public void Clear()
        {
            Search = string.Empty;
            Status = null;
            Signal = ProcessImprovementSignalFilter.All;
        }
    }

    private IReadOnlyList<ProcessRunListItem> FilterRuns(ProcessRunListFilterState filter)
    {
        var now = DateTimeOffset.UtcNow;
        return runs
            .Where(run => MatchesRunFilter(run, filter, now))
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ToList();
    }

    private IReadOnlyList<ProcessImprovementViewModel> FilterImprovements(ProcessImprovementFilterState filter)
    {
        return improvements
            .Where(improvement => MatchesImprovementFilter(improvement, filter))
            .OrderBy(improvement => improvement.Status)
            .ThenBy(improvement => improvement.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(improvement => improvement.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesRunFilter(
        ProcessRunListItem run,
        ProcessRunListFilterState filter,
        DateTimeOffset now)
    {
        if (filter.Status.HasValue && run.Status != filter.Status.Value)
        {
            return false;
        }

        if (filter.OperatingMode.HasValue && run.OperatingMode != filter.OperatingMode.Value)
        {
            return false;
        }

        if (!MatchesRunUpdatedTime(run, filter.UpdatedTime, now))
        {
            return false;
        }

        var search = NormalizeFilterText(filter.Search);
        if (!string.IsNullOrWhiteSpace(search) &&
            !MatchesRunSearch(run, search))
        {
            return false;
        }

        var tag = NormalizeFilterText(filter.Tag);
        if (!string.IsNullOrWhiteSpace(tag) &&
            !BuildRunTags(run).Any(item => ContainsFilterText(item.Text, tag)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesRunUpdatedTime(
        ProcessRunListItem run,
        ProcessRunUpdatedTimeFilter filter,
        DateTimeOffset now)
    {
        return filter switch
        {
            ProcessRunUpdatedTimeFilter.Last24Hours => run.UpdatedAtUtc >= now.AddHours(-24),
            ProcessRunUpdatedTimeFilter.Last7Days => run.UpdatedAtUtc >= now.AddDays(-7),
            ProcessRunUpdatedTimeFilter.Last30Days => run.UpdatedAtUtc >= now.AddDays(-30),
            ProcessRunUpdatedTimeFilter.OlderThan30Days => run.UpdatedAtUtc < now.AddDays(-30),
            _ => true
        };
    }

    private static bool MatchesRunSearch(ProcessRunListItem run, string search)
    {
        return ContainsFilterText(run.Name, search) ||
               ContainsFilterText(run.Status.ToString(), search) ||
               ContainsFilterText(run.OperatingMode.ToString(), search) ||
               ContainsFilterText(BuildRunSummary(run), search) ||
               BuildRunTags(run).Any(item => ContainsFilterText(item.Text, search));
    }

    private static bool MatchesImprovementFilter(
        ProcessImprovementViewModel improvement,
        ProcessImprovementFilterState filter)
    {
        if (filter.Status.HasValue && improvement.Status != filter.Status.Value)
        {
            return false;
        }

        if (filter.Signal == ProcessImprovementSignalFilter.TrainingOpportunity &&
            !improvement.IsTrainingOpportunity)
        {
            return false;
        }

        if (filter.Signal == ProcessImprovementSignalFilter.GovernanceReview &&
            !improvement.RequiresGovernanceReview)
        {
            return false;
        }

        var search = NormalizeFilterText(filter.Search);
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return ContainsFilterText(improvement.Title, search) ||
               ContainsFilterText(improvement.Category, search) ||
               ContainsFilterText(improvement.Status.ToString(), search) ||
               ContainsFilterText(improvement.ProblemSummary, search) ||
               BuildImprovementTags(improvement).Any(item => ContainsFilterText(item.Text, search));
    }

    private static IReadOnlyList<ProcessWorkspaceTagViewModel> BuildRunTags(ProcessRunListItem run)
    {
        var tags = new List<ProcessWorkspaceTagViewModel>
        {
            new(run.Status.ToString(), ResolveRunTone(run.Status)),
            new(run.OperatingMode.ToString(), "neutral")
        };

        if (run.Status == ProcessRunStatus.Active)
        {
            tags.Add(new("Running now", "info"));
        }

        if (run.HierarchyDepth > 0)
        {
            tags.Add(new($"Subprocess depth {run.HierarchyDepth}", "info"));
        }

        if (!string.IsNullOrWhiteSpace(run.ManagerAgentName))
        {
            tags.Add(new($"Manager {run.ManagerAgentName}", "neutral"));
        }

        if (run.BlockedStepCount > 0)
        {
            tags.Add(new($"{run.BlockedStepCount} blocked", "warning"));
        }

        if (run.CapabilityGapCount > 0)
        {
            tags.Add(new($"{run.CapabilityGapCount} gap", "danger"));
        }

        if (run.TotalStepCount > 0 && run.CompletedStepCount >= run.TotalStepCount)
        {
            tags.Add(new("All steps done", "success"));
        }

        if (run.EstimatedCost > 0 && run.ActualCost > run.EstimatedCost)
        {
            tags.Add(new("Over estimate", "warning"));
        }

        return tags;
    }

    private static IReadOnlyList<ProcessWorkspaceTagViewModel> BuildImprovementTags(ProcessImprovementViewModel improvement)
    {
        var tags = new List<ProcessWorkspaceTagViewModel>
        {
            new(improvement.Status.ToString(), ResolveImprovementTone(improvement.Status)),
            new(improvement.Category, "neutral")
        };

        if (improvement.IsTrainingOpportunity)
        {
            tags.Add(new("Training", "info"));
        }

        if (improvement.RequiresGovernanceReview)
        {
            tags.Add(new("Governance review", "warning"));
        }

        return tags;
    }

    private static string BuildRunUpdatedText(ProcessRunListItem run)
    {
        var ageText = FormatRunAge(DateTimeOffset.UtcNow - run.UpdatedAtUtc);
        return $"Updated {run.UpdatedAtUtc.LocalDateTime:g} / {ageText}";
    }

    private static string BuildRunCostText(ProcessRunListItem run)
    {
        if (run.DescendantRunCount == 0)
        {
            return $"{run.EstimatedCost:C} estimated / {run.ActualCost:C} actual";
        }

        return $"{run.TreeEstimatedCost:C} estimated total / {run.TreeActualCost:C} actual total ({run.ActualCost:C} own run)";
    }

    private static string BuildRunFilterResultText(int visibleCount, int totalCount)
    {
        if (visibleCount == totalCount)
        {
            return FormatCount(totalCount, "run", "runs");
        }

        return $"{visibleCount} of {FormatCount(totalCount, "run", "runs")}";
    }

    private static string BuildImprovementFilterResultText(int visibleCount, int totalCount)
    {
        if (visibleCount == totalCount)
        {
            return FormatCount(totalCount, "candidate", "candidates");
        }

        return $"{visibleCount} of {FormatCount(totalCount, "candidate", "candidates")}";
    }

    private static string ResolveRunUpdatedTimeFilterText(ProcessRunUpdatedTimeFilter filter)
    {
        return filter switch
        {
            ProcessRunUpdatedTimeFilter.Last24Hours => "Last 24 hours",
            ProcessRunUpdatedTimeFilter.Last7Days => "Last 7 days",
            ProcessRunUpdatedTimeFilter.Last30Days => "Last 30 days",
            ProcessRunUpdatedTimeFilter.OlderThan30Days => "Older than 30 days",
            _ => "Any time"
        };
    }

    private static string ResolveImprovementSignalFilterText(ProcessImprovementSignalFilter filter)
    {
        return filter switch
        {
            ProcessImprovementSignalFilter.TrainingOpportunity => "Training",
            ProcessImprovementSignalFilter.GovernanceReview => "Governance review",
            _ => "Any signal"
        };
    }

    private static string ResolveImprovementTone(ProcessImprovementStatus status)
    {
        return status switch
        {
            ProcessImprovementStatus.Open => "warning",
            ProcessImprovementStatus.Planned => "info",
            ProcessImprovementStatus.Accepted => "success",
            ProcessImprovementStatus.Rejected => "neutral",
            ProcessImprovementStatus.Closed => "neutral",
            _ => "neutral"
        };
    }

    private static string FormatRunAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            return "just now";
        }

        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)Math.Round(age.TotalMinutes));
            return $"{minutes}m ago";
        }

        if (age < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)Math.Round(age.TotalHours));
            return $"{hours}h ago";
        }

        var days = Math.Max(1, (int)Math.Round(age.TotalDays));
        return $"{days}d ago";
    }

    private static string NormalizeFilterText(string value)
    {
        return value.Trim();
    }

    private static bool ContainsFilterText(string value, string filter)
    {
        return value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
