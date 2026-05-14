using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Core.Hosting;

public sealed class McpIdleShutdownOptions
{
    public bool Enabled { get; set; } = true;

    [Range(1, 86_400)]
    public int InactivityTimeoutSeconds { get; set; } = 300;

    [Range(1, 3_600)]
    public int CheckIntervalSeconds { get; set; } = 15;

    public TimeSpan InactivityTimeout => TimeSpan.FromSeconds(InactivityTimeoutSeconds);

    public TimeSpan CheckInterval => TimeSpan.FromSeconds(CheckIntervalSeconds);

    public static McpIdleShutdownOptions Create(int inactivityTimeoutSeconds, int checkIntervalSeconds)
    {
        return new()
        {
            InactivityTimeoutSeconds = inactivityTimeoutSeconds,
            CheckIntervalSeconds = checkIntervalSeconds
        };
    }

    public void CopyFrom(McpIdleShutdownOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Enabled = source.Enabled;
        InactivityTimeoutSeconds = source.InactivityTimeoutSeconds;
        CheckIntervalSeconds = source.CheckIntervalSeconds;
    }
}

public interface IMcpIdleActivityTracker
{
    IDisposable BeginOperation();

    McpIdleActivitySnapshot GetSnapshot();
}

public readonly record struct McpIdleActivitySnapshot(DateTimeOffset LastActivityUtc, int ActiveOperationCount);

public sealed class McpIdleActivityTracker(TimeProvider timeProvider) : IMcpIdleActivityTracker
{
    private readonly object gate = new();
    private DateTimeOffset lastActivityUtc = timeProvider.GetUtcNow();
    private int activeOperationCount;

    public IDisposable BeginOperation()
    {
        lock (gate)
        {
            activeOperationCount++;
            lastActivityUtc = timeProvider.GetUtcNow();
        }

        return new OperationScope(this);
    }

    public McpIdleActivitySnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new McpIdleActivitySnapshot(lastActivityUtc, activeOperationCount);
        }
    }

    private void CompleteOperation()
    {
        lock (gate)
        {
            if (activeOperationCount > 0)
            {
                activeOperationCount--;
            }

            lastActivityUtc = timeProvider.GetUtcNow();
        }
    }

    private sealed class OperationScope(McpIdleActivityTracker tracker) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                tracker.CompleteOperation();
            }
        }
    }
}

public enum McpIdleShutdownEvaluationState
{
    Disabled,
    Waiting,
    ActiveOperation,
    StopRequested,
    StopAlreadyRequested
}

public readonly record struct McpIdleShutdownEvaluation(
    McpIdleShutdownEvaluationState State,
    TimeSpan IdleDuration,
    int ActiveOperationCount);

public sealed class McpIdleShutdownCoordinator(
    IMcpIdleActivityTracker activityTracker,
    IOptions<McpIdleShutdownOptions> options,
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider,
    ILogger<McpIdleShutdownCoordinator> logger)
{
    private int stopRequested;

    public McpIdleShutdownEvaluation Evaluate()
    {
        var configuredOptions = options.Value;
        var snapshot = activityTracker.GetSnapshot();
        var idleDuration = timeProvider.GetUtcNow() - snapshot.LastActivityUtc;

        if (!configuredOptions.Enabled)
        {
            return new(McpIdleShutdownEvaluationState.Disabled, idleDuration, snapshot.ActiveOperationCount);
        }

        if (snapshot.ActiveOperationCount > 0)
        {
            return new(McpIdleShutdownEvaluationState.ActiveOperation, idleDuration, snapshot.ActiveOperationCount);
        }

        if (idleDuration < configuredOptions.InactivityTimeout)
        {
            return new(McpIdleShutdownEvaluationState.Waiting, idleDuration, snapshot.ActiveOperationCount);
        }

        if (Interlocked.Exchange(ref stopRequested, 1) == 1)
        {
            return new(McpIdleShutdownEvaluationState.StopAlreadyRequested, idleDuration, snapshot.ActiveOperationCount);
        }

        logger.LogInformation(
            "Stopping MCP host after idle timeout. IdleDurationSeconds={IdleDurationSeconds}, TimeoutSeconds={TimeoutSeconds}.",
            (int)idleDuration.TotalSeconds,
            configuredOptions.InactivityTimeoutSeconds);

        applicationLifetime.StopApplication();
        return new(McpIdleShutdownEvaluationState.StopRequested, idleDuration, snapshot.ActiveOperationCount);
    }
}

public sealed class McpIdleShutdownHostedService(
    IOptions<McpIdleShutdownOptions> options,
    McpIdleShutdownCoordinator coordinator,
    TimeProvider timeProvider,
    ILogger<McpIdleShutdownHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuredOptions = options.Value;

        if (!configuredOptions.Enabled)
        {
            logger.LogInformation("MCP idle shutdown is disabled.");
            return;
        }

        logger.LogInformation(
            "MCP idle shutdown enabled. TimeoutSeconds={TimeoutSeconds}, CheckIntervalSeconds={CheckIntervalSeconds}.",
            configuredOptions.InactivityTimeoutSeconds,
            configuredOptions.CheckIntervalSeconds);

        using var timer = new PeriodicTimer(configuredOptions.CheckInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                coordinator.Evaluate();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
