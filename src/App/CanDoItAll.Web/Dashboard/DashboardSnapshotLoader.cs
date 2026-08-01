using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using System.Collections.Immutable;

namespace CanDoItAll.Web.Dashboard;

public interface IDashboardSnapshotLoader
{
    Task<DashboardSnapshotData> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardSnapshotLoader(
    IRecentProjectActivityQueryService projectActivity,
    IWorkflowDashboardActivityQueryService workflowActivity,
    IProcessDashboardActivityQueryService processActivity,
    IAgentUsageTotalsQueryService usageTotals) : IDashboardSnapshotLoader
{
    private const int ProjectItemCount = 6;
    private const int ActivityItemCount = 5;

    public async Task<DashboardSnapshotData> LoadAsync(CancellationToken cancellationToken = default)
    {
        var projectsTask = LoadSourceAsync(
            DashboardSnapshotSource.Projects,
            () => projectActivity.ListAsync(ProjectItemCount, cancellationToken));
        var workflowsTask = LoadSourceAsync(
            DashboardSnapshotSource.Workflows,
            () => workflowActivity.QueryAsync(
                new WorkflowDashboardActivityQuery(ActivityItemCount),
                cancellationToken));
        var processesTask = LoadSourceAsync(
            DashboardSnapshotSource.Processes,
            () => processActivity.QueryAsync(
                new ProcessDashboardActivityQuery(ActivityItemCount),
                cancellationToken));
        var usageTask = LoadSourceAsync(
            DashboardSnapshotSource.AgentUsage,
            () => usageTotals.GetTotalsAsync(cancellationToken));

        await Task.WhenAll(projectsTask, workflowsTask, processesTask, usageTask).ConfigureAwait(false);

        var projects = await projectsTask.ConfigureAwait(false);
        var workflows = await workflowsTask.ConfigureAwait(false);
        var processes = await processesTask.ConfigureAwait(false);
        var usage = await usageTask.ConfigureAwait(false);

        EnsureBounded(projects.Count, ProjectItemCount, nameof(projects));
        EnsureBounded(workflows.Items.Count, ActivityItemCount, nameof(workflows));
        EnsureBounded(processes.Items.Count, ActivityItemCount, nameof(processes));

        return new DashboardSnapshotData(
            projects.Select(MapProject).ToImmutableArray(),
            MapWorkflowMode(workflows.Mode),
            workflows.Items.Select(MapWorkflow).ToImmutableArray(),
            MapProcessMode(processes.Mode),
            processes.Items.Select(MapProcess).ToImmutableArray(),
            new DashboardUsageTotals(
                usage.ObservedTokens,
                usage.KnownCostUsd,
                usage.UnknownUsageObservationCount,
                usage.UpdatedAtUtc));
    }

    private static DashboardProjectItem MapProject(RecentProjectActivityItem project)
        => new(
            project.Id,
            project.Name,
            project.CurrentPhase,
            MapProjectStatus(project.Status),
            project.UpdatedAtUtc);

    private static DashboardWorkflowRunItem MapWorkflow(WorkflowDashboardActivityItem item)
        => new(
            item.Run.WorkflowId.Value,
            item.Run.RunId.Value,
            item.WorkflowName,
            string.IsNullOrWhiteSpace(item.Run.Summary)
                ? "No run summary available."
                : item.Run.Summary.Trim(),
            MapWorkflowStatus(item.Run.State),
            item.Run.UpdatedAtUtc);

    private static DashboardProcessRunItem MapProcess(ProcessDashboardActivityItem item)
    {
        var projection = item.Projection;
        var projectionIsBehind = projection is null ||
            projection.Freshness.Lag.BacklogEventCount > 0 ||
            projection.LastEventAtUtc < item.UpdatedAtUtc;

        return new DashboardProcessRunItem(
            item.RunId.Value,
            string.IsNullOrWhiteSpace(projection?.ProcessName)
                ? "Projection metadata unavailable"
                : projection.ProcessName.Trim(),
            string.IsNullOrWhiteSpace(projection?.ProjectName)
                ? "No project context"
                : projection.ProjectName.Trim(),
            MapProcessStatus(item.Status),
            item.UpdatedAtUtc,
            projectionIsBehind);
    }

    private static DashboardActivityMode MapWorkflowMode(WorkflowDashboardActivityMode mode)
        => mode switch
        {
            WorkflowDashboardActivityMode.Active => DashboardActivityMode.Active,
            WorkflowDashboardActivityMode.RecentFallback => DashboardActivityMode.RecentFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Workflow activity mode is not defined.")
        };

    private static DashboardActivityMode MapProcessMode(ProcessDashboardActivityMode mode)
        => mode switch
        {
            ProcessDashboardActivityMode.Active => DashboardActivityMode.Active,
            ProcessDashboardActivityMode.RecentFallback => DashboardActivityMode.RecentFallback,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Process activity mode is not defined.")
        };

    private static DashboardDisplayStatus MapProjectStatus(ProjectStatus status)
        => status switch
        {
            ProjectStatus.Draft => new("Draft", DashboardStatusTone.Neutral),
            ProjectStatus.Active => new("Active", DashboardStatusTone.Success),
            ProjectStatus.OnHold => new("On hold", DashboardStatusTone.Warning),
            ProjectStatus.Completed => new("Completed", DashboardStatusTone.Success),
            ProjectStatus.Archived => new("Archived", DashboardStatusTone.Neutral),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Project status is not defined.")
        };

    private static DashboardDisplayStatus MapWorkflowStatus(WorkflowRunState state)
        => state switch
        {
            WorkflowRunState.NotStarted => new("Not started", DashboardStatusTone.Neutral),
            WorkflowRunState.Running => new("Running", DashboardStatusTone.Info),
            WorkflowRunState.WaitingForInput => new("Waiting for input", DashboardStatusTone.Warning),
            WorkflowRunState.Idle => new("Idle", DashboardStatusTone.Neutral),
            WorkflowRunState.Completed => new("Completed", DashboardStatusTone.Success),
            WorkflowRunState.Failed => new("Failed", DashboardStatusTone.Danger),
            WorkflowRunState.Cancelled => new("Cancelled", DashboardStatusTone.Neutral),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Workflow state is not defined.")
        };

    private static DashboardDisplayStatus MapProcessStatus(ProcessRuntimeStatus status)
        => status switch
        {
            ProcessRuntimeStatus.Created => new("Created", DashboardStatusTone.Neutral),
            ProcessRuntimeStatus.Active => new("Active", DashboardStatusTone.Info),
            ProcessRuntimeStatus.Waiting => new("Waiting", DashboardStatusTone.Warning),
            ProcessRuntimeStatus.Blocked => new("Blocked", DashboardStatusTone.Danger),
            ProcessRuntimeStatus.Completed => new("Completed", DashboardStatusTone.Success),
            ProcessRuntimeStatus.Failed => new("Failed", DashboardStatusTone.Danger),
            ProcessRuntimeStatus.CancelRequested => new("Cancel requested", DashboardStatusTone.Warning),
            ProcessRuntimeStatus.Cancelled => new("Cancelled", DashboardStatusTone.Neutral),
            ProcessRuntimeStatus.Escalated => new("Escalated", DashboardStatusTone.Danger),
            ProcessRuntimeStatus.WaitingForUser => new("Waiting for user", DashboardStatusTone.Warning),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Process runtime status is not defined.")
        };

    private static void EnsureBounded(int count, int maximumCount, string source)
    {
        if (count > maximumCount)
        {
            throw new InvalidOperationException(
                $"Dashboard source '{source}' returned {count} rows; the maximum is {maximumCount}.");
        }
    }

    private static async Task<T> LoadSourceAsync<T>(
        DashboardSnapshotSource source,
        Func<Task<T>> loadAsync)
    {
        try
        {
            return await loadAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DashboardSnapshotSourceException(source, exception);
        }
    }
}
