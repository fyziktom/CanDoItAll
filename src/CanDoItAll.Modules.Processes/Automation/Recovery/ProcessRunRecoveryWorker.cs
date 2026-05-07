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

    public Task<int> RecoverActiveRunsAsync(CancellationToken cancellationToken = default)
        => RecoverActiveRunsAsync(reclaimStrandedAutomationDispatchLeases: false, cancellationToken);

    public async Task<int> RecoverActiveRunsAsync(
        bool reclaimStrandedAutomationDispatchLeases,
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
        if (candidates.Count == 0)
        {
            return 0;
        }

        if (reclaimStrandedAutomationDispatchLeases)
        {
            var releasedLeaseCount = await ReleaseStrandedAutomationDispatchLeasesAsync(
                dbContext,
                candidates.Select(candidate => candidate.RunId).ToArray(),
                cancellationToken);
            if (releasedLeaseCount > 0)
            {
                logger.LogWarning(
                    "Released {ReleasedLeaseCount} stranded process automation dispatch lease(s) during runtime recovery startup scan.",
                    releasedLeaseCount);
            }
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

        return ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
            executionRuns,
            clock.GetUtcNow());
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

    private async Task<int> ReleaseStrandedAutomationDispatchLeasesAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> runIds,
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
            .Where(item =>
                item.ProcessRunId.HasValue &&
                runIdSet.Contains(item.ProcessRunId.Value) &&
                item.LeaseExpiresAtUtc.HasValue &&
                item.LeaseExpiresAtUtc.Value > now)
            .ToList();
        foreach (var record in records)
        {
            record.LeaseToken = string.Empty;
            record.LeaseExpiresAtUtc = null;
            record.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return records.Count;
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
    private bool startupLeaseRecoveryCompleted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
                var reclaimStrandedAutomationDispatchLeases = !startupLeaseRecoveryCompleted;
                var dispatchedCount = await recoveryService.RecoverActiveRunsAsync(
                    reclaimStrandedAutomationDispatchLeases,
                    stoppingToken);
                startupLeaseRecoveryCompleted = true;
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
