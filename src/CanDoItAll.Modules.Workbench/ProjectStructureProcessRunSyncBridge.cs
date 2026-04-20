using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessRunSyncBridge(IClock clock) : IProcessProjectStructureBridge
{
    public async Task SyncRunAsync(
        AppDbContext dbContext,
        ProcessRun run,
        IReadOnlyCollection<ProcessStepRun> stepRuns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stepRuns);

        if (!run.ProjectId.HasValue || run.ProjectId.Value == Guid.Empty)
        {
            return;
        }

        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var projectId = run.ProjectId.Value;
        var canonicalNodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && !item.IsSystemManaged)
            .ToListAsync(cancellationToken);
        if (canonicalNodes.Count == 0)
        {
            return;
        }

        await ProjectNodeBindingStorage.LoadAsync(dbContext, canonicalNodes, cancellationToken);

        var boundRunNodes = canonicalNodes
            .Where(item => IsBoundToRun(item.Binding, run.Id))
            .ToList();
        if (boundRunNodes.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var stats = ProcessRunSyncStats.Create(stepRuns);

        foreach (var node in boundRunNodes)
        {
            ApplyRunState(node, run, stats, now);
        }

        var nodesByKey = canonicalNodes.ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var childrenByParent = canonicalNodes
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentNodeKey))
            .GroupBy(item => item.ParentNodeKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var parentKeysToRefresh = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in boundRunNodes)
        {
            var currentParentKey = node.ParentNodeKey;
            while (!string.IsNullOrWhiteSpace(currentParentKey) &&
                   nodesByKey.TryGetValue(currentParentKey, out var parentNode))
            {
                if (!parentKeysToRefresh.Add(parentNode.NodeKey))
                {
                    currentParentKey = parentNode.ParentNodeKey;
                    continue;
                }

                currentParentKey = parentNode.ParentNodeKey;
            }
        }

        var orderedParentKeys = parentKeysToRefresh
            .OrderByDescending(GetNodeDepth)
            .ToList();
        foreach (var parentKey in orderedParentKeys)
        {
            if (!nodesByKey.TryGetValue(parentKey, out var parentNode) ||
                !childrenByParent.TryGetValue(parentNode.NodeKey, out var children) ||
                children.Count == 0)
            {
                continue;
            }

            ApplyParentRollup(parentNode, children, now);
        }

        await UpdateProjectStatusAsync(dbContext, projectId, canonicalNodes, now, cancellationToken);

        int GetNodeDepth(string nodeKey)
        {
            var depth = 0;
            var currentKey = nodeKey;
            while (nodesByKey.TryGetValue(currentKey, out var currentNode) &&
                   !string.IsNullOrWhiteSpace(currentNode.ParentNodeKey))
            {
                depth++;
                currentKey = currentNode.ParentNodeKey!;
            }

            return depth;
        }
    }

    private static bool IsBoundToRun(ProjectNodeBindingState binding, Guid runId)
    {
        if (!binding.ExternalArtifactId.HasValue || binding.ExternalArtifactId.Value != runId)
        {
            return false;
        }

        return string.Equals(binding.ExternalArtifactKind, ProjectObjectType.ProcessRun.ToString(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(binding.ExternalArtifactKind, "process-run", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRunState(
        ProjectObjectRecord node,
        ProcessRun run,
        ProcessRunSyncStats stats,
        DateTimeOffset updatedAtUtc)
    {
        var resolvedState = ResolveRunState(run.Status, stats);
        node.Status = resolvedState.Status;
        node.ProgressMode = resolvedState.ProgressMode;
        node.ProgressPercent = resolvedState.ProgressPercent;
        node.UpdatedAtUtc = updatedAtUtc;
    }

    private static void ApplyParentRollup(
        ProjectObjectRecord parentNode,
        IReadOnlyCollection<ProjectObjectRecord> children,
        DateTimeOffset updatedAtUtc)
    {
        var resolvedState = ResolveParentState(children);
        parentNode.Status = resolvedState.Status;
        parentNode.ProgressMode = resolvedState.ProgressMode;
        parentNode.ProgressPercent = resolvedState.ProgressPercent;
        parentNode.UpdatedAtUtc = updatedAtUtc;
    }

    private static async Task UpdateProjectStatusAsync(
        AppDbContext dbContext,
        Guid projectId,
        IReadOnlyCollection<ProjectObjectRecord> canonicalNodes,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var projectRootNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var rootChildren = canonicalNodes
            .Where(item => string.Equals(item.ParentNodeKey, projectRootNodeKey, StringComparison.Ordinal))
            .ToList();
        if (rootChildren.Count == 0)
        {
            return;
        }

        var project = await dbContext.Set<Project>()
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
        {
            return;
        }

        if (rootChildren.All(IsFinished))
        {
            project.Status = ProjectStatus.Completed;
            project.CurrentPhase = "Completed";
            project.UpdatedAtUtc = updatedAtUtc;
        }
    }

    private static (string Status, string ProgressMode, int ProgressPercent) ResolveRunState(
        ProcessRunStatus status,
        ProcessRunSyncStats stats)
    {
        var progressPercent = status == ProcessRunStatus.Completed
            ? 100
            : stats.ProgressPercent;

        return status switch
        {
            ProcessRunStatus.Draft => ("Planned", "progress", progressPercent),
            ProcessRunStatus.Active => ("In Progress", "progress", progressPercent),
            ProcessRunStatus.Blocked => ("Blocked", "progress", progressPercent),
            ProcessRunStatus.Completed => ("Completed", "complete", 100),
            ProcessRunStatus.Cancelled => ("Cancelled", "progress", progressPercent),
            ProcessRunStatus.Failed => ("Failed", "progress", progressPercent),
            _ => (status.ToString(), "progress", progressPercent)
        };
    }

    private static (string Status, string ProgressMode, int ProgressPercent) ResolveParentState(
        IReadOnlyCollection<ProjectObjectRecord> children)
    {
        if (children.All(IsFinished))
        {
            return ("Completed", "complete", 100);
        }

        var averageProgress = (int)Math.Round(
            children.Average(item => ResolveNormalizedProgressPercent(item)),
            MidpointRounding.AwayFromZero);

        if (children.Any(IsBlockedLike))
        {
            return ("Blocked", "progress", averageProgress);
        }

        if (children.Any(IsStartedLike))
        {
            return ("In Progress", "progress", averageProgress);
        }

        return ("Planned", "progress", averageProgress);
    }

    private static int ResolveNormalizedProgressPercent(ProjectObjectRecord node)
    {
        if (string.Equals(node.ProgressMode, "complete", StringComparison.OrdinalIgnoreCase) || IsFinished(node))
        {
            return 100;
        }

        return Math.Clamp(node.ProgressPercent, 0, 100);
    }

    private static bool IsFinished(ProjectObjectRecord node)
    {
        if (string.Equals(node.ProgressMode, "complete", StringComparison.OrdinalIgnoreCase) ||
            node.ProgressPercent >= 100)
        {
            return true;
        }

        return IsFinishedStatus(node.Status);
    }

    private static bool IsFinishedStatus(string? status)
    {
        var normalizedStatus = status?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedStatus))
        {
            return false;
        }

        return normalizedStatus.Contains("done", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("approved", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("ready", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("final", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("archived", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlockedLike(ProjectObjectRecord node)
    {
        var normalizedStatus = node.Status?.Trim() ?? string.Empty;
        return normalizedStatus.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("risk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStartedLike(ProjectObjectRecord node)
    {
        if (ResolveNormalizedProgressPercent(node) > 0)
        {
            return true;
        }

        var normalizedStatus = node.Status?.Trim() ?? string.Empty;
        return normalizedStatus.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("active", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("running", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("review", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("qa", StringComparison.OrdinalIgnoreCase) ||
               normalizedStatus.Contains("test", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProcessRunSyncStats(
        int TotalStepCount,
        int CompletedStepCount,
        int ProgressPercent)
    {
        public static ProcessRunSyncStats Create(IReadOnlyCollection<ProcessStepRun> stepRuns)
        {
            if (stepRuns.Count == 0)
            {
                return new ProcessRunSyncStats(0, 0, 0);
            }

            var completedStepCount = stepRuns.Count(item =>
                item.Status is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Skipped);
            var progressPercent = (int)Math.Round(
                completedStepCount * 100d / stepRuns.Count,
                MidpointRounding.AwayFromZero);

            if (progressPercent == 0 &&
                stepRuns.Any(item => item.Status != ProcessStepRunStatus.Pending))
            {
                progressPercent = 5;
            }

            return new ProcessRunSyncStats(stepRuns.Count, completedStepCount, progressPercent);
        }
    }
}
