using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanReviewCompletionTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Original_reviewed_publication_completes_the_waiting_step_and_charges_work_once(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        await fixture.PublishAsync();
        var before = await CountsAsync(fixture.Services, fixture.State.RunId);
        var result = await fixture.CompleteAsync();
        await VerifyCompletedAsync(fixture, result);
        var after = await CountsAsync(fixture.Services, fixture.State.RunId);
        Assert.Equal(before.Events + 2, after.Events);
        Assert.Equal(before.Outbox + 2, after.Outbox);
        Assert.Equal(before.Commands + 1, after.Commands);
        var replay = await fixture.Coordinator().CompleteAsync(result.State, fixture.CommandContext with {
            CorrelationId = new("lost-review-response"), OccurredAtUtc = fixture.Clock.GetUtcNow().AddDays(1)
        }, fixture.Request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(result.Decision, replay.Decision);
        Assert.Equal(result.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(after, await CountsAsync(fixture.Services, fixture.State.RunId));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Pending_approval_keeps_original_result_and_reserved_work_until_actual_publication() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var before = await SelectedAsync(fixture);
        var counts = await CountsAsync(fixture.Services, fixture.State.RunId);
        var result = await fixture.CompleteAsync();
        Assert.False(result.Succeeded);
        Assert.Equal("Runtime.ReviewOutcomePending", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(counts, await CountsAsync(fixture.Services, fixture.State.RunId));
        Assert.Equal(before.Budget, (await SelectedAsync(fixture)).Budget);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        await fixture.PublishAsync();
        await VerifyCompletedAsync(fixture, await fixture.CompleteAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Competing_completions_preserve_one_result_settlement_and_event_pair(bool sameKey) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.PublishAsync();
        var before = await CountsAsync(fixture.Services, fixture.State.RunId);
        var rendezvous = new CommitRendezvous();
        var results = await Task.WhenAll(AttemptAsync(fixture.Request), AttemptAsync(sameKey ? fixture.Request :
            fixture.Request with { CompletionKey = new(Guid.NewGuid()) }));
        var accepted = results.Where(item => item.Result?.Succeeded == true).ToArray();
        Assert.Equal(sameKey ? 2 : 1, accepted.Length);
        Assert.Single(accepted, item => item.Result!.CommittedNow);
        if (sameKey) {
            Assert.All(results, item => Assert.Null(item.Failure));
        } else {
            Assert.IsType<ProcessRuntimeOptimisticConcurrencyException>(Assert.Single(results, item => item.Failure is not null).Failure);
        }
        await VerifyCompletedAsync(fixture, accepted[0].Result!);
        var after = await CountsAsync(fixture.Services, fixture.State.RunId);
        Assert.Equal(before.Events + 2, after.Events);
        Assert.Equal(before.Outbox + 2, after.Outbox);
        Assert.Equal(before.Commands + 1, after.Commands);

        async Task<(ProcessRuntimeCommitResult? Result, Exception? Failure)> AttemptAsync(ProcessDecisionReviewRequest request) {
            await using var scope = fixture.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var owner = new BeforeCommit(services.GetRequiredService<IProcessRuntimeUnitOfWork>(), rendezvous.WaitAsync);
            var coordinator = new ProcessDecisionReviewCoordinator(services.GetRequiredService<IProcessAcceptedExecutionInputReader>(),
                services.GetRequiredService<IProcessDecisionResultQuery>(), services.GetServices<IProcessDecisionChildOutcomeReader>(),
                services.GetRequiredService<IProcessIdempotencyStore>(), owner, NullLogger<ProcessDecisionReviewCoordinator>.Instance);
            try {
                return (await coordinator.CompleteAsync(fixture.State, fixture.CommandContext, request), null);
            } catch (ProcessRuntimeOptimisticConcurrencyException failure) {
                return (null, failure);
            }
        }
    }

    [Fact]
    public async Task Failure_after_SQL_flush_rolls_back_result_closure_charge_events_and_outbox_together() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.PublishAsync();
        var before = await SelectedAsync(fixture);
        var counts = await CountsAsync(fixture.Services, fixture.State.RunId);
        var failure = new IOException("review-before-commit");
        await using (var database = Context(fixture.Services, new AfterFlushFault(failure))) {
            var owner = fixture.Process.Owner(database, fixture.Clock);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                fixture.Coordinator(owner, owner).CompleteAsync(fixture.State, fixture.CommandContext, fixture.Request)));
        }
        Assert.Equal(counts, await CountsAsync(fixture.Services, fixture.State.RunId));
        var retained = await SelectedAsync(fixture);
        Assert.Equal(before.Budget, retained.Budget);
        Assert.Equal(before.Head, retained.Head);
        Assert.Null(retained.Child!.Observation.Closure);
        Assert.Null(retained.Child.Observation.Settlement);
        await VerifyCompletedAsync(fixture, await fixture.CompleteAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_or_changed_decision_after_outcome_read_prevents_new_terminal_completion(bool cancelRoot) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        await fixture.PublishAsync();
        var before = await SelectedAsync(fixture);
        var changed = false;
        var owner = new BeforeCommit(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), async token => {
            await using var database = Context(fixture.Services);
            var competing = fixture.Process.Owner(database, fixture.Clock);
            if (cancelRoot) {
                await new ProcessRuntimeEngine(competing).BeginRootCancellationAsync(fixture.State, fixture.CommandContext, token);
            } else {
                await competing.CommitAsync(Request(fixture.State,
                    new ProcessDecisionChangePhase(fixture.Request.Child.StepInstanceId, before.Head!.Expectation,
                        ProcessDecisionPhaseChange.BeginReconciliation)), token);
            }
            changed = true;
        });
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() =>
            fixture.Coordinator(owner).CompleteAsync(fixture.State, fixture.CommandContext, fixture.Request));
        Assert.True(changed);
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Single(state.AppliedResults);
        Assert.NotEqual(ProcessRuntimeStepStatus.Completed, state.Steps.Single().Status);
        var retained = await SelectedAsync(fixture);
        Assert.Equal(before.Budget, retained.Budget);
        Assert.Null(retained.Child!.Observation.Closure);
        Assert.Null(retained.Child.Observation.Settlement);
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_replays_after_full_host_restart_without_another_outcome_read() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-reviewed-completion");
        var profile = environment.CreatePostgreSqlProfile("reviewed-completion");
        ProcessDecisionReviewRequest request;
        RuntimeCommandContext context;
        ProcessRunId runId;
        Counts counts;
        var failure = new IOException("review-commit-ack");
        await using (var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(environment: environment, profile: profile)) {
            await fixture.PublishAsync();
            request = fixture.Request;
            context = fixture.CommandContext;
            runId = fixture.State.RunId;
            await using var database = Context(fixture.Services, new AfterCommitFault(failure));
            var owner = fixture.Process.Owner(database, fixture.Clock);
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                fixture.Coordinator(owner, owner).CompleteAsync(fixture.State, context, request)));
            counts = await CountsAsync(fixture.Services, runId);
            Assert.Single(fixture.Entry.Wire.Requests);
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
                services.RemoveAll<IProcessDecisionChildOutcomeReader>();
                services.AddSingleton<IProcessDecisionChildOutcomeReader, ReadForbidden>();
            }
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId))!;
        var replay = await services.GetRequiredService<ProcessDecisionReviewCoordinator>().CompleteAsync(state, context, request);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.False(replay.CommittedNow);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, replay.State.Steps.Single().Status);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, replay.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(2, replay.State.AppliedResults.Count);
        Assert.Equal(counts, await CountsAsync(services, runId));
    }

    private static async Task VerifyCompletedAsync(ProjectTaskPlanReviewFixture fixture, ProcessRuntimeCommitResult result) {
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        Assert.Equal(ProcessRuntimeStatus.Completed, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Completed, result.State.Steps.Single().Status);
        Assert.Equal(JsonSerializer.Serialize(fixture.State.AppliedResults.Single()), JsonSerializer.Serialize(result.State.AppliedResults[0]));
        Assert.Equal(StrategyOutcome.Succeeded, result.State.AppliedResults[1].Outcome);
        Assert.Equal(fixture.State.Claims.Single(), result.State.Claims.Single());
        Assert.Equal(ProcessDecisionPhase.Completed, result.Decision!.Current.Phase);
        Assert.Equal(ProcessDecisionChildOutcome.Accepted, result.Decision.Child!.Closure!.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, result.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        var retained = await SelectedAsync(fixture);
        Assert.Equal(result.DecisionBudget.Change.Balance, retained.Budget);
        Assert.Equal(result.Decision.Child.Closure, retained.Child!.Observation.Closure);
        Assert.Equal(result.DecisionBudget.Change.Allocation.Settlement, retained.Child.Observation.Settlement);
        await using var database = Context(fixture.Services);
        Assert.Equal(1, await database.RuntimeEvents.CountAsync(item => item.RunId == fixture.State.RunId.Value &&
            item.EventType == ProcessRuntimeEventTypes.DispatchClaimCompleted.Value));
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    private static async Task<ProcessDecisionResultSelection> SelectedAsync(ProjectTaskPlanReviewFixture fixture)
        => (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;

    private static async Task<Counts> CountsAsync(IServiceProvider services, ProcessRunId runId) {
        await using var database = Context(services);
        var events = database.RuntimeEvents.Where(item => item.RunId == runId.Value).Select(item => item.EventId);
        return new(await events.CountAsync(), await database.OutboxMessages.CountAsync(item => events.Contains(item.EventId)),
            await database.IdempotencyKeys.CountAsync(item => item.RunId == runId.Value));
    }

    private sealed record Counts(int Events, int Outbox, int Commands);

    private sealed class BeforeCommit(IProcessRuntimeUnitOfWork inner, Func<CancellationToken, Task> before) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            await before(cancellationToken);
            return await inner.CommitAsync(request, cancellationToken);
        }
    }

    private sealed class CommitRendezvous {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;
        internal async Task WaitAsync(CancellationToken token) {
            if (Interlocked.Increment(ref arrived) == 2) {
                ready.TrySetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        }
    }

    private sealed class AfterFlushFault(Exception failure) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
    }

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed class ReadForbidden : IProcessDecisionChildOutcomeReader {
        public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;
        public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Committed review replay cannot read transient child evidence.");
    }
}
