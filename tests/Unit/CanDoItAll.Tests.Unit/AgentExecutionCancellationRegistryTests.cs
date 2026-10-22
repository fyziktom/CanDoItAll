using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentExecutionCancellationRegistryTests
{
    [Fact]
    public void Exact_execution_stop_signals_only_the_selected_run_and_reports_disposal() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        var selected = CreateRun("shared-process");
        using var target = registry.Register(selected, default);
        using var sibling = registry.Register(CreateRun("shared-process"), default);
        Assert.True(registry.RequestCancellation(selected.Id));
        Assert.True(registry.RequestCancellation(selected.Id));
        Assert.True(target.Token.IsCancellationRequested);
        Assert.False(sibling.Token.IsCancellationRequested);
        target.Dispose();
        Assert.False(registry.RequestCancellation(selected.Id));
        Assert.False(registry.RequestCancellation(Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => registry.RequestCancellation(Guid.Empty));
    }

    [Fact]
    public void RequestCancellationByProcessRunIds_cancels_only_matching_active_execution()
    {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        using var matching = registry.Register(CreateRun("process-run-001"), CancellationToken.None);
        using var unrelated = registry.Register(CreateRun("process-run-002"), CancellationToken.None);

        var cancelledCount = registry.RequestCancellationByProcessRunIds(
            ["process-run-001"],
            "unit-test",
            "Stop matching process run.");

        Assert.Equal(1, cancelledCount);
        Assert.True(matching.Token.IsCancellationRequested);
        Assert.False(unrelated.Token.IsCancellationRequested);
    }

    [Fact]
    public void RequestCancellationByProcessRunIds_ignores_disposed_registration()
    {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        var registration = registry.Register(CreateRun("process-run-001"), CancellationToken.None);
        registration.Dispose();

        var cancelledCount = registry.RequestCancellationByProcessRunIds(
            ["process-run-001"],
            "unit-test",
            "Disposed registration should no longer be active.");

        Assert.Equal(0, cancelledCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Signals_return_while_callbacks_are_blocked_and_disposal_retains_the_exact_identity(bool byProcess) {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        var run = CreateRun("selected-process");
        var registration = registry.Register(run, default);
        using var sibling = registry.Register(CreateRun("other-process"), default);
        var entered = Signal();
        var release = Signal();
        using var callback = registration.Token.Register(() => {
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        });
        var request = Task.Run(() => byProcess
            ? registry.RequestCancellationByProcessRunIds([run.ProcessRunId], "fixture", "Stop selected work.") == 1
            : registry.RequestCancellation(run.Id));
        try {
            Assert.True(await request.WaitAsync(Deadline));
            Assert.True(registration.IsCancellationRequested);
            Assert.False(sibling.IsCancellationRequested);
            await entered.Task.WaitAsync(Deadline);
            Assert.True(registry.RequestCancellation(run.Id));
            Assert.Equal(0, registry.RequestCancellationByProcessRunIds([run.ProcessRunId], "fixture", "Repeated stop."));
            registration.Dispose();
            Assert.False(registry.RequestCancellation(run.Id));
            var drained = registration.DisposeAsync().AsTask();
            Assert.False(drained.IsCompleted);
            Assert.Throws<InvalidOperationException>(() => registry.Register(run, default));
        } finally {
            release.TrySetResult();
            await request.WaitAsync(Deadline);
            await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
        }
        using var replacement = registry.Register(run, default);
        registration.Dispose();
        Assert.True(registry.RequestCancellation(run.Id));
        Assert.True(replacement.IsCancellationRequested);
    }

    [Fact]
    public async Task Upstream_cancellation_signals_without_waiting_for_consumer_callbacks() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        using var parent = new CancellationTokenSource();
        var run = CreateRun("upstream-process");
        var registration = registry.Register(run, parent.Token);
        var entered = Signal();
        var release = Signal();
        using var callback = registration.Token.Register(() => {
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        });
        var request = Task.Run(() => parent.Cancel());
        try {
            await request.WaitAsync(Deadline);
            Assert.True(registration.Token.IsCancellationRequested);
            await entered.Task.WaitAsync(Deadline);
            Assert.True(registry.RequestCancellation(run.Id));
            Assert.Equal(0, registry.RequestCancellationByProcessRunIds([run.ProcessRunId], "fixture", "Already signalled upstream."));
        } finally {
            release.TrySetResult();
            await request.WaitAsync(Deadline);
            await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
        }
    }

    [Fact]
    public async Task Already_cancelled_upstream_is_visible_before_registration_returns() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        using var parent = new CancellationTokenSource();
        parent.Cancel();
        var run = CreateRun("already-cancelled");
        await using var registration = registry.Register(run, parent.Token);
        Assert.True(registration.IsCancellationRequested);
        Assert.True(registry.RequestCancellation(run.Id));
        Assert.Equal(0, registry.RequestCancellationByProcessRunIds([run.ProcessRunId], "fixture", "Already signalled."));
    }

    [Fact]
    public async Task Concurrent_exact_group_and_upstream_signals_execute_each_callback_once() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        using var parent = new CancellationTokenSource();
        var run = CreateRun("shared-signals");
        var registration = registry.Register(run, parent.Token);
        var entered = Signal();
        var release = Signal();
        var calls = 0;
        using var callback = registration.Token.Register(() => {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
        });
        var requests = Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() => {
            if (index % 3 == 0) {
                Assert.True(registry.RequestCancellation(run.Id));
            } else if (index % 3 == 1) {
                registry.RequestCancellationByProcessRunIds([run.ProcessRunId], "fixture", "Concurrent group signal.");
            } else {
                parent.Cancel();
            }
        })));
        try {
            await requests.WaitAsync(Deadline);
            await entered.Task.WaitAsync(Deadline);
            Assert.Equal(1, Volatile.Read(ref calls));
            Assert.True(registration.IsCancellationRequested);
            Assert.False(registration.DisposeAsync().IsCompleted);
            Assert.False(registration.DisposeAsync().IsCompleted);
        } finally {
            release.TrySetResult();
            await requests.WaitAsync(Deadline);
            await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
        }
        Assert.Equal(1, calls);
        using var replacement = registry.Register(run, default);
        Assert.False(replacement.IsCancellationRequested);
    }

    [Fact]
    public async Task A_callback_can_dispose_its_registration_without_waiting_for_itself() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        var run = CreateRun("self-disposal");
        var registration = registry.Register(run, default);
        var returned = Signal();
        using var callback = registration.Token.Register(() => {
            registration.Dispose();
            returned.TrySetResult();
        });
        Assert.True(registry.RequestCancellation(run.Id));
        await returned.Task.WaitAsync(Deadline);
        await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
        Assert.False(registry.RequestCancellation(run.Id));
        using var replacement = registry.Register(run, default);
        registration.Dispose();
        Assert.True(registry.RequestCancellation(run.Id));
    }

    [Fact]
    public async Task Callback_faults_are_observed_once_without_logging_callback_data_or_skipping_other_callbacks() {
        var logger = new RecordingLogger();
        var registry = new AgentExecutionCancellationRegistry(logger);
        var run = CreateRun("faulting-callbacks");
        var registration = registry.Register(run, default);
        const string privateMessage = "private-callback-content-must-not-be-logged";
        var completed = 0;
        using var first = registration.Token.Register(() => throw new InvalidOperationException(privateMessage));
        using var second = registration.Token.Register(() => throw new IOException(privateMessage));
        using var remaining = registration.Token.Register(() => Interlocked.Increment(ref completed));
        Assert.True(registry.RequestCancellation(run.Id));
        Assert.True(registry.RequestCancellation(run.Id));
        await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
        Assert.Equal(1, completed);
        Assert.True(registration.IsCancellationRequested);
        var failure = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Contains(run.Id.ToString(), failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2 failures", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(privateMessage, failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.Exception);
        using var replacement = registry.Register(run, default);
        Assert.False(replacement.IsCancellationRequested);
    }

    [Fact]
    public async Task Disposal_unlinks_upstream_and_old_disposal_cannot_remove_a_replacement() {
        var registry = new AgentExecutionCancellationRegistry(NullLogger<AgentExecutionCancellationRegistry>.Instance);
        using var parent = new CancellationTokenSource();
        var run = CreateRun("disposed-parent-link");
        var original = registry.Register(run, parent.Token);
        original.Dispose();
        parent.Cancel();
        Assert.False(original.IsCancellationRequested);
        await using var replacement = registry.Register(run, default);
        await original.DisposeAsync();
        Assert.True(registry.RequestCancellation(run.Id));
        Assert.True(replacement.IsCancellationRequested);
        Assert.False(original.IsCancellationRequested);
    }

    [Fact]
    public async Task Invalid_or_duplicate_registration_does_not_remove_the_live_owner() {
        var logger = NullLogger<AgentExecutionCancellationRegistry>.Instance;
        var registry = new AgentExecutionCancellationRegistry(logger);
        var run = CreateRun("registration-validation");
        await using var registration = registry.Register(run, default);
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!, default));
        Assert.Throws<ArgumentException>(() => registry.Register(run with { Id = Guid.Empty }, default));
        Assert.Throws<ArgumentNullException>(() => new AgentExecutionCancellationRegistry((ILogger<AgentExecutionCancellationRegistry>)null!));
        Assert.Throws<InvalidOperationException>(() => registry.Register(run, default));
        Assert.True(registry.RequestCancellation(run.Id));
        Assert.True(registration.IsCancellationRequested);
    }

    [Fact]
    public async Task Concurrent_signal_and_disposal_leave_no_identity_or_source_lifetime_race() {
        var logger = new RecordingLogger();
        var registry = new AgentExecutionCancellationRegistry(logger);
        for (var index = 0; index < 32; index++) {
            using var parent = new CancellationTokenSource();
            var run = CreateRun("disposal-race");
            var registration = registry.Register(run, parent.Token);
            var start = Signal();
            var signal = Task.Run(async () => {
                await start.Task;
                registry.RequestCancellation(run.Id);
            });
            var upstream = Task.Run(async () => {
                await start.Task;
                parent.Cancel();
            });
            var disposal = Task.Run(async () => {
                await start.Task;
                registration.Dispose();
            });
            start.TrySetResult();
            await Task.WhenAll(signal, upstream, disposal).WaitAsync(Deadline);
            await registration.DisposeAsync().AsTask().WaitAsync(Deadline);
            Assert.False(registry.RequestCancellation(run.Id));
            await using var replacement = registry.Register(run, default);
            registration.Dispose();
            Assert.True(registry.RequestCancellation(run.Id));
        }
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_composition_resolves_one_logged_registry_without_creating_a_workspace(bool module) {
        var workspace = Path.Combine(Path.GetTempPath(), $"candoitall-cancellation-composition-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(workspace));
        try {
            var services = new ServiceCollection();
            services.AddLogging();
            if (module) {
                services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());
            } else {
                services.AddAgentFrameworkCore(workspace);
            }
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await using var scope = provider.CreateAsyncScope();
            var registry = Assert.IsType<AgentExecutionCancellationRegistry>(provider.GetRequiredService<IAgentExecutionCancellationRegistry>());
            Assert.Same(registry, scope.ServiceProvider.GetRequiredService<IAgentExecutionCancellationRegistry>());
            var run = CreateRun("composition");
            await using var registration = registry.Register(run, default);
            Assert.True(registry.RequestCancellation(run.Id));
            Assert.True(registration.IsCancellationRequested);
            Assert.False(Directory.Exists(workspace));
        } finally {
            if (Directory.Exists(workspace)) {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class RecordingLogger : ILogger<AgentExecutionCancellationRegistry> {
        internal ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((level, formatter(state, exception), exception));
    }

    private static ExecutionRunRecord CreateRun(string processRunId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Process step",
            SourceKind: "process-step",
            SourceId: "step-001",
            CorrelationId: processRunId,
            CausationId: Guid.NewGuid().ToString("D"),
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: "{}",
            InputSummary: "Run process step.",
            ResultSummary: string.Empty,
            ProviderName: "OpenAI",
            Model: "gpt-5",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: "runtime-session-key",
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: processRunId,
            ProcessStepId: Guid.NewGuid().ToString("D"));
    }
}
