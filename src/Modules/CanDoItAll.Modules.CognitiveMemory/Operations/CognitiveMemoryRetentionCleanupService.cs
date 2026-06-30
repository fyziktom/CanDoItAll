using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryRetentionCleanupService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ILogger<CognitiveMemoryRetentionCleanupService> logger) : ICognitiveMemoryRetentionCleanupService
{
    private const string AlgorithmVersion = "retention-cleanup-v1";

    public async ValueTask<CognitiveMemoryRetentionCleanupResult> CleanupAsync(
        CognitiveMemoryRetentionCleanupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        var deleteBeforeUtc = NormalizeCutoff(request.DeleteBeforeUtc);
        var nowUtc = clock.GetUtcNow();
        if (deleteBeforeUtc >= nowUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DeleteBeforeUtc),
                "Retention cleanup cutoff must be earlier than the current UTC time.");
        }

        var scopes = NormalizeScopes(request.Scopes ?? CognitiveMemoryRetentionCleanupRequest.DefaultScopes);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = new CognitiveMemoryRunRecord
        {
            ProjectId = request.ProjectId,
            RunKind = CognitiveMemoryRunKind.RetentionCleanup,
            Status = CognitiveMemoryRunStatus.Running,
            OperationMode = request.DryRun
                ? CognitiveMemoryOperationMode.Observe
                : CognitiveMemoryOperationMode.Maintenance,
            IdempotencyKey = BuildRunIdempotencyKey(request, actorId, nowUtc),
            InputHash = BuildRequestHash(request, actorId, scopes),
            AlgorithmVersion = AlgorithmVersion,
            Cursor = string.Join(",", scopes),
            StartedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(run);

        var results = new List<CognitiveMemoryRetentionCleanupScopeResult>(scopes.Count);
        foreach (var scope in scopes)
        {
            var result = scope switch
            {
                CognitiveMemoryRetentionCleanupScope.RecallTraces => await CleanupRecallTracesAsync(dbContext, request.ProjectId, deleteBeforeUtc, request.DryRun, cancellationToken),
                CognitiveMemoryRetentionCleanupScope.ConsolidationCandidates => await CleanupConsolidationCandidatesAsync(dbContext, request.ProjectId, deleteBeforeUtc, request.DryRun, cancellationToken),
                CognitiveMemoryRetentionCleanupScope.ProbeSessions => await CleanupProbeSessionsAsync(dbContext, request.ProjectId, deleteBeforeUtc, request.DryRun, cancellationToken),
                CognitiveMemoryRetentionCleanupScope.DistributedJobs => await CleanupDistributedJobsAsync(dbContext, request.ProjectId, deleteBeforeUtc, request.DryRun, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Scopes), scope, "Retention cleanup scope is not supported.")
            };
            results.Add(result);
        }

        run.Status = CognitiveMemoryRunStatus.Succeeded;
        run.CompletedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Cognitive memory retention cleanup completed. ActorId={ActorId} ProjectId={ProjectId} DryRun={DryRun} DeleteBeforeUtc={DeleteBeforeUtc} TotalMatched={TotalMatched} TotalDeleted={TotalDeleted}",
            actorId,
            request.ProjectId,
            request.DryRun,
            deleteBeforeUtc,
            results.Sum(result => result.MatchedRootRecords),
            results.Sum(result => result.DeletedRecords));

        return new CognitiveMemoryRetentionCleanupResult(
            request.ProjectId,
            deleteBeforeUtc,
            request.DryRun,
            actorId,
            results);
    }

    private static async Task<CognitiveMemoryRetentionCleanupScopeResult> CleanupRecallTracesAsync(
        AppDbContext dbContext,
        Guid? projectId,
        DateTimeOffset deleteBeforeUtc,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var traceIds = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .Where(trace => !projectId.HasValue || trace.ProjectId == projectId.Value)
            .Where(trace => (trace.CompletedAtUtc ?? trace.StartedAtUtc) < deleteBeforeUtc)
            .Select(trace => trace.Id)
            .ToArrayAsync(cancellationToken);
        if (traceIds.Length == 0)
        {
            return Empty(CognitiveMemoryRetentionCleanupScope.RecallTraces, "No recall traces matched the cutoff.");
        }

        var stages = await dbContext.Set<CognitiveMemoryRecallTraceStageRecord>()
            .Where(stage => traceIds.Contains(stage.RecallTraceId))
            .ToListAsync(cancellationToken);
        var candidates = await dbContext.Set<CognitiveMemoryRecallCandidateRecord>()
            .Where(candidate => traceIds.Contains(candidate.RecallTraceId))
            .ToListAsync(cancellationToken);
        var sections = await dbContext.Set<CognitiveMemoryRecallContextSectionRecord>()
            .Where(section => traceIds.Contains(section.RecallTraceId))
            .ToListAsync(cancellationToken);
        var sourceRefs = await dbContext.Set<CognitiveMemoryRecallSourceRefRecord>()
            .Where(sourceRef => traceIds.Contains(sourceRef.RecallTraceId))
            .ToListAsync(cancellationToken);
        var packs = await dbContext.Set<CognitiveMemoryRecallContextPackRecord>()
            .Where(pack => traceIds.Contains(pack.RecallTraceId))
            .ToListAsync(cancellationToken);
        var traces = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .Where(trace => traceIds.Contains(trace.Id))
            .ToListAsync(cancellationToken);
        var deletedRecords = stages.Count + candidates.Count + sections.Count + sourceRefs.Count + packs.Count + traces.Count;

        if (!dryRun)
        {
            dbContext.RemoveRange(sections);
            dbContext.RemoveRange(sourceRefs);
            dbContext.RemoveRange(packs);
            dbContext.RemoveRange(candidates);
            dbContext.RemoveRange(stages);
            dbContext.RemoveRange(traces);
        }

        return new CognitiveMemoryRetentionCleanupScopeResult(
            CognitiveMemoryRetentionCleanupScope.RecallTraces,
            traces.Count,
            dryRun ? 0 : deletedRecords,
            $"{deletedRecords} recall trace record(s), including dependent operational rows, matched the cutoff.");
    }

    private static async Task<CognitiveMemoryRetentionCleanupScopeResult> CleanupConsolidationCandidatesAsync(
        AppDbContext dbContext,
        Guid? projectId,
        DateTimeOffset deleteBeforeUtc,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .Where(candidate => !projectId.HasValue || candidate.ProjectId == projectId.Value)
            .Where(candidate => candidate.CreatedAtUtc < deleteBeforeUtc &&
                                (candidate.Status == CognitiveMemoryConsolidationCandidateStatus.Rejected ||
                                 candidate.Status == CognitiveMemoryConsolidationCandidateStatus.SkippedDuplicate))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return Empty(CognitiveMemoryRetentionCleanupScope.ConsolidationCandidates, "No rejected or duplicate consolidation candidates matched the cutoff.");
        }

        if (!dryRun)
        {
            dbContext.RemoveRange(candidates);
        }

        return new CognitiveMemoryRetentionCleanupScopeResult(
            CognitiveMemoryRetentionCleanupScope.ConsolidationCandidates,
            candidates.Count,
            dryRun ? 0 : candidates.Count,
            "Only rejected or duplicate consolidation candidates are eligible for cleanup.");
    }

    private static async Task<CognitiveMemoryRetentionCleanupScopeResult> CleanupProbeSessionsAsync(
        AppDbContext dbContext,
        Guid? projectId,
        DateTimeOffset deleteBeforeUtc,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var sessionIds = await dbContext.Set<CognitiveMemoryProbeSessionRecord>()
            .Where(session => !projectId.HasValue || session.ProjectId == projectId.Value)
            .Where(session => (session.ClosedAtUtc ?? session.UpdatedAtUtc) < deleteBeforeUtc &&
                              (session.Status == CognitiveMemoryProbeSessionStatus.Closed ||
                               session.Status == CognitiveMemoryProbeSessionStatus.Abandoned))
            .Select(session => session.Id)
            .ToArrayAsync(cancellationToken);
        if (sessionIds.Length == 0)
        {
            return Empty(CognitiveMemoryRetentionCleanupScope.ProbeSessions, "No closed or abandoned probe sessions matched the cutoff.");
        }

        var turns = await dbContext.Set<CognitiveMemoryProbeTurnRecord>()
            .Where(turn => sessionIds.Contains(turn.ProbeSessionId))
            .ToListAsync(cancellationToken);
        var turnIds = turns.Select(turn => turn.Id).ToArray();
        var feedback = await dbContext.Set<CognitiveMemoryProbeFeedbackRecord>()
            .Where(item => turnIds.Contains(item.ProbeTurnId))
            .ToListAsync(cancellationToken);
        var findings = await dbContext.Set<CognitiveMemoryProbeFindingRecord>()
            .Where(item => turnIds.Contains(item.ProbeTurnId))
            .ToListAsync(cancellationToken);
        var regressionCases = await dbContext.Set<CognitiveMemoryProbeRegressionTestCaseRecord>()
            .Where(item => turnIds.Contains(item.ProbeTurnId))
            .ToListAsync(cancellationToken);
        var regressionCaseIds = regressionCases.Select(item => item.Id).ToArray();
        var regressionRuns = await dbContext.Set<CognitiveMemoryProbeRegressionRunRecord>()
            .Where(item => regressionCaseIds.Contains(item.RegressionTestCaseId))
            .ToListAsync(cancellationToken);
        var sessions = await dbContext.Set<CognitiveMemoryProbeSessionRecord>()
            .Where(session => sessionIds.Contains(session.Id))
            .ToListAsync(cancellationToken);
        var deletedRecords = regressionRuns.Count + regressionCases.Count + feedback.Count + findings.Count + turns.Count + sessions.Count;

        if (!dryRun)
        {
            dbContext.RemoveRange(regressionRuns);
            dbContext.RemoveRange(regressionCases);
            dbContext.RemoveRange(feedback);
            dbContext.RemoveRange(findings);
            dbContext.RemoveRange(turns);
            dbContext.RemoveRange(sessions);
        }

        return new CognitiveMemoryRetentionCleanupScopeResult(
            CognitiveMemoryRetentionCleanupScope.ProbeSessions,
            sessions.Count,
            dryRun ? 0 : deletedRecords,
            $"{deletedRecords} probe session record(s), including turns and feedback, matched the cutoff.");
    }

    private static async Task<CognitiveMemoryRetentionCleanupScopeResult> CleanupDistributedJobsAsync(
        AppDbContext dbContext,
        Guid? projectId,
        DateTimeOffset deleteBeforeUtc,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var jobIds = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .Where(job => !projectId.HasValue || job.ProjectId == projectId.Value)
            .Where(job => job.UpdatedAtUtc < deleteBeforeUtc &&
                          (job.State == CognitiveMemoryDistributedJobState.Completed ||
                           job.State == CognitiveMemoryDistributedJobState.Rejected ||
                           job.State == CognitiveMemoryDistributedJobState.Expired))
            .Select(job => job.Id)
            .ToArrayAsync(cancellationToken);
        if (jobIds.Length == 0)
        {
            return Empty(CognitiveMemoryRetentionCleanupScope.DistributedJobs, "No completed, rejected, or expired distributed jobs matched the cutoff.");
        }

        var results = await dbContext.Set<CognitiveMemoryDistributedWorkerResultRecord>()
            .Where(result => jobIds.Contains(result.DistributedJobId))
            .ToListAsync(cancellationToken);
        var jobs = await dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .Where(job => jobIds.Contains(job.Id))
            .ToListAsync(cancellationToken);
        var deletedRecords = results.Count + jobs.Count;

        if (!dryRun)
        {
            dbContext.RemoveRange(results);
            dbContext.RemoveRange(jobs);
        }

        return new CognitiveMemoryRetentionCleanupScopeResult(
            CognitiveMemoryRetentionCleanupScope.DistributedJobs,
            jobs.Count,
            dryRun ? 0 : deletedRecords,
            "Only completed, rejected, or expired distributed jobs are eligible for cleanup.");
    }

    private static DateTimeOffset NormalizeCutoff(DateTimeOffset value)
        => value.ToUniversalTime();

    private static IReadOnlyList<CognitiveMemoryRetentionCleanupScope> NormalizeScopes(
        IReadOnlyList<CognitiveMemoryRetentionCleanupScope> scopes)
    {
        return scopes.Count == 0
            ? CognitiveMemoryRetentionCleanupRequest.DefaultScopes
            : scopes
                .Distinct()
                .OrderBy(scope => scope)
                .ToArray();
    }

    private static CognitiveMemoryRetentionCleanupScopeResult Empty(
        CognitiveMemoryRetentionCleanupScope scope,
        string notes)
        => new(scope, 0, 0, notes);

    private static string BuildRunIdempotencyKey(
        CognitiveMemoryRetentionCleanupRequest request,
        string actorId,
        DateTimeOffset nowUtc)
        => $"retention-cleanup:{request.ProjectId?.ToString("D") ?? "global"}:{actorId}:{nowUtc:yyyyMMddHHmmssfffffff}";

    private static string BuildRequestHash(
        CognitiveMemoryRetentionCleanupRequest request,
        string actorId,
        IReadOnlyList<CognitiveMemoryRetentionCleanupScope> scopes)
        => CognitiveMemoryHash.FromUtf8(string.Join(
            "|",
            request.ProjectId?.ToString("D") ?? string.Empty,
            request.DeleteBeforeUtc.ToUniversalTime().ToString("O"),
            request.DryRun.ToString(),
            string.Join(",", scopes),
            actorId)).Value;
}
