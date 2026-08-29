using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public sealed class HistoryMaintenanceHostedService(
    IDbContextFactory<AppDbContext> factory,
    IDatabaseRuntimeState runtime,
    IDatabaseRuntimeWriteFence writeFence,
    IEnumerable<IHistorySourceMaintenance> sources,
    HistorySourceMaintenanceRunner sourceRunner,
    IProviderHistoryPartition partitions,
    HistoryHostLeaseStore hostLease,
    HistoryOutboxProcessor projection,
    HistoryRecoveryStore recovery,
    HistoryRetentionStore retention,
    IHostApplicationLifetime lifetime,
    TimeProvider clock,
    ILogger<HistoryMaintenanceHostedService> logger) : BackgroundService {
    private readonly IHistorySourceMaintenance[] orderedSources = sources.OrderBy(source => source.Kind).ToArray();
    private int nextSource;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
        do {
            try {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(10));
                await RunPassAsync(budget.Token);
            } catch (DatabaseRuntimeProfileChangedException) {
                logger.LogInformation("History maintenance will resume on the active database runtime generation.");
            } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                break;
            } catch (OperationCanceledException) {
                logger.LogWarning("History maintenance exceeded its ten-second budget; queued metadata remains available for retry.");
            } catch (Exception exception) {
                logger.LogError("History maintenance failed with {FailureType}; inspect projection checkpoints and database availability.",
                    exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunPassAsync(CancellationToken cancellationToken) {
        var expected = runtime.GetSnapshot();
        var state = await writeFence.ExecuteAsync(expected, async token => {
            var partition = await partitions.GetAsync(token);
            await hostLease.HeartbeatAsync(partition, token);
            await using var db = await factory.CreateDbContextAsync(token);
            var size = await db.Set<HistoryPolicyRow>().AsNoTracking()
                .Where(row => row.PartitionId == partition.StorageLineageId).Select(row => row.BatchSize).SingleAsync(token);
            return (Partition: partition, Size: size);
        }, cancellationToken);
        var context = new HistoryMaintenanceContext(state.Partition, expected, writeFence);
        await context.DatabaseAsync(token => projection.ProcessAsync(state.Partition, state.Size, token), cancellationToken);
        await context.DatabaseAsync(token => recovery.InterruptAbandonedAsync(state.Partition, state.Size, token), cancellationToken);
        await context.DatabaseAsync(token => retention.PurgeExpiredDetailAsync(state.Partition, state.Size, token), cancellationToken);
        await context.DatabaseAsync(token => retention.PurgeExpiredMetadataAsync(state.Partition, state.Size, token), cancellationToken);
        var first = nextSource;
        for (var index = 0; index < orderedSources.Length; index++) {
            var sourceIndex = (first + index) % orderedSources.Length;
            nextSource = (sourceIndex + 1) % orderedSources.Length;
            var source = orderedSources[sourceIndex];
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            try {
                await sourceRunner.ProcessAsync(source, context, Math.Min(state.Size, 100), budget.Token);
            } catch (DatabaseRuntimeProfileChangedException) {
                throw;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                logger.LogWarning("History source {SourceKind} failed with {FailureType}; its checkpoint is retryable.",
                    source.Kind, exception.GetType().Name);
            }
        }
    }
}
