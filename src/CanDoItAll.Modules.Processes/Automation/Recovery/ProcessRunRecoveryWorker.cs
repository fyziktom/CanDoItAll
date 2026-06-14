using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunRecoveryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProcessOutboxService processOutboxService,
    IAgentFrameworkWorkspaceService workspaceService,
    IClock clock,
    ILogger<ProcessRunRecoveryService> logger)
{
    private const int MaxRunBatchSize = 10;
    private const string RecoveryTrigger = "runtime-recovery-scan";
    private static readonly TimeSpan StaleAutomationExecutionRunTimeout = TimeSpan.FromMinutes(10);

    public Task<int> RecoverActiveRunsAsync(CancellationToken cancellationToken = default)
        => RecoverActiveRunsAsync(reclaimExpiredAutomationDispatchLeases: false, cancellationToken);

    public async Task<int> RecoverActiveRunsAsync(
        bool reclaimExpiredAutomationDispatchLeases,
        CancellationToken cancellationToken = default)
        => await RecoverActiveRunsAsync(
            reclaimExpiredAutomationDispatchLeases,
            startupCutoffUtc: null,
            cancellationToken);

    internal async Task<int> RecoverActiveRunsAsync(
        bool reclaimExpiredAutomationDispatchLeases,
        DateTimeOffset? startupCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await (
                from run in dbContext.Set<ProcessRun>().AsNoTracking()
                where run.Status != ProcessRunStatus.Completed
                   && run.Status != ProcessRunStatus.Cancelled
                join step in dbContext.Set<ProcessStepRun>().AsNoTracking() on run.Id equals step.ProcessRunId
                where (run.Status != ProcessRunStatus.Failed || step.Status == ProcessStepRunStatus.InProgress) &&
                      step.CurrentExecutorPartyId.HasValue &&
                      (step.Status == ProcessStepRunStatus.Ready ||
                       step.Status == ProcessStepRunStatus.WaitingApproval ||
                       step.Status == ProcessStepRunStatus.InProgress)
                group step by new
                {
                    RunId = run.Id,
                    run.ProjectId,
                    run.ProcessDefinitionId
                }
                into runSteps
                orderby runSteps.Min(item => item.Sequence)
                select new RecoveryDispatchCandidate(
                    runSteps.Key.RunId,
                    runSteps.Key.ProjectId,
                    runSteps.Key.ProcessDefinitionId))
            .Take(MaxRunBatchSize)
            .ToListAsync(cancellationToken);

        if (reclaimExpiredAutomationDispatchLeases)
        {
            var leaseRecoveryRunIds = await ListActiveRunIdsWithRecoverableAutomationDispatchLeasesAsync(
                dbContext,
                startupCutoffUtc,
                cancellationToken);
            var releasedLeaseCount = await ReleaseRecoverableAutomationDispatchLeasesAsync(
                dbContext,
                candidates
                    .Select(candidate => candidate.RunId)
                    .Concat(leaseRecoveryRunIds)
                    .Distinct()
                    .ToArray(),
                startupCutoffUtc,
                cancellationToken);
            if (releasedLeaseCount > 0)
            {
                logger.LogWarning(
                    "Released {ReleasedLeaseCount} stale process automation dispatch lease(s) during runtime recovery startup scan.",
                    releasedLeaseCount);
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var queuedCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await ShouldSkipForRecoveryBackoffAsync(dbContext, candidate.RunId, cancellationToken) ||
                    await HasPendingAutomationDispatchAsync(dbContext, candidate.RunId, cancellationToken) ||
                    await HasActiveAutomationExecutionAsync(candidate.RunId, cancellationToken))
                {
                    continue;
                }

                await processOutboxService.EnqueueAutomationDispatchAsync(
                    dbContext,
                    candidate.ProjectId,
                    candidate.ProcessDefinitionId,
                    candidate.RunId,
                    null,
                    RecoveryTrigger,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                queuedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Process run recovery scan failed for run {RunId}.",
                    candidate.RunId);
            }
        }

        return queuedCount;
    }

    private async Task<bool> HasActiveAutomationExecutionAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: runId.ToString("D"),
                Take: 50),
            cancellationToken);

        return HasBlockingAutomationExecutionRun(executionRuns, clock.GetUtcNow());
    }

    private static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return executionRuns.Any(executionRun =>
            string.Equals(
                executionRun.RequestedBy,
                ProcessRunAutomationDispatchService.AutomationActor,
                StringComparison.OrdinalIgnoreCase) &&
            executionRun.State is not ExecutionState.Completed and not ExecutionState.Failed &&
            !IsStaleAutomationExecutionRun(executionRun, now));
    }

    private static bool IsStaleAutomationExecutionRun(
        ExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        if (executionRun.PendingApprovals.Count > 0)
        {
            return false;
        }

        var lastProgressAtUtc = executionRun.UpdatedAtUtc == default
            ? executionRun.CreatedAtUtc
            : executionRun.UpdatedAtUtc;
        return now - lastProgressAtUtc >= StaleAutomationExecutionRunTimeout;
    }

    private async Task<bool> ShouldSkipForRecoveryBackoffAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var ledgerRows = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId &&
                           item.EventType == ProcessRuntimeEventTypes.AgentRecoveryAttemptRecorded)
            .Select(item => new
            {
                item.OccurredAtUtc,
                item.ReplayContextJson
            })
            .ToListAsync(cancellationToken);
        var entries = ledgerRows
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(20)
            .Select(item => item.ReplayContextJson)
            .Select(TryReadLedgerEntry)
            .OfType<AgentRecoveryLedgerEntry>()
            .ToList();
        return !AgentRecoveryLedger.CanAttemptNow(entries, now);
    }

    private static Task<bool> HasPendingAutomationDispatchAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<ProcessOutboxRecord>()
            .AsNoTracking()
            .AnyAsync(item =>
                item.ProcessRunId == runId &&
                item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                item.Status == ProcessOutboxRecordStatus.Pending,
                cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ListActiveRunIdsWithRecoverableAutomationDispatchLeasesAsync(
        AppDbContext dbContext,
        DateTimeOffset? startupCutoffUtc,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var records = await (
                from record in dbContext.Set<ProcessOutboxRecord>().AsNoTracking()
                where record.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                      record.Status == ProcessOutboxRecordStatus.Pending &&
                      record.ProcessRunId.HasValue &&
                      record.LeaseExpiresAtUtc.HasValue
                join run in dbContext.Set<ProcessRun>().AsNoTracking() on record.ProcessRunId equals (Guid?)run.Id
                where run.Status != ProcessRunStatus.Completed &&
                      run.Status != ProcessRunStatus.Cancelled
                select record)
            .ToListAsync(cancellationToken);

        return records
            .Where(record => IsRecoverableAutomationDispatchOutboxLease(record, now, startupCutoffUtc))
            .Select(record => record.ProcessRunId!.Value)
            .Distinct()
            .ToList();
    }

    private async Task<int> ReleaseRecoverableAutomationDispatchLeasesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> runIds,
        DateTimeOffset? startupCutoffUtc,
        CancellationToken cancellationToken)
    {
        if (runIds.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var runIdSet = runIds.ToHashSet();
        var records = (await dbContext.Set<ProcessOutboxRecord>()
                .Where(item =>
                    item.CommandKey == ProcessOutboxService.AutomationDispatchCommandKey &&
                    item.Status == ProcessOutboxRecordStatus.Pending)
                .ToListAsync(cancellationToken))
            .Where(item => ShouldReleaseAutomationDispatchOutboxLeaseForRecovery(
                item,
                runIdSet,
                now,
                startupCutoffUtc))
            .ToList();
        foreach (var record in records)
        {
            record.LeaseToken = string.Empty;
            record.LeaseExpiresAtUtc = null;
            record.UpdatedAtUtc = now;
        }

        var steps = (await dbContext.Set<ProcessStepRun>()
                .Where(item => runIds.Contains(item.ProcessRunId) &&
                    item.AutomationDispatchLeaseExpiresAtUtc.HasValue &&
                    (item.Status == ProcessStepRunStatus.Ready ||
                     item.Status == ProcessStepRunStatus.WaitingApproval ||
                     item.Status == ProcessStepRunStatus.InProgress))
                .ToListAsync(cancellationToken))
            .Where(item => ShouldReleaseStepDispatchClaimForRecovery(
                item,
                runIdSet,
                now,
                startupCutoffUtc))
            .ToList();
        foreach (var step in steps)
        {
            step.AutomationDispatchClaimToken = string.Empty;
            step.AutomationDispatchClaimedBy = string.Empty;
            step.AutomationDispatchClaimedAtUtc = null;
            step.AutomationDispatchLeaseExpiresAtUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return records.Count + steps.Count;
    }

    internal static bool ShouldReleaseAutomationDispatchOutboxLeaseForRecovery(
        ProcessOutboxRecord record,
        IReadOnlySet<Guid> runIds,
        DateTimeOffset now,
        DateTimeOffset? startupCutoffUtc)
    {
        if (record.ProcessRunId is not { } runId ||
            !runIds.Contains(runId) ||
            !string.Equals(record.CommandKey, ProcessOutboxService.AutomationDispatchCommandKey, StringComparison.Ordinal) ||
            record.Status != ProcessOutboxRecordStatus.Pending ||
            !record.LeaseExpiresAtUtc.HasValue)
        {
            return false;
        }

        return IsRecoverableAutomationDispatchOutboxLease(record, now, startupCutoffUtc);
    }

    internal static bool ShouldReleaseStepDispatchClaimForRecovery(
        ProcessStepRun stepRun,
        IReadOnlySet<Guid> runIds,
        DateTimeOffset now,
        DateTimeOffset? startupCutoffUtc)
    {
        if (!runIds.Contains(stepRun.ProcessRunId) ||
            !stepRun.AutomationDispatchLeaseExpiresAtUtc.HasValue ||
            stepRun.Status is not (
                ProcessStepRunStatus.Ready or
                ProcessStepRunStatus.WaitingApproval or
                ProcessStepRunStatus.InProgress))
        {
            return false;
        }

        return stepRun.AutomationDispatchLeaseExpiresAtUtc.Value <= now ||
            IsPreStartupLease(stepRun.AutomationDispatchClaimedAtUtc, startupCutoffUtc);
    }

    private static bool IsRecoverableAutomationDispatchOutboxLease(
        ProcessOutboxRecord record,
        DateTimeOffset now,
        DateTimeOffset? startupCutoffUtc)
    {
        return record.LeaseExpiresAtUtc.HasValue &&
            (record.LeaseExpiresAtUtc.Value <= now ||
             IsPreStartupLease(ResolveOutboxLeaseOwnershipTime(record), startupCutoffUtc));
    }

    private static DateTimeOffset? ResolveOutboxLeaseOwnershipTime(ProcessOutboxRecord record)
    {
        if (record.LastAttemptAtUtc.HasValue)
        {
            return record.LastAttemptAtUtc.Value;
        }

        if (record.UpdatedAtUtc != default)
        {
            return record.UpdatedAtUtc;
        }

        return record.CreatedAtUtc == default
            ? null
            : record.CreatedAtUtc;
    }

    private static bool IsPreStartupLease(DateTimeOffset? ownershipAtUtc, DateTimeOffset? startupCutoffUtc)
    {
        return startupCutoffUtc.HasValue &&
            ownershipAtUtc.HasValue &&
            ownershipAtUtc.Value <= startupCutoffUtc.Value;
    }

    private static AgentRecoveryLedgerEntry? TryReadLedgerEntry(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AgentRecoveryLedgerEntry>(
                json,
                CanDoItAll.AgentFramework.Core.AgentOutputJson.SerializerOptions);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed record RecoveryDispatchCandidate(
        Guid RunId,
        Guid? ProjectId,
        Guid ProcessDefinitionId);
}

public sealed class ProcessRunRecoveryStartupGate
{
    private readonly TaskCompletionSource recoveryCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitForStartupRecoveryAsync(CancellationToken cancellationToken)
        => recoveryCompleted.Task.WaitAsync(cancellationToken);

    public void MarkStartupRecoveryCompleted()
        => recoveryCompleted.TrySetResult();
}

public sealed class ProcessRunRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    ProcessRunRecoveryStartupGate startupGate,
    ILogger<ProcessRunRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(2);
    private readonly DateTimeOffset startupCutoffUtc = DateTimeOffset.UtcNow;
    private bool startupExpiredLeaseRecoveryCompleted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
                var reclaimExpiredAutomationDispatchLeases = !startupExpiredLeaseRecoveryCompleted;
                var dispatchedCount = await recoveryService.RecoverActiveRunsAsync(
                    reclaimExpiredAutomationDispatchLeases,
                    startupCutoffUtc,
                    stoppingToken);
                startupExpiredLeaseRecoveryCompleted = true;
                startupGate.MarkStartupRecoveryCompleted();
                if (dispatchedCount == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "ProcessRunRecoveryWorker iteration failed. The worker will retry after {FailureBackoff}.",
                    FailureBackoff);
                await Task.Delay(FailureBackoff, stoppingToken);
            }
        }
    }
}
