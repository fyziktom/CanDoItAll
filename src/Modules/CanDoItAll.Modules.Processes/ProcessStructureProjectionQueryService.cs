using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessStructureProjectionQueryService(
    IDbContextFactory<ProcessPersistenceDbContext> factory,
    DbContextOptions<ProcessPersistenceDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProcessStructureProjectionQueryService {
    private const string ProjectIdVariableName = "ProjectId";

    public async Task<ProcessRunRecordPage> ListAsync(ProcessRunRecordListQuery query, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await new EfProcessRunRecordStore(context).ListAsync(query, cancellationToken);
    }

    public async Task<ProcessRunRecordPage> ListForMutationAsync(ProcessRunRecordListQuery query, CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        return await new EfProcessRunRecordStore(context).ListAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessStructureAssignmentFact>> GetProjectAssignmentsAsync(Guid projectId,
        IReadOnlyCollection<Guid> excludedRunIds, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadAssignmentsAsync(context, projectId, excludedRunIds, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessStructureAssignmentFact>> GetProjectAssignmentsForMutationAsync(Guid projectId,
        IReadOnlyCollection<Guid> excludedRunIds, CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        return await ReadAssignmentsAsync(context, projectId, excludedRunIds, cancellationToken);
    }

    public async Task<ProcessStructureRuntimeFacts> GetRuntimeFactsAsync(IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadRuntimeFactsAsync(context, runIds, cancellationToken);
    }

    public async Task<ProcessStructureRuntimeFacts> GetRuntimeFactsForMutationAsync(IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken);
        return await ReadRuntimeFactsAsync(context, runIds, cancellationToken);
    }

    private Task<ProcessPersistenceDbContext> CreateEnlistedAsync(CancellationToken cancellationToken) =>
        transactions.CreateEnlistedAsync(options, static value => new ProcessPersistenceDbContext(value), cancellationToken);

    private static async Task<IReadOnlyList<ProcessStructureAssignmentFact>> ReadAssignmentsAsync(
        ProcessPersistenceDbContext context, Guid projectId, IReadOnlyCollection<Guid> excludedRunIds,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(projectId, Guid.Empty);
        var excluded = NormalizeIds(excludedRunIds);
        var snippet = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) {
            [ProjectIdVariableName] = projectId.ToString("D")
        }).Trim('{', '}');
        var rows = await context.RuntimeStepAssignments.AsNoTracking()
            .Where(item => !excluded.Contains(item.RunId) && item.LaunchVariablesJson.Contains(snippet))
            .OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.RunId).ThenBy(item => item.StepInstanceId)
            .Select(item => new ProcessStructureAssignmentFact(item.RunId, item.LaunchVariablesJson, item.CreatedAtUtc))
            .Take(ProcessStructureProjectionQueryLimits.MaximumAssignments + 1).ToListAsync(cancellationToken);
        if (rows.Count > ProcessStructureProjectionQueryLimits.MaximumAssignments) {
            throw new InvalidOperationException($"Process structure projection exceeds {ProcessStructureProjectionQueryLimits.MaximumAssignments} historical assignment candidates. Select a smaller project scope.");
        }
        return rows;
    }

    private static async Task<ProcessStructureRuntimeFacts> ReadRuntimeFactsAsync(ProcessPersistenceDbContext context,
        IReadOnlyCollection<Guid> runIds, CancellationToken cancellationToken) {
        var ids = NormalizeIds(runIds);
        if (ids.Length == 0) {
            return new([], [], [], []);
        }
        var linked = await context.RuntimeStates.AsNoTracking().Where(item => ids.Contains(item.RunId))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new ProcessStructureRuntimeFact(item.RunId, item.RootRunId, item.PlanId, item.Status, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var rootIds = linked.Select(item => item.RootRunId).Distinct().ToArray();
        var roots = await context.RuntimeStates.AsNoTracking().Where(item => rootIds.Contains(item.RunId))
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new ProcessStructureRuntimeFact(item.RunId, item.RootRunId, item.PlanId, item.Status, item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var planIds = roots.Where(item => item.PlanId.HasValue).Select(item => item.PlanId!.Value).Distinct().ToArray();
        var plans = await context.InstancePlans.AsNoTracking().Where(item => planIds.Contains(item.PlanId))
            .Select(item => new ProcessStructurePlanFact(item.PlanId, item.DefinitionId, item.CreatedAtUtc)).ToListAsync(cancellationToken);
        var statistics = await context.RuntimeSteps.AsNoTracking().Where(item => rootIds.Contains(item.RunId))
            .GroupBy(item => item.RunId).Select(group => new ProcessStructureStepStatistics(group.Key, group.Count(),
                group.Count(item => item.Status == ProcessRuntimeStepStatus.Completed),
                group.Count(item => item.Status == ProcessRuntimeStepStatus.Blocked),
                group.Count(item => item.Status == ProcessRuntimeStepStatus.WaitingApproval),
                group.Count(item => item.Status == ProcessRuntimeStepStatus.Ready || item.Status == ProcessRuntimeStepStatus.Waiting ||
                    item.Status == ProcessRuntimeStepStatus.Running || item.Status == ProcessRuntimeStepStatus.Claimed)))
            .ToListAsync(cancellationToken);
        return new(linked, roots, plans, statistics);
    }

    private static Guid[] NormalizeIds(IReadOnlyCollection<Guid> runIds) {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > ProcessStructureProjectionQueryLimits.MaximumRunIds || runIds.Any(id => id == Guid.Empty)) {
            throw new ArgumentException($"Process projection requires at most {ProcessStructureProjectionQueryLimits.MaximumRunIds} non-empty identifiers.", nameof(runIds));
        }
        return runIds.Distinct().ToArray();
    }
}
