using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRunRecoveryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IProcessRunAutomationDispatchService automationDispatchService,
    ILogger<ProcessRunRecoveryService> logger)
{
    private const int MaxRunBatchSize = 10;
    private const string RecoveryTrigger = "runtime-recovery-scan";

    public async Task<int> RecoverActiveRunsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidateRunIds = await (
                from run in dbContext.Set<ProcessRun>().AsNoTracking()
                where run.Status != ProcessRunStatus.Completed
                   && run.Status != ProcessRunStatus.Cancelled
                   && run.Status != ProcessRunStatus.Failed
                join step in dbContext.Set<ProcessStepRun>().AsNoTracking() on run.Id equals step.ProcessRunId
                where step.CurrentExecutorPartyId.HasValue &&
                      (step.Status == ProcessStepRunStatus.Ready ||
                       step.Status == ProcessStepRunStatus.WaitingApproval ||
                       step.Status == ProcessStepRunStatus.InProgress)
                group step by run.Id
                into runSteps
                orderby runSteps.Min(item => item.Sequence)
                select runSteps.Key)
            .Take(MaxRunBatchSize)
            .ToListAsync(cancellationToken);
        if (candidateRunIds.Count == 0)
        {
            return 0;
        }

        var dispatchedCount = 0;
        foreach (var runId in candidateRunIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await ShouldSkipForRecoveryBackoffAsync(dbContext, runId, cancellationToken))
                {
                    continue;
                }

                await automationDispatchService.DispatchAsync(
                    runId,
                    triggerStepRunId: null,
                    RecoveryTrigger,
                    cancellationToken: cancellationToken);
                dispatchedCount++;
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
                    runId);
            }
        }

        return dispatchedCount;
    }

    private static async Task<bool> ShouldSkipForRecoveryBackoffAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var ledgerPayloads = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId &&
                           item.EventType == ProcessRuntimeEventTypes.AgentRecoveryAttemptRecorded)
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(20)
            .Select(item => item.ReplayContextJson)
            .ToListAsync(cancellationToken);
        var entries = ledgerPayloads
            .Select(TryReadLedgerEntry)
            .OfType<AgentRecoveryLedgerEntry>()
            .ToList();
        return !AgentRecoveryLedger.CanAttemptNow(entries, now);
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
}

public sealed class ProcessRunRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ProcessRunRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recoveryService = scope.ServiceProvider.GetRequiredService<ProcessRunRecoveryService>();
                var dispatchedCount = await recoveryService.RecoverActiveRunsAsync(stoppingToken);
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
