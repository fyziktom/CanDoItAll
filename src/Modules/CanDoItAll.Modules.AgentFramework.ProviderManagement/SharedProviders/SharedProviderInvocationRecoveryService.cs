using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class SharedProviderInvocationRecoveryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock)
{
    public const int DefaultMaximumCount = 100;
    public const int MaximumAllowedCount = 1_000;

    public static readonly TimeSpan InterruptedAfter =
        SharedProviderRelayTarget.MaximumTimeout + TimeSpan.FromMinutes(5);

    public async Task<int> RecoverAsync(
        int maximumCount = DefaultMaximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount is <= 0 or > MaximumAllowedCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        DateTimeOffset recoveredAtUtc = clock.GetUtcNow();
        DateTimeOffset interruptedBeforeUtc = recoveredAtUtc - InterruptedAfter;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await dbContext.Set<SharedProviderInvocationRecord>()
            .AsNoTracking()
            .Where(record =>
                record.Outcome == SharedProviderInvocationOutcome.InProgress &&
                record.StartedAtUtc <= interruptedBeforeUtc)
            .OrderBy(record => record.StartedAtUtc)
            .ThenBy(record => record.Id)
            .Select(record => record.Id)
            .Take(maximumCount)
            .ToArrayAsync(cancellationToken);

        var recoveredCount = 0;
        foreach (Guid candidateId in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryRecoverAsync(
                    candidateId,
                    interruptedBeforeUtc,
                    recoveredAtUtc,
                    cancellationToken))
            {
                recoveredCount++;
            }
        }

        return recoveredCount;
    }

    private async Task<bool> TryRecoverAsync(
        Guid candidateId,
        DateTimeOffset interruptedBeforeUtc,
        DateTimeOffset recoveredAtUtc,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<SharedProviderInvocationRecord>()
            .SingleOrDefaultAsync(candidate => candidate.Id == candidateId, cancellationToken);
        if (record is null ||
            record.Outcome != SharedProviderInvocationOutcome.InProgress ||
            record.StartedAtUtc > interruptedBeforeUtc ||
            !SharedProviderInvocationTransitions.RecoverInterruptedFinalization(
                record,
                recoveredAtUtc))
        {
            return false;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await using var verification = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var winnerOutcome = await verification.Set<SharedProviderInvocationRecord>()
                .AsNoTracking()
                .Where(candidate => candidate.Id == candidateId)
                .Select(candidate => (SharedProviderInvocationOutcome?)candidate.Outcome)
                .SingleOrDefaultAsync(cancellationToken);
            if (winnerOutcome is null || winnerOutcome != SharedProviderInvocationOutcome.InProgress)
            {
                return false;
            }

            throw new SharedProviderConcurrencyException(
                nameof(SharedProviderInvocationRecord),
                candidateId,
                exception);
        }
    }
}

internal sealed record SharedProviderInvocationRecoverySchedule
{
    public SharedProviderInvocationRecoverySchedule(
        TimeSpan startupDelay,
        TimeSpan reconciliationInterval)
    {
        if (startupDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startupDelay));
        }

        if (reconciliationInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(reconciliationInterval));
        }

        StartupDelay = startupDelay;
        ReconciliationInterval = reconciliationInterval;
    }

    public TimeSpan StartupDelay { get; }

    public TimeSpan ReconciliationInterval { get; }

    public static SharedProviderInvocationRecoverySchedule Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1));
}

internal sealed class SharedProviderInvocationRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    SharedProviderInvocationRecoverySchedule schedule,
    ILogger<SharedProviderInvocationRecoveryWorker> logger) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await DelayAsync(schedule.StartupDelay, stoppingToken))
        {
            return;
        }

        var failureReported = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var recoveryService = scope.ServiceProvider
                    .GetRequiredService<SharedProviderInvocationRecoveryService>();
                int recoveredCount = await recoveryService.RecoverAsync(
                    SharedProviderInvocationRecoveryService.DefaultMaximumCount,
                    stoppingToken);
                failureReported = false;
                if (recoveredCount > 0)
                {
                    logger.LogInformation(
                        "Recovered {RecoveredCount} interrupted shared-provider invocation audit record(s).",
                        recoveredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (!failureReported)
                {
                    logger.LogWarning(
                        "Shared-provider invocation audit recovery is temporarily unavailable; retry remains scheduled.");
                    failureReported = true;
                }
            }

            if (!await DelayAsync(schedule.ReconciliationInterval, stoppingToken))
            {
                break;
            }
        }
    }

    private static async Task<bool> DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
