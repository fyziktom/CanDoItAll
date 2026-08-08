using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Modules.Workbench.CanvasAdapters;

namespace CanDoItAll.Modules.Workbench.AgentContext;

/// <summary>
/// Completeness of the visible Gantt projection at observation time. A turn
/// admitted while the projection is loading or failed receives these explicit
/// facts instead of silently reusing stale Canvas or Gantt content.
/// </summary>
public enum ProjectStructureGanttObservationCompleteness
{
    Ready = 0,
    Loading = 1,
    Failed = 2
}

/// <summary>
/// Bounded, model-facing summary of the currently visible Gantt projection.
/// It describes what the user sees; exact task data is read through canonical
/// project-structure tools and mutations use typed product commands. The
/// observation never grants product access.
/// </summary>
public sealed record ProjectStructureGanttObservation
{
    public const int MaximumTopIssueCount = 5;
    public const int MaximumIssueTextLength = 200;

    public ProjectStructureGanttObservation(
        Guid projectId,
        ProjectStructureGanttObservationCompleteness completeness,
        int taskCount,
        int dependencyCount,
        int unscheduledTaskCount,
        int warningCount,
        int errorCount,
        DateTimeOffset? projectedStartUtc,
        DateTimeOffset? projectedEndUtc,
        IReadOnlyList<string>? topIssueSummaries,
        string rowOrderFingerprint,
        string? selectedTaskNodeId,
        DateTimeOffset capturedAtUtc)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project id is required.", nameof(projectId));
        }

        if (taskCount < 0 || dependencyCount < 0 || unscheduledTaskCount < 0 ||
            warningCount < 0 || errorCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taskCount), "Gantt observation counts cannot be negative.");
        }

        var issues = (topIssueSummaries ?? [])
            .Where(static issue => !string.IsNullOrWhiteSpace(issue))
            .Take(MaximumTopIssueCount)
            .Select(static issue => issue.Length > MaximumIssueTextLength
                ? issue[..MaximumIssueTextLength]
                : issue)
            .ToArray();

        ProjectId = projectId;
        Completeness = completeness;
        TaskCount = taskCount;
        DependencyCount = dependencyCount;
        UnscheduledTaskCount = unscheduledTaskCount;
        WarningCount = warningCount;
        ErrorCount = errorCount;
        ProjectedStartUtc = projectedStartUtc;
        ProjectedEndUtc = projectedEndUtc;
        TopIssueSummaries = issues;
        RowOrderFingerprint = rowOrderFingerprint?.Trim() ?? string.Empty;
        SelectedTaskNodeId = string.IsNullOrWhiteSpace(selectedTaskNodeId)
            ? null
            : selectedTaskNodeId.Trim();
        CapturedAtUtc = capturedAtUtc;
        ContentFingerprint = ComputeContentFingerprint();
    }

    public Guid ProjectId { get; }

    public ProjectStructureGanttObservationCompleteness Completeness { get; }

    public int TaskCount { get; }

    public int DependencyCount { get; }

    public int UnscheduledTaskCount { get; }

    public int WarningCount { get; }

    public int ErrorCount { get; }

    public DateTimeOffset? ProjectedStartUtc { get; }

    public DateTimeOffset? ProjectedEndUtc { get; }

    public IReadOnlyList<string> TopIssueSummaries { get; }

    public string RowOrderFingerprint { get; }

    public string? SelectedTaskNodeId { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public string ContentFingerprint { get; }

    private string ComputeContentFingerprint()
    {
        var payload = string.Join(
            '',
            ProjectId.ToString("N"),
            Completeness.ToString(),
            TaskCount.ToString(),
            DependencyCount.ToString(),
            UnscheduledTaskCount.ToString(),
            WarningCount.ToString(),
            ErrorCount.ToString(),
            ProjectedStartUtc?.ToString("O") ?? string.Empty,
            ProjectedEndUtc?.ToString("O") ?? string.Empty,
            string.Join('', TopIssueSummaries),
            RowOrderFingerprint,
            SelectedTaskNodeId ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

}

/// <summary>
/// Typed opaque attachment carrying the exact visible Gantt observation for
/// module-owned interpretation. It never authorizes product access.
/// </summary>
public sealed record ProjectStructureGanttObservationAttachment(
    ProjectStructureGanttObservation Observation) : IAgentChatContextAttachment;

/// <summary>
/// Builds bounded Gantt observations from the panel's projection state.
/// </summary>
public static class ProjectStructureGanttObservationFactory
{
    public static ProjectStructureGanttObservation FromProjection(
        Guid projectId,
        ProjectStructureGanttProjectionResult? projection,
        bool isLoading,
        string? loadError,
        string? selectedTaskNodeId,
        DateTimeOffset capturedAtUtc)
    {
        if (isLoading || (projection is null && string.IsNullOrWhiteSpace(loadError)))
        {
            return new ProjectStructureGanttObservation(
                projectId,
                ProjectStructureGanttObservationCompleteness.Loading,
                taskCount: 0,
                dependencyCount: 0,
                unscheduledTaskCount: 0,
                warningCount: 0,
                errorCount: 0,
                projectedStartUtc: null,
                projectedEndUtc: null,
                topIssueSummaries: null,
                rowOrderFingerprint: string.Empty,
                selectedTaskNodeId: selectedTaskNodeId,
                capturedAtUtc);
        }

        if (projection is null)
        {
            return new ProjectStructureGanttObservation(
                projectId,
                ProjectStructureGanttObservationCompleteness.Failed,
                taskCount: 0,
                dependencyCount: 0,
                unscheduledTaskCount: 0,
                warningCount: 0,
                errorCount: 1,
                projectedStartUtc: null,
                projectedEndUtc: null,
                topIssueSummaries: string.IsNullOrWhiteSpace(loadError) ? null : [loadError],
                rowOrderFingerprint: string.Empty,
                selectedTaskNodeId: selectedTaskNodeId,
                capturedAtUtc);
        }

        var warningCount = projection.Issues.Count(static issue =>
            issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Warning);
        var errorCount = projection.Issues.Count(static issue =>
            issue.Severity == ProjectStructureGanttProjectionIssueSeverity.Error);
        var topIssues = projection.Issues
            .OrderByDescending(static issue => issue.Severity)
            .ThenBy(static issue => issue.Code)
            .Take(ProjectStructureGanttObservation.MaximumTopIssueCount)
            .Select(static issue => $"{issue.Severity}/{issue.Code}: {issue.Message}")
            .ToArray();
        return new ProjectStructureGanttObservation(
            projectId,
            ProjectStructureGanttObservationCompleteness.Ready,
            taskCount: projection.Tasks.Count,
            dependencyCount: projection.Dependencies.Count,
            unscheduledTaskCount: projection.ProjectionOnlyTaskIds.Count,
            warningCount: warningCount,
            errorCount: errorCount,
            projectedStartUtc: projection.Tasks.Count > 0
                ? projection.Tasks.Min(static task => task.Start)
                : null,
            projectedEndUtc: projection.Tasks.Count > 0
                ? projection.Tasks.Max(static task => task.End)
                : null,
            topIssueSummaries: topIssues,
            rowOrderFingerprint: ComputeRowOrderFingerprint(projection.Tasks),
            selectedTaskNodeId: selectedTaskNodeId,
            capturedAtUtc: capturedAtUtc);
    }

    private static string ComputeRowOrderFingerprint(IReadOnlyList<GanttTask> tasks)
    {
        if (tasks.Count == 0)
        {
            return string.Empty;
        }

        var payload = string.Join('', tasks.Select(static task => task.Id.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}


/// <summary>
/// Publishes the visible Gantt projection facts as an agent chat context
/// contributor: a bounded fragment for the model plus a typed opaque
/// attachment for module-owned interpretation. When the projection is loading
/// or failed the fragment states so explicitly instead of retaining stale
/// facts.
/// </summary>
public static class ProjectStructureGanttObservationContributor
{
    public const string ContributorId = "gantt.observation";
    public const string AttachmentKind = "workbench.gantt-observation";

    public static AgentChatContextContributorPublication BuildPublication(
        ProjectStructureGanttObservation? observation,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset freshUntilUtc)
    {
        var fragment = new AgentChatContextFragment(
            new AgentChatContextContributorId(ContributorId),
            order: 160,
            BuildFragmentContent(observation));
        if (observation is null)
        {
            return new AgentChatContextContributorPublication(fragment);
        }

        var draft = new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind(AttachmentKind),
            new SnapshotContentFingerprint(observation.ContentFingerprint),
            new SnapshotCoverageFingerprint(
                $"tasks:{observation.TaskCount};deps:{observation.DependencyCount};issues:{observation.WarningCount + observation.ErrorCount}"),
            databaseProfileGeneration,
            new SnapshotFreshnessFingerprint(observation.CapturedAtUtc.ToString("O")),
            capturedAtUtc,
            freshUntilUtc,
            new ProjectStructureGanttObservationAttachment(observation));
        return new AgentChatContextContributorPublication(fragment, [draft]);
    }

    private static string BuildFragmentContent(ProjectStructureGanttObservation? observation)
    {
        if (observation is null || observation.Completeness == ProjectStructureGanttObservationCompleteness.Loading)
        {
            return """
- Gantt projection facts: loading/partial. Exact visible projection facts are unavailable for this turn.
- Canonical project-structure tools may still query authorized product data; do not treat earlier Canvas or Gantt facts as current.
""";
        }

        if (observation.Completeness == ProjectStructureGanttObservationCompleteness.Failed)
        {
            var failureDetail = observation.TopIssueSummaries.Count > 0
                ? observation.TopIssueSummaries[0]
                : "The projection could not be built.";
            return $"""
- Gantt projection facts: failed. {failureDetail}
- Canonical project-structure tools may still query authorized product data; do not treat earlier Canvas or Gantt facts as current.
""";
        }

        var dateRange = observation.ProjectedStartUtc is { } start && observation.ProjectedEndUtc is { } end
            ? $"{start:yyyy-MM-dd} .. {end:yyyy-MM-dd}"
            : "no scheduled range";
        var issueLines = observation.TopIssueSummaries.Count == 0
            ? "- Gantt projection issues: none."
            : string.Join(
                Environment.NewLine,
                observation.TopIssueSummaries.Select(static issue => $"- Gantt projection issue: {issue}"));
        var selectionLine = observation.SelectedTaskNodeId is { } selectedTask
            ? $"- Selected Gantt task node: {selectedTask}."
            : "- Selected Gantt task node: none.";
        return $"""
- Visible Gantt projection: {observation.TaskCount} tasks, {observation.DependencyCount} dependencies, {observation.UnscheduledTaskCount} without canonical schedules, projected range {dateRange}.
- Gantt projection warnings: {observation.WarningCount}; errors: {observation.ErrorCount}.
{issueLines}
{selectionLine}
- These are visible projection facts only. Read exact task data with canonical project-structure tools; apply changes with typed product commands and readback evidence.
""";
    }
}
