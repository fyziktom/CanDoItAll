using CanDoItAll.Components.Gantt;
using System.Globalization;
using System.Text;

namespace CanDoItAll.Modules.Workbench.CanvasAdapters;

internal static class ProjectStructureGanttMermaidExporter
{
    public static string Build(
        string projectName,
        ProjectStructureGanttProjectionResult projection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentNullException.ThrowIfNull(projection);
        if (!projection.IsValid)
        {
            throw new InvalidOperationException("A Mermaid Gantt cannot be exported from an invalid task projection.");
        }

        var mermaidIdsByTaskId = projection.Tasks
            .Select((task, index) => (task.Id, MermaidId: $"task{index + 1}"))
            .ToDictionary(item => item.Id, item => item.MermaidId);
        var tasksById = projection.Tasks.ToDictionary(task => task.Id);
        var intervalSynthesizedTaskIds = projection.Issues
            .Where(issue =>
                issue.Code == ProjectStructureGanttProjectionIssueCode.ScheduleSynthesized &&
                issue.TaskId is not null)
            .Select(issue => issue.TaskId!.Value)
            .ToHashSet();
        var predecessorIdsBySuccessorId = projection.Dependencies
            .Where(dependency =>
                mermaidIdsByTaskId.ContainsKey(dependency.PredecessorId) &&
                mermaidIdsByTaskId.ContainsKey(dependency.SuccessorId))
            .GroupBy(dependency => dependency.SuccessorId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(dependency => mermaidIdsByTaskId[dependency.PredecessorId])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray());

        var builder = new StringBuilder();
        builder.AppendLine("gantt");
        builder.AppendLine($"    title {SanitizeMermaidText(projectName)} schedule");
        builder.AppendLine("    dateFormat YYYY-MM-DD HH:mm:ss");
        builder.AppendLine("    axisFormat %m-%d %H:%M");
        builder.AppendLine("    section Tasks");

        var emittedMermaidIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in projection.Tasks)
        {
            predecessorIdsBySuccessorId.TryGetValue(task.Id, out var predecessorIds);
            var mermaidId = mermaidIdsByTaskId[task.Id];
            builder.Append("    ");
            builder.Append(SanitizeMermaidText(task.Title));
            builder.Append(" :");
            builder.Append(ResolveStateToken(task));
            builder.Append(mermaidId);
            builder.Append(", ");

            if (intervalSynthesizedTaskIds.Contains(task.Id) &&
                predecessorIds is { Length: > 0 } &&
                predecessorIds.All(emittedMermaidIds.Contains) &&
                HasDependencyAlignedStart(task, projection.Dependencies, tasksById))
            {
                builder.Append("after ");
                builder.Append(string.Join(' ', predecessorIds));
            }
            else
            {
                builder.Append(task.Start
                    .ToUniversalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            }

            builder.Append(", ");
            builder.AppendLine(FormatDuration(task.Duration));
            emittedMermaidIds.Add(mermaidId);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool HasDependencyAlignedStart(
        GanttTask task,
        IReadOnlyCollection<GanttDependency> dependencies,
        IReadOnlyDictionary<GanttTaskId, GanttTask> tasksById)
    {
        var latestPredecessorEnd = dependencies
            .Where(dependency => dependency.SuccessorId == task.Id)
            .Select(dependency => tasksById[dependency.PredecessorId].End)
            .Max();

        return task.Start == latestPredecessorEnd;
    }

    private static string ResolveStateToken(GanttTask task)
        => task.ProgressPercent switch
        {
            100 => "done, ",
            > 0 => "active, ",
            _ => string.Empty
        };

    private static string FormatDuration(TimeSpan duration)
    {
        var durationSeconds = Math.Max(1L, (long)Math.Ceiling(duration.TotalSeconds));
        if (durationSeconds % 3600 == 0)
        {
            return $"{durationSeconds / 3600}h";
        }

        if (durationSeconds % 60 == 0)
        {
            return $"{durationSeconds / 60}m";
        }

        return $"{durationSeconds}s";
    }

    private static string SanitizeMermaidText(string value)
        => value
            .Replace(":", " -", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
