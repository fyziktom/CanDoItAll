using CanDoItAll.Processes.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCancellationRecoveryWorker(IServiceScopeFactory scopeFactory,
    IOptions<ProcessCancellationRecoveryOptions> options, TimeProvider clock,
    ILogger<ProcessCancellationRecoveryWorker> logger) : BackgroundService {
    private const int LiveTurnsPerLegacyTurn = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var settings = options.Value;
        if (!settings.Enabled) {
            return;
        }
        var turn = 0;
        try {
            await Task.Delay(settings.IdleDelay, clock, stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested) {
                var origin = turn == LiveTurnsPerLegacyTurn ? ProcessCancellationRecoveryOrigin.LegacySeed : ProcessCancellationRecoveryOrigin.LiveBarrier;
                turn = (turn + 1) % (LiveTurnsPerLegacyTurn + 1);
                var delay = settings.Limits.YieldDelay;
                try {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var coordinator = scope.ServiceProvider.GetRequiredService<ProcessCancellationRecoveryCoordinator>();
                    var result = await coordinator.RunSliceAsync(origin, settings.Limits, stoppingToken).ConfigureAwait(false);
                    delay = result.Status switch {
                        ProcessCancellationRecoverySliceStatus.Idle => settings.IdleDelay,
                        ProcessCancellationRecoverySliceStatus.TimedOut or ProcessCancellationRecoverySliceStatus.InvalidEvidence => settings.Limits.FailureDelay,
                        _ => settings.Limits.YieldDelay
                    };
                    logger.LogDebug("Cancellation recovery preferred {Origin}, root {RootRunId}: {Status} after {Operations} operations, pending {Pending}.",
                        origin, result.RootRunId, result.Status, result.Operations, result.Pending);
                } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                    throw;
                } catch (Exception failure) {
                    delay = settings.Limits.FailureDelay;
                    logger.LogError("Cancellation recovery preferred {Origin} failed with {FailureType}; durable ownership and evidence remain with the queue.",
                        origin, failure.GetType().FullName);
                }
                if (origin == ProcessCancellationRecoveryOrigin.LegacySeed) {
                    await SeedLegacyAsync(settings, stoppingToken).ConfigureAwait(false);
                }
                await Task.Delay(delay, clock, stoppingToken).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
        }
    }

    private async Task SeedLegacyAsync(ProcessCancellationRecoveryOptions settings, CancellationToken stoppingToken) {
        using var deadline = new CancellationTokenSource(settings.Limits.MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token);
        try {
            await using var scope = scopeFactory.CreateAsyncScope();
            var queue = scope.ServiceProvider.GetRequiredService<IProcessCancellationRecoveryQueue>();
            var result = await queue.SeedLegacyAsync(settings.LegacySeedPageSize, bounded.Token).ConfigureAwait(false);
            logger.LogDebug("Cancellation recovery legacy seed {Status}: selected {Selected}, inserted {Inserted}, reopened {Reopened}.",
                result.Status, result.Selected, result.Inserted, result.Reopened);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            throw;
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested) {
            logger.LogWarning("Cancellation recovery legacy seeding exceeded its cooperative deadline; the next scope must read durable progress.");
        } catch (ProcessCancellationRecoveryCorruptRecordException failure) {
            logger.LogError("Historical cancellation recovery for root {RootRunId} requires owner repair; the seed page was not advanced.", failure.RootRunId);
        } catch (Exception failure) {
            logger.LogError("Cancellation recovery legacy seeding failed with {FailureType}; durable progress remains with the queue.",
                failure.GetType().FullName);
        }
    }
}
