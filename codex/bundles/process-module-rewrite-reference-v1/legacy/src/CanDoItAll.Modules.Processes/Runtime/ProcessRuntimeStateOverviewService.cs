using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessRunStatusCounts(
    int Draft,
    int Active,
    int Blocked,
    int Completed,
    int Cancelled,
    int Failed) {
    public static ProcessRunStatusCounts Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public int Total => Draft + Active + Blocked + Completed + Cancelled + Failed;

    public int Running => Active;

    public int Attention => Blocked + Failed;

    public ProcessRunStatusCounts Add(ProcessRunStatus status, int count) {
        return status switch {
            ProcessRunStatus.Draft => this with { Draft = Draft + count },
            ProcessRunStatus.Active => this with { Active = Active + count },
            ProcessRunStatus.Blocked => this with { Blocked = Blocked + count },
            ProcessRunStatus.Completed => this with { Completed = Completed + count },
            ProcessRunStatus.Cancelled => this with { Cancelled = Cancelled + count },
            ProcessRunStatus.Failed => this with { Failed = Failed + count },
            _ => this
        };
    }
}

public sealed record ProcessDefinitionRuntimeState(
    Guid DefinitionId,
    ProcessRunStatusCounts RunCounts) {
    public static ProcessDefinitionRuntimeState Empty(Guid definitionId) {
        return new ProcessDefinitionRuntimeState(definitionId, ProcessRunStatusCounts.Empty);
    }
}

public sealed record ProcessRuntimeStateOverview(
    Guid? ProjectId,
    ProcessRunStatusCounts Totals,
    IReadOnlyDictionary<Guid, ProcessDefinitionRuntimeState> Definitions) {
    public static ProcessRuntimeStateOverview Empty(Guid? projectId) {
        return new ProcessRuntimeStateOverview(projectId, ProcessRunStatusCounts.Empty, new Dictionary<Guid, ProcessDefinitionRuntimeState>());
    }

    public ProcessDefinitionRuntimeState GetDefinition(Guid definitionId) {
        return Definitions.TryGetValue(definitionId, out var state)
            ? state
            : ProcessDefinitionRuntimeState.Empty(definitionId);
    }
}

public sealed class ProcessRuntimeStateOverviewService(IDbContextFactory<AppDbContext> dbContextFactory) {
    private IReadOnlyList<Guid> cachedDefinitionIds = [];
    private Guid? cachedProjectId;
    private ProcessRuntimeStateOverview? cachedOverview;

    public void Invalidate() {
        cachedDefinitionIds = [];
        cachedProjectId = null;
        cachedOverview = null;
    }

    public async Task<ProcessRuntimeStateOverview> GetOverviewAsync(
        IReadOnlyCollection<Guid> definitionIds,
        Guid? projectId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(definitionIds);

        var normalizedDefinitionIds = definitionIds
            .Where(definitionId => definitionId != Guid.Empty)
            .Distinct()
            .OrderBy(definitionId => definitionId)
            .ToList();
        if (normalizedDefinitionIds.Count == 0) {
            return ProcessRuntimeStateOverview.Empty(projectId);
        }

        if (!forceRefresh &&
            cachedOverview is not null &&
            cachedProjectId == projectId &&
            SequenceEqual(cachedDefinitionIds, normalizedDefinitionIds)) {
            return cachedOverview;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runsQuery = dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(run => normalizedDefinitionIds.Contains(run.ProcessDefinitionId));
        if (projectId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        var statusCounts = await runsQuery
            .GroupBy(run => new
            {
                run.ProcessDefinitionId,
                run.Status
            })
            .Select(group => new ProcessRunStatusCountProjection(group.Key.ProcessDefinitionId, group.Key.Status, group.Count()))
            .ToListAsync(cancellationToken);
        var countsByDefinitionId = normalizedDefinitionIds.ToDictionary(
            definitionId => definitionId,
            _ => ProcessRunStatusCounts.Empty);
        var totals = ProcessRunStatusCounts.Empty;

        foreach (var statusCount in statusCounts) {
            countsByDefinitionId[statusCount.ProcessDefinitionId] = countsByDefinitionId[statusCount.ProcessDefinitionId]
                .Add(statusCount.Status, statusCount.Count);
            totals = totals.Add(statusCount.Status, statusCount.Count);
        }

        cachedDefinitionIds = normalizedDefinitionIds;
        cachedProjectId = projectId;
        cachedOverview = new ProcessRuntimeStateOverview(
            projectId,
            totals,
            countsByDefinitionId.ToDictionary(
                item => item.Key,
                item => new ProcessDefinitionRuntimeState(item.Key, item.Value)));

        return cachedOverview;
    }

    private static bool SequenceEqual(IReadOnlyList<Guid> left, IReadOnlyList<Guid> right) {
        if (left.Count != right.Count) {
            return false;
        }

        for (var index = 0; index < left.Count; index++) {
            if (left[index] != right[index]) {
                return false;
            }
        }

        return true;
    }

    private sealed record ProcessRunStatusCountProjection(
        Guid ProcessDefinitionId,
        ProcessRunStatus Status,
        int Count);
}
