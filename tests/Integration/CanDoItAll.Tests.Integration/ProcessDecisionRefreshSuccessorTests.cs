using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshSuccessorTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Captured_successor_preserves_history_replays_and_enrolls_the_next_scheduler_attempt(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var candidate = await PrepareAsync(fixture, mode);
        var original = candidate.Request.OriginalState;
        var result = await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(candidate.Request);
        Assert.True(result.CommittedNow);
        Assert.Equal(ProcessRuntimeStatus.Active, result.State.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, result.State.Steps.Single().Status);
        Assert.Equal(original.Steps.Single().AttemptNumber, result.State.Steps.Single().AttemptNumber);
        Assert.Equal(JsonSerializer.Serialize(original.Claims), JsonSerializer.Serialize(result.State.Claims));
        Assert.Equal(JsonSerializer.Serialize(original.AppliedResults), JsonSerializer.Serialize(result.State.AppliedResults));
        Assert.Equal(ProcessDecisionPhase.Superseded, result.Decision!.Previous!.Phase);
        Assert.Equal(candidate.Intent.Request.CommandId, result.Decision.Previous.RefreshCommandId);
        Assert.Equal(ProcessDecisionPhase.Active, result.Decision.Current.Phase);
        Assert.Null(result.Decision.Current.RefreshCommandId);
        Assert.Equal(fixture.Parent.Child.Address.Reference.Generation + 1, result.Decision.Current.Reference.Generation);
        Assert.Equal(candidate.Material.Reference.MaterialId, result.Decision.Current.MaterialId);
        Assert.Equal(0, result.Decision.Current.ChildCount);
        Assert.Equal(0, result.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(0, result.DecisionBudget.Funding!.Receipt.Change.Balance.ReservedWorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Allocation.Settlement!.Consumed.Replans);
        var beforeReplay = await RetainedAsync(fixture.Services, original.RunId);
        await using (var scope = fixture.Services.CreateAsyncScope()) {
            var replay = await scope.ServiceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(candidate.Request);
            Assert.Equal(result.Outcome, replay.Outcome);
            Assert.False(replay.CommittedNow);
            Assert.Equal(result.Decision, replay.Decision);
            Assert.Equal(result.DecisionBudget, replay.DecisionBudget);
        }
        Assert.Equal(beforeReplay, await RetainedAsync(fixture.Services, original.RunId));
        var plan = (await fixture.Services.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(result.State.PlanId))!;
        var work = Assert.Single(new ProcessRuntimeScheduler().CalculateReadyWork(result.State, plan, fixture.Clock.GetUtcNow()));
        Assert.Equal(original.Steps.Single().AttemptNumber + 1, work.AttemptNumber);
        DispatcherOwnerId owner = new("refresh-successor-fixture");
        DispatchClaimToken token = new(Guid.NewGuid());
        var claimed = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .CreateClaimAsync(result.State, fixture.CommandContext, new(work, owner, token, fixture.Clock.GetUtcNow().AddMinutes(1)));
        Assert.True(claimed.CommittedNow);
        var enrolled = await fixture.Services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
            .PrepareAsync(claimed.State, new(work.StepInstanceId, token, owner), TimeSpan.FromSeconds(30));
        Assert.True(enrolled.EnrolledNow);
        Assert.NotEqual(fixture.Parent.Child.Address.ChildId, enrolled.Child!.Child.Address.ChildId);
        Assert.Equal(result.Decision.Current.Reference, enrolled.Child.Child.Address.Reference);
        Assert.Equal(candidate.Material.Stamp, enrolled.Child.Material);
        Assert.Equal(fixture.Parent.ExecutionContract, enrolled.Child.ExecutionContract);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Competing_successors_commit_one_generation_and_one_settlement(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var candidate = await PrepareAsync(fixture, mode);
        var arrived = 0;
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = await Task.WhenAll(AttemptAsync(), AttemptAsync());
        Assert.Single(results.Where(item => item.CommittedNow));
        Assert.Single(results.Where(item => !item.CommittedNow));
        Assert.Equal(results[0].Decision, results[1].Decision);
        Assert.Equal(results[0].DecisionBudget, results[1].DecisionBudget);
        await using var context = Context(fixture.Services);
        Assert.Equal(2, await context.DecisionGenerations.CountAsync());
        Assert.Equal(1, await context.IdempotencyKeys.CountAsync(row => row.CommandId == candidate.Request.CommandId.Value));

        async Task<ProcessRuntimeCommitResult> AttemptAsync() {
            await using var scope = fixture.Services.CreateAsyncScope();
            if (Interlocked.Increment(ref arrived) == 2) {
                ready.TrySetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return await scope.ServiceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(candidate.Request);
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Failed_flush_rolls_back_readiness_material_membership_and_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var candidate = await PrepareAsync(fixture, mode);
        var before = await RetainedAsync(fixture.Services, fixture.State.RunId);
        var failure = new IOException("Successor interrupted before commit.");
        await using (var context = Context(fixture.Services, new AfterFlush(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Owner(fixture, context).CommitAsync(candidate.Request)));
        }
        Assert.Equal(before, await RetainedAsync(fixture.Services, fixture.State.RunId));
        Assert.True((await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(candidate.Request)).CommittedNow);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Lost_acknowledgement_replays_the_exact_successor_after_host_restart(ReadBasisMode mode) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-refresh-successor");
        var profile = environment.CreatePostgreSqlProfile("refresh-successor");
        ProcessRuntimeCommitRequest request;
        string retained;
        var failure = new IOException("Successor acknowledgement lost.");
        await using (var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile)) {
            request = (await PrepareAsync(fixture, mode)).Request;
            await using var context = Context(fixture.Services, new AfterCommit(failure));
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => Owner(fixture, context).CommitAsync(request)));
            retained = await RetainedAsync(fixture.Services, request.OriginalState.RunId);
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
            }
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var replay = await scope.ServiceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request);
        Assert.True(replay.Succeeded);
        Assert.False(replay.CommittedNow);
        Assert.Equal(retained, await RetainedAsync(scope.ServiceProvider, request.OriginalState.RunId));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Cancellation_after_capture_fences_readiness_and_preserves_unsettled_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var candidate = await PrepareAsync(fixture, mode);
        var cancelled = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .BeginRootCancellationAsync(candidate.Request.OriginalState, fixture.CommandContext);
        Assert.True(cancelled.CommittedNow);
        var before = await RetainedAsync(fixture.Services, fixture.State.RunId);
        await Assert.ThrowsAsync<ProcessRuntimeOptimisticConcurrencyException>(() => fixture.Services
            .GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(candidate.Request));
        Assert.Equal(before, await RetainedAsync(fixture.Services, fixture.State.RunId));
    }

    public enum InvalidSuccessor { MissingAccounting, OriginalAllocation, ResetAttempt, RewriteHistory, DifferentActor }

    [Theory]
    [InlineData(InvalidSuccessor.MissingAccounting)]
    [InlineData(InvalidSuccessor.OriginalAllocation)]
    [InlineData(InvalidSuccessor.ResetAttempt)]
    [InlineData(InvalidSuccessor.RewriteHistory)]
    [InlineData(InvalidSuccessor.DifferentActor)]
    public async Task Substituted_authority_or_mutation_cannot_advance_the_reconciled_head(InvalidSuccessor invalid) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync();
        var candidate = await PrepareAsync(fixture, ReadBasisMode.StepSnapshot);
        var request = candidate.Request;
        var mutation = request.Mutation;
        if (invalid == InvalidSuccessor.MissingAccounting) {
            await using var context = Context(fixture.Services);
            var id = new ProcessDecisionRefreshAccounting(candidate.Intent.Request.CommandId).CommandId.Value;
            Assert.Equal(1, await context.IdempotencyKeys.Where(row => row.CommandId == id).ExecuteDeleteAsync());
        } else {
            request = invalid switch {
                InvalidSuccessor.OriginalAllocation => request with { DecisionBudget = ((ProcessDecisionBudgetSettlement)request.DecisionBudget!)
                    with { AllocationId = fixture.Parent.Child.AllocationId } },
                InvalidSuccessor.ResetAttempt => request with { Mutation = mutation with { State = mutation.State with {
                    Steps = [mutation.State.Steps.Single() with { AttemptNumber = 0 }]
                } } },
                InvalidSuccessor.RewriteHistory => request with { Mutation = mutation with { State = mutation.State with { AppliedResults = [] } } },
                InvalidSuccessor.DifferentActor => request with { Mutation = mutation with { Events = [mutation.Events[0] with {
                    Actor = new(ProcessEventActorKind.System, new("different-actor"))
                }, .. mutation.Events.Skip(1)] } },
                _ => throw new ArgumentOutOfRangeException(nameof(invalid))
            };
        }
        var before = await RetainedAsync(fixture.Services, fixture.State.RunId);
        var exception = await Record.ExceptionAsync(() => fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request));
        Assert.True(exception is InvalidOperationException or InvalidDataException, exception?.ToString() ?? "The invalid successor committed.");
        Assert.Equal(before, await RetainedAsync(fixture.Services, fixture.State.RunId));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Another_open_local_allocation_prevents_successor_even_with_current_expectations(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var candidate = await PrepareAsync(fixture, mode);
        var settlement = (ProcessDecisionBudgetSettlement)candidate.Request.DecisionBudget!;
        var reserved = await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(TimestampOnly(candidate.Request.OriginalState)
            with { DecisionBudget = new ProcessDecisionBudgetReservation(settlement.Expectation, new(Guid.NewGuid()), new(TimeSpan.TicksPerSecond, 0)) });
        Assert.True(reserved.CommittedNow);
        var request = candidate.Request with {
            OriginalState = reserved.State,
            Mutation = new ProcessDecisionRefreshSuccessor(candidate.Intent.Request.CommandId).Prepare(reserved.State,
                fixture.CommandContext, candidate.Intent, (ProcessDecisionRefresh)candidate.Request.Decision!),
            DecisionBudget = settlement with { Expectation = settlement.Expectation with { Revision = reserved.DecisionBudget!.Change.Balance.Revision } }
        };
        var before = await RetainedAsync(fixture.Services, fixture.State.RunId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request));
        Assert.Equal(before, await RetainedAsync(fixture.Services, fixture.State.RunId));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Root_quiescence_is_required_only_for_shared_material(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var candidate = await PrepareAsync(fixture, mode);
        var state = candidate.Request.OriginalState;
        DecisionBudgetBalance root;
        await using (var context = Context(fixture.Services)) {
            root = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RootRunId, null))!;
        }
        DecisionBudgetAllocationId unrelated = new(Guid.NewGuid());
        var reserved = await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(TimestampOnly(state) with {
            DecisionBudget = new ProcessDecisionBudgetReservation(new(null, root.BudgetId, root.Revision), unrelated, new(TimeSpan.TicksPerSecond, 0))
        });
        Assert.True(reserved.CommittedNow);
        var request = candidate.Request with {
            OriginalState = reserved.State,
            Mutation = new ProcessDecisionRefreshSuccessor(candidate.Intent.Request.CommandId).Prepare(reserved.State,
                fixture.CommandContext, candidate.Intent, (ProcessDecisionRefresh)candidate.Request.Decision!)
        };
        if (mode == ReadBasisMode.RunSnapshot) {
            var before = await RetainedAsync(fixture.Services, state.RunId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request));
            Assert.Equal(before, await RetainedAsync(fixture.Services, state.RunId));
        } else {
            var result = await fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>().CommitAsync(request);
            Assert.True(result.CommittedNow);
            Assert.Equal(ProcessRuntimeStepStatus.Ready, result.State.Steps.Single().Status);
            Assert.Equal(0, result.DecisionBudget!.Change.Balance.ReservedWorkTicks);
            Assert.Equal(TimeSpan.TicksPerSecond, result.DecisionBudget.Funding!.Receipt.Change.Balance.ReservedWorkTicks);
            await using var context = Context(fixture.Services);
            var retained = await new EfProcessDecisionBudgetStore(context).ReadAllocationAsync(root.BudgetId, unrelated);
            Assert.Null(retained!.Settlement);
            Assert.Equal(TimeSpan.TicksPerSecond, retained.Grant.WorkTicks);
        }
    }

    private sealed record Candidate(ProcessRuntimeCommitRequest Request, ProcessDecisionRefreshIntent Intent, ProcessDecisionMaterial Material);

    private static async Task<Candidate> PrepareAsync(ProjectTaskPlanReviewFixture fixture, ReadBasisMode mode) {
        var services = fixture.Services;
        var owner = services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var selected = await SelectedAsync(fixture);
        var review = fixture.Request;
        ProcessDecisionRefreshRequest refresh = new(new(Guid.NewGuid()), review.Child, review.StrategyId, review.WaitingResultKey,
            review.Decision, review.Budget, selected.SharedScope, ProcessDecisionRefreshKind.ManagerRefresh);
        Assert.True((await services.GetRequiredService<ProcessDecisionRefreshCoordinator>()
            .AdmitAsync(fixture.State, fixture.CommandContext, refresh)).CommittedNow);
        Assert.Equal(ProcessDecisionChildStopResult.ReadyForAccounting, await Assert.Single(services
            .GetServices<IProcessDecisionRefreshChildStopper>()).StopAsync(fixture.Parent, refresh.CommandId));
        var state = (await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        var accounted = Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(await services
            .GetRequiredService<ProcessDecisionRefreshAccountingCoordinator>().ReconcileAsync(state, fixture.CommandContext, refresh.CommandId)).Commit;
        var admission = (await services.GetRequiredService<IProcessDecisionRefreshReceiptReader>().ReadAsync(state.RunId, refresh.CommandId))!;
        var intent = ((ProcessDecisionBeginRefresh)admission.Decision.Command).Intent;
        state = accounted.State;
        ProcessDecisionMaterial material;
        ProcessDecisionRunScopeExpectation? inherited = null;
        if (mode == ReadBasisMode.RunSnapshot) {
            var root = (await services.GetRequiredService<IProcessDecisionRootQuery>().ReadAsync(state))!;
            ProcessDecisionRunScopeCapture capture = new(new(Guid.NewGuid()), new(Guid.NewGuid()), ProcessDecisionRunCaptureKind.ManagerRefresh);
            var reservedRoot = await owner.CommitAsync(TimestampOnly(state) with {
                DecisionRunScope = new(root.Scope.Expectation, ProcessDecisionRunScopeCommandKind.BeginCapture, capture),
                DecisionBudget = new ProcessDecisionBudgetReservation(new(null, root.Budget.BudgetId, root.Budget.Revision),
                    capture.AllocationId, new(TimeSpan.FromSeconds(30).Ticks, 0))
            });
            Assert.True(reservedRoot.CommittedNow);
            var started = fixture.Clock.GetTimestamp();
            material = await CaptureAsync(fixture, root.Source, root.Scope.Admission.MaterialScope, root.Scope.Admission.ContentContract,
                new(root.Scope.Admission.ScopeId, root.Scope.Material!.Stamp.Reference.Generation + 1, capture.MaterialId));
            var budget = reservedRoot.DecisionBudget!.Change.Balance;
            var completedRoot = await owner.CommitAsync(TimestampOnly(reservedRoot.State) with {
                DecisionRunScope = new(reservedRoot.DecisionRunScope!.State.Expectation, ProcessDecisionRunScopeCommandKind.CompleteCapture, Material: material.Stamp),
                DecisionBudget = new ProcessDecisionBudgetSettlement(new(null, budget.BudgetId, budget.Revision), capture.AllocationId,
                    new(1, new(fixture.Clock.GetElapsedTime(started).Ticks, 0))),
                DecisionMaterial = material
            });
            Assert.True(completedRoot.CommittedNow);
            state = completedRoot.State;
            inherited = completedRoot.DecisionRunScope!.State.Expectation;
            Assert.Equal(ProcessDecisionPhase.Reconciling, (await SelectedAsync(fixture)).Head!.Phase);
        } else {
            material = null!;
        }
        selected = await SelectedAsync(fixture);
        DecisionBudgetAllocationId allocation = new(Guid.NewGuid());
        var reserved = await owner.CommitAsync(TimestampOnly(state) with {
            DecisionBudget = new ProcessDecisionBudgetReservation(new(review.Child.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
                allocation, new(TimeSpan.FromSeconds(30).Ticks, 0))
        });
        Assert.True(reserved.CommittedNow);
        var startedLocal = fixture.Clock.GetTimestamp();
        if (mode == ReadBasisMode.StepSnapshot) {
            var input = await services.GetRequiredService<IProcessAcceptedExecutionInputReader>().ReadAsync(state.RunId, review.Child.StepInstanceId);
            material = await CaptureAsync(fixture, input.Source, fixture.Parent.Scope, input.Assignment.DecisionDataPolicy!.ContentContract!,
                new(fixture.Parent.Scope.Id, fixture.Parent.Material.Reference.Generation + 1, new(Guid.NewGuid())));
        } else {
            var retained = (await services.GetRequiredService<IProcessDecisionRootQuery>().ReadAsync(reserved.State))!;
            Assert.Equal(material.Stamp, retained.Scope.Material!.Stamp);
            Assert.Equal(inherited, retained.Scope.Expectation);
        }
        ProcessDecisionRefresh command = new(review.Child.StepInstanceId, selected.Head!.Expectation, material.Reference.MaterialId,
            mode == ReadBasisMode.RunSnapshot ? ProcessDecisionRefreshKind.ParentRefresh : ProcessDecisionRefreshKind.ManagerRefresh) {
            AdmissionCommandId = refresh.CommandId, Material = material.Stamp, InheritedScope = inherited
        };
        ProcessDecisionRefreshSuccessor successor = new(refresh.CommandId);
        var localBudget = reserved.DecisionBudget!.Change.Balance;
        return new(new(successor.CommandId, reserved.State, successor.Prepare(reserved.State, fixture.CommandContext, intent, command)) {
            Decision = command,
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(review.Child.StepInstanceId, localBudget.BudgetId, localBudget.Revision),
                allocation, new(1, new(fixture.Clock.GetElapsedTime(startedLocal).Ticks, 0))),
            DecisionMaterial = mode == ReadBasisMode.StepSnapshot ? material : null
        }, intent, material);
    }

    private static async Task<ProcessDecisionMaterial> CaptureAsync(ProjectTaskPlanReviewFixture fixture, ProcessLaunchAuthority source,
        ProcessDecisionMaterialScope scope, ProcessDecisionContentContract contract, ProcessDecisionMaterialReference reference)
        => Assert.IsType<ProcessDecisionMaterialCaptureResult.Captured>(await fixture.Services.GetServices<IProcessDecisionMaterialCapturer>()
            .Single(item => item.ContentContract == contract).CaptureAsync(new(source, scope, contract, reference))).Material;

    private static ProcessRuntimeCommitRequest TimestampOnly(ProcessRuntimeStateSnapshot state)
        => new(RuntimeCommandId.New(), state, new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = new(state.UpdatedAtUtc.UtcTicks / 10 * 10 + 10, TimeSpan.Zero) }, [], [], [], []));

    private static async Task<ProcessDecisionResultSelection> SelectedAsync(ProjectTaskPlanReviewFixture fixture)
        => (await fixture.Services.GetRequiredService<IProcessDecisionResultQuery>()
            .ReadAsync(fixture.State.RunId, fixture.Request.Child.StepInstanceId, new(fixture.Waiting.Id)))!;

    private static EfProcessRuntimeUnitOfWork Owner(ProjectTaskPlanReviewFixture fixture, ProcessPersistenceDbContext context)
        => new(context, fixture.Clock, runtimeDatabase: fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>());

    private static async Task<string> RetainedAsync(IServiceProvider services, ProcessRunId runId) {
        await using var context = Context(services);
        return JsonSerializer.Serialize(new {
            Runtime = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(row => row.BudgetId).ThenBy(row => row.AllocationId).ToArrayAsync(),
            Generations = await context.DecisionGenerations.AsNoTracking().OrderBy(row => row.Generation).ToArrayAsync(),
            Children = await context.DecisionChildren.AsNoTracking().OrderBy(row => row.ChildId).ToArrayAsync(),
            Scopes = await context.DecisionRunScopes.AsNoTracking().OrderBy(row => row.ScopeId).ToArrayAsync(),
            Events = await context.RuntimeEvents.CountAsync(), Outbox = await context.OutboxMessages.CountAsync(),
            Commands = await context.IdempotencyKeys.CountAsync(), Materials = await context.DecisionMaterials.CountAsync()
        });
    }

    private sealed class AfterFlush(Exception failure) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
    }

    private sealed class AfterCommit(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }
}
