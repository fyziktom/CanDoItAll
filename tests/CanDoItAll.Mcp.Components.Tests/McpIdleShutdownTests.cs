using CanDoItAll.Mcp.Core.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Tests;

public sealed class McpIdleShutdownTests
{
    [Fact]
    public void Evaluate_Requests_Stop_After_Inactivity_Timeout()
    {
        var timeProvider = new ManualTimeProvider();
        var activityTracker = new McpIdleActivityTracker(timeProvider);
        var applicationLifetime = new TestHostApplicationLifetime();
        var coordinator = CreateCoordinator(activityTracker, applicationLifetime, timeProvider);

        timeProvider.Advance(TimeSpan.FromSeconds(11));

        var evaluation = coordinator.Evaluate();

        Assert.Equal(McpIdleShutdownEvaluationState.StopRequested, evaluation.State);
        Assert.True(applicationLifetime.StopRequested);
    }

    [Fact]
    public void Evaluate_Does_Not_Stop_While_Operation_Is_Active()
    {
        var timeProvider = new ManualTimeProvider();
        var activityTracker = new McpIdleActivityTracker(timeProvider);
        var applicationLifetime = new TestHostApplicationLifetime();
        var coordinator = CreateCoordinator(activityTracker, applicationLifetime, timeProvider);

        using (activityTracker.BeginOperation())
        {
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            var activeEvaluation = coordinator.Evaluate();

            Assert.Equal(McpIdleShutdownEvaluationState.ActiveOperation, activeEvaluation.State);
            Assert.False(applicationLifetime.StopRequested);
        }

        var completionEvaluation = coordinator.Evaluate();

        Assert.Equal(McpIdleShutdownEvaluationState.Waiting, completionEvaluation.State);
        Assert.False(applicationLifetime.StopRequested);

        timeProvider.Advance(TimeSpan.FromSeconds(11));

        var idleEvaluation = coordinator.Evaluate();

        Assert.Equal(McpIdleShutdownEvaluationState.StopRequested, idleEvaluation.State);
        Assert.True(applicationLifetime.StopRequested);
    }

    [Fact]
    public void Components_Default_Timeout_Is_Short_For_Documentation_Lookups()
    {
        var componentsOptions = new Configuration.McpServerOptions();

        Assert.True(componentsOptions.Server.IdleShutdown.Enabled);
        Assert.Equal(300, componentsOptions.Server.IdleShutdown.InactivityTimeoutSeconds);
        Assert.Equal(15, componentsOptions.Server.IdleShutdown.CheckIntervalSeconds);
    }

    private static McpIdleShutdownCoordinator CreateCoordinator(
        IMcpIdleActivityTracker activityTracker,
        IHostApplicationLifetime applicationLifetime,
        TimeProvider timeProvider)
    {
        return new(
            activityTracker,
            Options.Create(new McpIdleShutdownOptions
            {
                Enabled = true,
                InactivityTimeoutSeconds = 10,
                CheckIntervalSeconds = 1
            }),
            applicationLifetime,
            timeProvider,
            NullLogger<McpIdleShutdownCoordinator>.Instance);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            utcNow += duration;
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => started.Token;

        public CancellationToken ApplicationStopping => stopping.Token;

        public CancellationToken ApplicationStopped => stopped.Token;

        public bool StopRequested { get; private set; }

        public void StopApplication()
        {
            StopRequested = true;
        }
    }
}
