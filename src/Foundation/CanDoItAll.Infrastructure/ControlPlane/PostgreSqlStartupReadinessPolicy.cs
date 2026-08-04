using CanDoItAll.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Runtime.ExceptionServices;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal sealed class PostgreSqlStartupReadinessPolicy
{
    private readonly PostgreSqlStartupReadinessOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PostgreSqlStartupReadinessPolicy> logger;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    public PostgreSqlStartupReadinessPolicy(
        IOptions<PostgreSqlStartupReadinessOptions> options,
        TimeProvider timeProvider,
        ILogger<PostgreSqlStartupReadinessPolicy> logger)
        : this(
            options.Value,
            timeProvider,
            logger,
            (delay, cancellationToken) => Task.Delay(delay, timeProvider, cancellationToken))
    {
    }

    internal PostgreSqlStartupReadinessPolicy(
        PostgreSqlStartupReadinessOptions options,
        TimeProvider timeProvider,
        ILogger<PostgreSqlStartupReadinessPolicy> logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.options = options;
        this.timeProvider = timeProvider;
        this.logger = logger;
        this.delayAsync = delayAsync;
    }

    internal async Task ExecuteAsync(
        Guid profileId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            profileId,
            async operationCancellationToken =>
            {
                await operation(operationCancellationToken);
                return true;
            },
            cancellationToken);
    }

    internal async Task<TResult> ExecuteAsync<TResult>(
        Guid profileId,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var startedTimestamp = timeProvider.GetTimestamp();
        PostgresException? lastStartupException = null;
        var attempt = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var elapsed = timeProvider.GetElapsedTime(startedTimestamp);
            if (lastStartupException is not null && elapsed >= options.Timeout)
            {
                LogExhaustion(profileId, attempt, elapsed);
                ExceptionDispatchInfo.Capture(lastStartupException).Throw();
            }

            attempt++;
            var remainingForAttempt = options.Timeout - elapsed;
            using var timeoutCancellation = new CancellationTokenSource(remainingForAttempt, timeProvider);
            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);
            try
            {
                var result = await operation(operationCancellation.Token);
                if (timeoutCancellation.IsCancellationRequested)
                {
                    await DisposeRejectedResultAsync(result);
                    LogAttemptTimeout(profileId, attempt, timeProvider.GetElapsedTime(startedTimestamp));
                    throw CreateTimeoutException(profileId, attempt);
                }

                if (attempt > 1)
                {
                    logger.LogInformation(
                        "PostgreSQL profile {ProfileId} accepted a connection after {AttemptCount} attempts and {ElapsedMilliseconds} ms.",
                        profileId,
                        attempt,
                        timeProvider.GetElapsedTime(startedTimestamp).TotalMilliseconds);
                }

                return result;
            }
            catch (OperationCanceledException exception) when (
                !cancellationToken.IsCancellationRequested &&
                timeoutCancellation.IsCancellationRequested)
            {
                LogAttemptTimeout(profileId, attempt, timeProvider.GetElapsedTime(startedTimestamp));
                throw CreateTimeoutException(profileId, attempt, exception);
            }
            catch (PostgresException exception) when (
                exception.SqlState == PostgresErrorCodes.CannotConnectNow &&
                !cancellationToken.IsCancellationRequested)
            {
                lastStartupException = exception;
                elapsed = timeProvider.GetElapsedTime(startedTimestamp);
                var remaining = options.Timeout - elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    LogExhaustion(profileId, attempt, elapsed);
                    throw;
                }

                var configuredDelay = CalculateDelay(attempt);
                var delay = configuredDelay <= remaining ? configuredDelay : remaining;
                LogRetry(profileId, attempt, elapsed, remaining, delay);
                await delayAsync(delay, cancellationToken);
            }
        }
    }

    private static async ValueTask DisposeRejectedResultAsync<TResult>(TResult result)
    {
        switch (result)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;

            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    private TimeoutException CreateTimeoutException(
        Guid profileId,
        int attempt,
        Exception? innerException = null)
    {
        return new TimeoutException(
            $"PostgreSQL profile '{profileId}' did not accept a connection within the configured startup readiness timeout after {attempt} attempts.",
            innerException);
    }

    private TimeSpan CalculateDelay(int failedAttempt)
    {
        var delayTicks = options.InitialRetryDelay.Ticks;
        for (var retry = 1; retry < failedAttempt && delayTicks < options.MaximumRetryDelay.Ticks; retry++)
        {
            delayTicks = delayTicks > options.MaximumRetryDelay.Ticks / 2
                ? options.MaximumRetryDelay.Ticks
                : delayTicks * 2;
        }

        return TimeSpan.FromTicks(Math.Min(delayTicks, options.MaximumRetryDelay.Ticks));
    }

    private void LogRetry(
        Guid profileId,
        int attempt,
        TimeSpan elapsed,
        TimeSpan remaining,
        TimeSpan delay)
    {
        if (attempt == 1)
        {
            logger.LogWarning(
                "PostgreSQL profile {ProfileId} is still starting. SqlState={SqlState}, Attempt={Attempt}, ElapsedMilliseconds={ElapsedMilliseconds}, RemainingMilliseconds={RemainingMilliseconds}, NextDelayMilliseconds={NextDelayMilliseconds}.",
                profileId,
                PostgresErrorCodes.CannotConnectNow,
                attempt,
                elapsed.TotalMilliseconds,
                remaining.TotalMilliseconds,
                delay.TotalMilliseconds);
            return;
        }

        logger.LogInformation(
            "PostgreSQL profile {ProfileId} is still starting. SqlState={SqlState}, Attempt={Attempt}, ElapsedMilliseconds={ElapsedMilliseconds}, RemainingMilliseconds={RemainingMilliseconds}, NextDelayMilliseconds={NextDelayMilliseconds}.",
            profileId,
            PostgresErrorCodes.CannotConnectNow,
            attempt,
            elapsed.TotalMilliseconds,
            remaining.TotalMilliseconds,
            delay.TotalMilliseconds);
    }

    private void LogExhaustion(Guid profileId, int attempt, TimeSpan elapsed)
    {
        logger.LogError(
            "PostgreSQL profile {ProfileId} did not become ready within the configured startup window. SqlState={SqlState}, AttemptCount={AttemptCount}, ElapsedMilliseconds={ElapsedMilliseconds}, TimeoutMilliseconds={TimeoutMilliseconds}.",
            profileId,
            PostgresErrorCodes.CannotConnectNow,
            attempt,
            elapsed.TotalMilliseconds,
            options.Timeout.TotalMilliseconds);
    }

    private void LogAttemptTimeout(Guid profileId, int attempt, TimeSpan elapsed)
    {
        logger.LogError(
            "PostgreSQL profile {ProfileId} connection attempt exceeded the configured startup window. Attempt={Attempt}, ElapsedMilliseconds={ElapsedMilliseconds}, TimeoutMilliseconds={TimeoutMilliseconds}.",
            profileId,
            attempt,
            elapsed.TotalMilliseconds,
            options.Timeout.TotalMilliseconds);
    }
}
