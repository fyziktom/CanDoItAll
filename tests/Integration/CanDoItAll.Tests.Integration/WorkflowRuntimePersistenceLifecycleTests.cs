using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Npgsql;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowRuntimePersistenceLifecycleTests
{
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AbortedAdmissionAndTransitionRetryFreshTransactionsWithoutDuplicateEvents() {
        await using var fixture = await RetryFixture.CreateAsync();
        var run = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        fixture.Probe.FailuresRemaining = 2;
        await fixture.Store.CreateRunWithStartedEventAsync(run, CreateEvent(run.RunId, WorkflowEventKind.Started, StartedAtUtc));
        Assert.Equal(3, fixture.Probe.Attempts);
        fixture.Probe.Attempts = 0;
        fixture.Probe.FailuresRemaining = 2;
        var completed = run with { State = WorkflowRunState.Completed, TerminalAtUtc = StartedAtUtc.AddSeconds(1) };
        var result = await fixture.Store.TryTransitionRunAsync(run.RunId, [WorkflowRunState.Running], completed,
            CreateEvent(run.RunId, WorkflowEventKind.Completed, StartedAtUtc.AddSeconds(1)));
        Assert.True(result.Transitioned);
        Assert.Equal(3, fixture.Probe.Attempts);
        var events = await fixture.Store.ListEventsAsync(run.RunId);
        Assert.Equal(2, events.Count);
        Assert.Single(events, e => e.Kind == WorkflowEventKind.Started);
        Assert.Single(events, e => e.Kind == WorkflowEventKind.Completed);
    }

    [Fact]
    public async Task ExhaustedSerializationConflictRollsBackAllAttempts() {
        await using var fixture = await RetryFixture.CreateAsync();
        fixture.Probe.FailuresRemaining = 10;
        var run = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.CreateRunWithStartedEventAsync(run,
            CreateEvent(run.RunId, WorkflowEventKind.Started, StartedAtUtc)));
        Assert.Equal(5, fixture.Probe.Attempts);
        Assert.Null(await fixture.Store.GetRunAsync(run.RunId));
        Assert.Empty(await fixture.Store.ListEventsAsync(run.RunId));
    }

    [Fact]
    public async Task UnrelatedCommitFailureIsNotRetried() {
        await using var fixture = await RetryFixture.CreateAsync();
        fixture.Probe.FailureCode = PostgresErrorCodes.DiskFull;
        fixture.Probe.FailuresRemaining = 10;
        var run = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Store.CreateRunWithStartedEventAsync(run,
            CreateEvent(run.RunId, WorkflowEventKind.Started, StartedAtUtc)));
        Assert.Equal(1, fixture.Probe.Attempts);
        Assert.Null(await fixture.Store.GetRunAsync(run.RunId));
    }

    [Fact]
    public async Task ConcurrentIndependentRunsKeepOneStartedAndTerminalEventEach() {
        await using var fixture = await RetryFixture.CreateAsync();
        var runs = Enumerable.Range(0, 12).Select(_ => CreateRun(WorkflowRunState.Running, StartedAtUtc)).ToArray();
        await Task.WhenAll(runs.Select(async run => {
            await fixture.Store.CreateRunWithStartedEventAsync(run, CreateEvent(run.RunId, WorkflowEventKind.Started, StartedAtUtc));
            var completed = run with { State = WorkflowRunState.Completed, TerminalAtUtc = StartedAtUtc.AddSeconds(1) };
            var result = await fixture.Store.TryTransitionRunAsync(run.RunId, [WorkflowRunState.Running], completed,
                CreateEvent(run.RunId, WorkflowEventKind.Completed, StartedAtUtc.AddSeconds(1)));
            Assert.True(result.Transitioned);
            Assert.Equal(2, (await fixture.Store.ListEventsAsync(run.RunId)).Count);
        }));
    }

    private sealed class RetryFixture(PostgresTestDatabaseLease database, PersistentWorkflowRunStore store, AbortCommitProbe probe) : IAsyncDisposable {
        public PersistentWorkflowRunStore Store { get; } = store;
        public AbortCommitProbe Probe { get; } = probe;

        public static async Task<RetryFixture> CreateAsync() {
            AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
            var database = PostgresTestDatabaseLease.Create("workflowtransactionretry");
            try {
                var probe = new AbortCommitProbe();
                var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions()).AddInterceptors(probe).Options;
                await using var context = new AppDbContext(options);
                await context.Database.EnsureCreatedAsync();
                probe.Attempts = 0;
                return new(database, new(WorkflowOwnerPersistenceTestFactory.FromCanonical(new TestDbContextFactory(options))), probe);
            } catch {
                await database.DisposeAsync();
                throw;
            }
        }

        public ValueTask DisposeAsync() => database.DisposeAsync();
    }

    private sealed class AbortCommitProbe : DbTransactionInterceptor {
        public int FailuresRemaining { get; set; }
        public int Attempts { get; set; }
        public string FailureCode { get; set; } = PostgresErrorCodes.SerializationFailure;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Attempts++;
            if (FailuresRemaining-- > 0) {
                throw new PostgresException("Injected transaction abort.", "ERROR", "ERROR", FailureCode);
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task PersistentStoreEnforcesAtomicLifecycleAndExactlyOnceExternalResponse()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowruntimepersistence");
        var options = database.CreateAppDbContextOptions();
        await using (var dbContext = new AppDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var store = new PersistentWorkflowRunStore(WorkflowOwnerPersistenceTestFactory.FromCanonical(new TestDbContextFactory(options)));
        var running = CreateRun(WorkflowRunState.Running, StartedAtUtc);
        var startedEvent = CreateEvent(running.RunId, WorkflowEventKind.Started, StartedAtUtc);

        await store.CreateRunWithStartedEventAsync(running, startedEvent);

        var persistedRunning = await store.GetRunAsync(running.RunId);
        var initialEvents = await store.ListEventsAsync(running.RunId);
        Assert.NotNull(persistedRunning);
        Assert.Equal(WorkflowRunState.Running, persistedRunning.State);
        Assert.Single(initialEvents, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Started);

        var completedAtUtc = StartedAtUtc.AddSeconds(5);
        var completed = running with
        {
            State = WorkflowRunState.Completed,
            Summary = "Completed.",
            UpdatedAtUtc = completedAtUtc,
            TerminalAtUtc = completedAtUtc
        };
        var completedEvent = CreateEvent(running.RunId, WorkflowEventKind.Completed, completedAtUtc);
        var firstTransition = await store.TryTransitionRunAsync(
            running.RunId,
            [WorkflowRunState.Running],
            completed,
            completedEvent);
        var cancelledAtUtc = completedAtUtc.AddSeconds(1);
        var lateCancelled = completed with
        {
            State = WorkflowRunState.Cancelled,
            Summary = "Late cancellation.",
            UpdatedAtUtc = cancelledAtUtc,
            TerminalAtUtc = cancelledAtUtc
        };
        var lateTransition = await store.TryTransitionRunAsync(
            running.RunId,
            [WorkflowRunState.Running],
            lateCancelled,
            CreateEvent(running.RunId, WorkflowEventKind.Cancelled, cancelledAtUtc));

        var persistedCompleted = await store.GetRunAsync(running.RunId);
        var terminalEvents = await store.ListEventsAsync(running.RunId);
        Assert.True(firstTransition.Transitioned);
        Assert.False(lateTransition.Transitioned);
        Assert.NotNull(persistedCompleted);
        Assert.Equal(WorkflowRunState.Completed, persistedCompleted.State);
        Assert.Equal(completedAtUtc, persistedCompleted.TerminalAtUtc);
        Assert.Single(terminalEvents, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Completed);
        Assert.DoesNotContain(terminalEvents, workflowEvent => workflowEvent.Kind == WorkflowEventKind.Cancelled);

        var waiting = CreateRun(WorkflowRunState.WaitingForInput, StartedAtUtc);
        var request = new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            waiting.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("human"),
            "human-input",
            "{\"question\":\"Continue?\"}",
            string.Empty,
            StartedAtUtc,
            RespondedAtUtc: null);
        await store.SaveRunAsync(waiting);
        await store.SaveExternalRequestAsync(request);

        var accepted = await store.TryAcceptExternalResponseAsync(
            request.Id,
            "{\"answer\":\"yes\"}",
            completedAtUtc);
        var duplicate = await store.TryAcceptExternalResponseAsync(
            request.Id,
            "{\"answer\":\"again\"}",
            cancelledAtUtc);

        Assert.Equal(WorkflowExternalResponseAcceptanceOutcome.Accepted, accepted.Outcome);
        Assert.Equal(WorkflowExternalResponseAcceptanceOutcome.AlreadyResponded, duplicate.Outcome);
        Assert.Equal("{\"answer\":\"yes\"}", duplicate.Request?.ResponseJson);
        Assert.Equal(completedAtUtc, duplicate.Request?.RespondedAtUtc);

        var collisionEventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await store.SaveEventAsync(CreateEvent(
            running.RunId,
            WorkflowEventKind.ExecutorInvoked,
            completedAtUtc,
            collisionEventId));
        var rolledBackRun = CreateRun(WorkflowRunState.Running, StartedAtUtc.AddMinutes(1));
        var collidingStartedEvent = CreateEvent(
            rolledBackRun.RunId,
            WorkflowEventKind.Started,
            rolledBackRun.CreatedAtUtc,
            collisionEventId);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.CreateRunWithStartedEventAsync(rolledBackRun, collidingStartedEvent));

        Assert.Null(await store.GetRunAsync(rolledBackRun.RunId));
        Assert.Empty(await store.ListEventsAsync(rolledBackRun.RunId));
    }

    private static WorkflowRunSnapshot CreateRun(
        WorkflowRunState state,
        DateTimeOffset timestamp)
        => new(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            state,
            WorkflowRuntimeBackendKind.InProcess,
            "persistent-test",
            $"Workflow is {state}.",
            timestamp,
            timestamp);

    private static WorkflowEventRecord CreateEvent(
        WorkflowRunId runId,
        WorkflowEventKind kind,
        DateTimeOffset timestamp,
        Guid? eventId = null)
        => new(
            eventId ?? Guid.NewGuid(),
            runId,
            kind,
            NodeId: null,
            kind.ToString(),
            "{}",
            timestamp);

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }
}
