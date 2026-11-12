using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshCaptureFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshCaptureCoordinatorTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_producer_captures_replays_and_enrolls_the_next_attempt(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var coordinator = fixture.Services.GetRequiredService<ProcessDecisionRefreshCaptureCoordinator>();
        var result = await coordinator.PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, result.Status);
        Assert.Equal(ProcessRuntimeStepStatus.Ready, result.State.Steps.Single().Status);
        Assert.Equal(state.Steps.Single().AttemptNumber, result.State.Steps.Single().AttemptNumber);
        Assert.Equal(JsonSerializer.Serialize(state.Claims), JsonSerializer.Serialize(result.State.Claims));
        Assert.Equal(JsonSerializer.Serialize(state.AppliedResults), JsonSerializer.Serialize(result.State.AppliedResults));
        var progress = await ProgressAsync(fixture.Services, state, admission);
        Assert.NotNull(progress.Successor);
        Assert.Equal(mode == ReadBasisMode.RunSnapshot, progress.RootCompletion is not null);
        Assert.Null(progress.LocalAbort);
        Assert.Null(progress.RootAbort);
        Assert.Equal(0, progress.Successor.Budget!.Change.Allocation.Settlement!.Consumed.Replans);
        Assert.Equal(0, progress.Successor.Budget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(0, progress.Successor.Budget.Funding!.Receipt.Change.Balance.ReservedWorkTicks);
        var retained = await RetainedAsync(fixture.Services, state);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(retained, await RetainedAsync(fixture.Services, state));
        var plan = (await fixture.Services.GetRequiredService<IProcessInstancePlanStore>().LoadAsync(state.PlanId))!;
        var work = Assert.Single(new ProcessRuntimeScheduler().CalculateReadyWork(result.State, plan, fixture.Clock.GetUtcNow()));
        Assert.Equal(state.Steps.Single().AttemptNumber + 1, work.AttemptNumber);
        DispatcherOwnerId owner = new("refresh-capture-fixture");
        DispatchClaimToken token = new(Guid.NewGuid());
        var claimed = await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
            .CreateClaimAsync(result.State, fixture.CommandContext, new(work, owner, token, fixture.Clock.GetUtcNow().AddMinutes(1)));
        var enrolled = await fixture.Services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
            .PrepareAsync(claimed.State, new(work.StepInstanceId, token, owner), TimeSpan.FromSeconds(30));
        Assert.True(enrolled.EnrolledNow);
        Assert.NotEqual(fixture.Parent.Child.Address.ChildId, enrolled.Child!.Child.Address.ChildId);
        Assert.Equal(new ProcessDecisionRefreshCaptureIdentity(admission).Material, enrolled.Child.Material.Reference.MaterialId);
        Assert.Equal(fixture.Parent.ExecutionContract, enrolled.Child.ExecutionContract);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    public enum CaptureFailure { Refused, Exception, Expired, CallerCancelled }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureFailure.Refused)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureFailure.Refused)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureFailure.Exception)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureFailure.Exception)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureFailure.Expired)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureFailure.Expired)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureFailure.CallerCancelled)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureFailure.CallerCancelled)]
    public async Task Known_failure_settles_measured_work_and_cannot_repeat_capture(ReadBasisMode mode, CaptureFailure failure) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var selected = await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadAsync(state, admission);
        using CancellationTokenSource cancellation = new();
        var expectedFailure = new IOException("Capture fixture failure.");
        CountingCapturer capturer = new(ActualCapturer(fixture.Services), (request, token) => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            if (failure == CaptureFailure.Exception) {
                throw expectedFailure;
            }
            if (failure == CaptureFailure.CallerCancelled) {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
            ProcessDecisionMaterialCaptureResult outcome = failure == CaptureFailure.Expired
                ? new ProcessDecisionMaterialCaptureResult.Captured(ProcessDecisionMaterial.FromContent(request.Scope, request.Material,
                    request.ContentContract, fixture.Clock.GetUtcNow() - selected.Input.Assignment.DecisionDataPolicy!.Policy.Budget.MaximumBasisAge - TimeSpan.FromSeconds(1), "{}"))
                : new ProcessDecisionMaterialCaptureResult.Refused(ProcessDecisionMaterialCaptureRefusal.SourceChanged);
            return Task.FromResult(outcome);
        });
        var coordinator = Coordinator(fixture, capturer);
        var running = coordinator.PrepareAsync(state, fixture.CommandContext, admission, cancellation.Token);
        if (failure == CaptureFailure.Exception) {
            Assert.Same(expectedFailure, await Assert.ThrowsAsync<IOException>(() => running));
        } else if (failure == CaptureFailure.CallerCancelled) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        } else {
            Assert.Equal(ProcessDecisionRefreshCaptureStatus.ReviewRequired, (await running).Status);
        }
        var progress = await ProgressAsync(fixture.Services, state, admission);
        var abort = mode == ReadBasisMode.RunSnapshot ? progress.RootAbort : progress.LocalAbort;
        Assert.NotNull(abort);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, abort.Budget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, abort.Budget.Change.Allocation.Settlement.Consumed.Replans);
        Assert.Equal(0, abort.Budget.Change.Balance.ReservedWorkTicks);
        Assert.Null(progress.Successor);
        Assert.Null(progress.RootCompletion);
        var current = await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadAsync(state, admission);
        Assert.Equal(ProcessDecisionPhase.Reconciling, current.Original.Head!.Phase);
        Assert.False(current.RootWorkPending);
        Assert.False(current.Original.OtherWorkPending);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.ReviewRequired, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(1, capturer.Calls);
        await using var context = Context(fixture.Services);
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Elapsed_grant_settles_the_actual_overrun_without_publishing_or_recapturing(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        CountingCapturer capturer = new(ActualCapturer(fixture.Services), (_, token) => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(11));
            Assert.True(token.IsCancellationRequested);
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("The capture deadline did not cancel.");
        });
        var coordinator = Coordinator(fixture, capturer);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.ReviewRequired, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        var progress = await ProgressAsync(fixture.Services, state, admission);
        var abort = mode == ReadBasisMode.RunSnapshot ? progress.RootAbort : progress.LocalAbort;
        Assert.NotNull(abort);
        Assert.Equal(TimeSpan.FromSeconds(10).Ticks, abort.Budget!.Change.Allocation.Grant.WorkTicks);
        Assert.Equal(TimeSpan.FromSeconds(11).Ticks, abort.Budget.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.True(abort.Budget.Change.Balance.OverrunDetected);
        Assert.Equal(0, abort.Budget.Change.Balance.ReservedWorkTicks);
        Assert.Null(progress.RootCompletion);
        Assert.Null(progress.Successor);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.ReviewRequired, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(1, capturer.Calls);
        if (mode == ReadBasisMode.StepSnapshot) {
            Assert.True(abort.Budget.Funding!.Receipt.Change.Balance.OverrunDetected);
            Assert.Equal(TimeSpan.FromSeconds(11).Ticks, abort.Budget.Funding.Receipt.Change.Allocation.Settlement!.Consumed.WorkTicks);
        }
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Runtime_cancellation_after_content_read_drains_capture_without_advancing(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var actual = ActualCapturer(fixture.Services);
        CountingCapturer capturer = new(actual, async (request, token) => {
            var captured = await actual.CaptureAsync(request, token);
            var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId))!;
            Assert.True((await new ProcessRuntimeEngine(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>())
                .BeginRootCancellationAsync(current, fixture.CommandContext)).CommittedNow);
            return captured;
        });
        var result = await Coordinator(fixture, capturer).PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Stopped, result.Status);
        Assert.Equal(ProcessRuntimeStatus.CancelRequested, result.State.Status);
        var progress = await ProgressAsync(fixture.Services, state, admission);
        var abort = mode == ReadBasisMode.RunSnapshot ? progress.RootAbort : progress.LocalAbort;
        Assert.NotNull(abort);
        Assert.True(abort.Budget!.Change.Allocation.Settlement!.Consumed.WorkTicks > 0);
        Assert.Equal(0, abort.Budget.Change.Balance.ReservedWorkTicks);
        Assert.Null(progress.Successor);
        Assert.Equal(1, capturer.Calls);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Lost_reservation_acknowledgement_reclaims_never_invoked_journal_and_captures_once(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        CountingCapturer capturer = new(ActualCapturer(fixture.Services));
        InterceptingCommit owner = new(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), (request, _) => {
            if (request.CommandId == (mode == ReadBasisMode.RunSnapshot ? identity.RootReserve : identity.LocalReserve)) {
                throw new IOException("Reservation acknowledgement lost.");
            }
            return Task.CompletedTask;
        });
        var coordinator = Coordinator(fixture, capturer, owner);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        var before = await RetainedAsync(fixture.Services, state);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await coordinator.PrepareAsync(state, fixture.CommandContext, admission)).Status);
        Assert.Equal(before, await RetainedAsync(fixture.Services, state));
        Assert.Equal(1, capturer.Calls);
        var progress = await ProgressAsync(fixture.Services, state, admission);
        var reservation = mode == ReadBasisMode.RunSnapshot ? progress.RootReservation : progress.LocalReservation;
        Assert.NotNull(reservation);
        Assert.Null(reservation.Budget!.Change.Allocation.Settlement);
        Assert.True(reservation.Budget.Change.Balance.ReservedWorkTicks > 0);
        Assert.NotNull(progress.Successor);
        Assert.Equal(0, progress.Successor.Budget!.Change.Balance.ReservedWorkTicks);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Lost_settlement_acknowledgement_reuses_captured_material_and_exact_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        CountingCapturer capturer = new(ActualCapturer(fixture.Services));
        var lost = 0;
        InterceptingCommit owner = new(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), (request, _) => {
            if (request.CommandId == (mode == ReadBasisMode.RunSnapshot ? identity.RootComplete : identity.Successor) &&
                    Interlocked.Increment(ref lost) == 1) {
                throw new IOException("Settlement acknowledgement lost.");
            }
            return Task.CompletedTask;
        });
        var result = await Coordinator(fixture, capturer, owner).PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, result.Status);
        Assert.Equal(1, capturer.Calls);
        var progress = await ProgressAsync(fixture.Services, state, admission);
        Assert.NotNull(progress.Successor);
        Assert.Equal(0, progress.Successor.Budget!.Change.Balance.ReservedWorkTicks);
        await using var context = Context(fixture.Services);
        Assert.Equal(2, await context.DecisionMaterials.CountAsync());
        Assert.Equal(2, await context.DecisionGenerations.CountAsync());
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Competing_callers_invoke_capture_only_for_the_reservation_winner(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var actual = ActualCapturer(fixture.Services);
        CountingCapturer capturer = new(actual, async (request, token) => {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return await actual.CaptureAsync(request, token);
        });
        await using var firstScope = fixture.Services.CreateAsyncScope();
        await using var secondScope = fixture.Services.CreateAsyncScope();
        var first = Coordinator(fixture, capturer, services: firstScope.ServiceProvider).PrepareAsync(state, fixture.CommandContext, admission);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var second = await Coordinator(fixture, capturer, services: secondScope.ServiceProvider).PrepareAsync(state, fixture.CommandContext, admission);
            Assert.Equal(ProcessDecisionRefreshCaptureStatus.Pending, second.Status);
            Assert.Equal(1, capturer.Calls);
        } finally {
            release.TrySetResult();
        }
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await first.WaitAsync(TimeSpan.FromSeconds(30))).Status);
        Assert.Equal(1, capturer.Calls);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Work_opened_after_reservation_is_preserved_while_the_capture_settles(ReadBasisMode mode, bool local) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        var actualOwner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        CountingCapturer capturer = new(ActualCapturer(fixture.Services));
        InterceptingCommit owner = new(actualOwner, async (request, result) => {
            if (request.CommandId != identity.LocalReserve) {
                return;
            }
            var balance = local ? result.DecisionBudget!.Change.Balance : result.DecisionBudget!.Funding!.Receipt.Change.Balance;
            ProcessRuntimeCommitRequest extra = new(RuntimeCommandId.New(), result.State,
                ProcessRuntimeMutation.Applied(result.State with { UpdatedAtUtc = new(result.State.UpdatedAtUtc.UtcTicks + 10, TimeSpan.Zero) }, [], [])) {
                DecisionBudget = new ProcessDecisionBudgetReservation(new(local ? fixture.Request.Child.StepInstanceId : null,
                    balance.BudgetId, balance.Revision), new(Guid.NewGuid()), new(TimeSpan.TicksPerSecond, 0))
            };
            Assert.True((await actualOwner.CommitAsync(extra)).CommittedNow);
        });
        var result = await Coordinator(fixture, capturer, owner).PrepareAsync(state, fixture.CommandContext, admission);
        var canAdvance = !local && mode == ReadBasisMode.StepSnapshot;
        Assert.Equal(canAdvance ? ProcessDecisionRefreshCaptureStatus.Advanced : ProcessDecisionRefreshCaptureStatus.ReviewRequired, result.Status);
        var progress = await ProgressAsync(fixture.Services, state, admission);
        Assert.Equal(canAdvance, progress.Successor is not null);
        Assert.Equal(!canAdvance, progress.LocalAbort is not null);
        var settlement = (progress.Successor ?? progress.LocalAbort)!.Budget!;
        Assert.Equal(TimeSpan.TicksPerSecond, settlement.Funding!.Receipt.Change.Balance.ReservedWorkTicks);
        Assert.Equal(local ? TimeSpan.TicksPerSecond : 0, settlement.Change.Balance.ReservedWorkTicks);
        Assert.True(settlement.Change.Allocation.Settlement!.Consumed.WorkTicks > 0);
        Assert.Equal(1, capturer.Calls);
    }

    [Fact]
    public async Task Completed_root_capture_waits_for_other_work_and_then_inherits_without_recapture() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        var actualOwner = fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>();
        DecisionBudgetAllocationId extraAllocation = new(Guid.NewGuid());
        CountingCapturer capturer = new(ActualCapturer(fixture.Services));
        InterceptingCommit owner = new(actualOwner, async (request, result) => {
            if (request.CommandId != identity.RootComplete) {
                return;
            }
            var budget = result.DecisionBudget!.Change.Balance;
            ProcessRuntimeCommitRequest extra = new(RuntimeCommandId.New(), result.State,
                ProcessRuntimeMutation.Applied(result.State with { UpdatedAtUtc = new(result.State.UpdatedAtUtc.UtcTicks + 10, TimeSpan.Zero) }, [], [])) {
                DecisionBudget = new ProcessDecisionBudgetReservation(new(null, budget.BudgetId, budget.Revision),
                    extraAllocation, new(TimeSpan.TicksPerSecond, 0))
            };
            Assert.True((await actualOwner.CommitAsync(extra)).CommittedNow);
        });
        var coordinator = Coordinator(fixture, capturer, owner);
        var pending = await coordinator.PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Pending, pending.Status);
        var captured = await ProgressAsync(fixture.Services, state, admission);
        Assert.NotNull(captured.RootCompletion);
        Assert.Null(captured.LocalReservation);
        Assert.Equal(1, capturer.Calls);
        Assert.Equal(0, captured.RootCompletion.Budget!.Change.Balance.ReservedWorkTicks);
        var balance = (await fixture.Services.GetRequiredService<IProcessDecisionRootQuery>().ReadAsync(pending.State))!.Budget;
        Assert.Equal(TimeSpan.TicksPerSecond, balance.ReservedWorkTicks);
        ProcessRuntimeCommitRequest settlement = new(RuntimeCommandId.New(), pending.State,
            ProcessRuntimeMutation.Applied(pending.State with { UpdatedAtUtc = new(pending.State.UpdatedAtUtc.UtcTicks + 10, TimeSpan.Zero) }, [], [])) {
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(null, balance.BudgetId, balance.Revision), extraAllocation,
                new(1, new(TimeSpan.TicksPerSecond, 0)))
        };
        Assert.True((await actualOwner.CommitAsync(settlement)).CommittedNow);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, (await coordinator.PrepareAsync(pending.State, fixture.CommandContext, admission)).Status);
        Assert.Equal(1, capturer.Calls);
        var completed = await ProgressAsync(fixture.Services, state, admission);
        Assert.Equal(captured.RootCompletion, completed.RootCompletion);
        Assert.NotNull(completed.Successor);
        Assert.Equal(0, completed.Successor.Budget!.Funding!.Receipt.Change.Balance.ReservedWorkTicks);
    }

    [Fact]
    public async Task Restart_after_root_completion_inherits_the_durable_material_without_recapture() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-refresh-capture-producer");
        var profile = environment.CreatePostgreSqlProfile("refresh-capture-producer");
        RuntimeCommandId admission;
        RuntimeCommandContext originalContext;
        ProcessRuntimeStateSnapshot state;
        ProcessDecisionRefreshCaptureProgress before;
        await using (var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(ReadBasisMode.RunSnapshot, environment, profile, freezeClock: false)) {
            admission = await AdmitAsync(fixture);
            state = await AccountAsync(fixture, admission);
            originalContext = fixture.CommandContext;
            ProcessDecisionRefreshCaptureIdentity identity = new(admission);
            using CancellationTokenSource interruption = new();
            CountingCapturer capturer = new(ActualCapturer(fixture.Services));
            InterceptingCommit owner = new(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), (request, _) => {
                if (request.CommandId == identity.RootComplete) {
                    interruption.Cancel();
                }
                return Task.CompletedTask;
            });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Coordinator(fixture, capturer, owner)
                .PrepareAsync(state, originalContext, admission, interruption.Token));
            before = await ProgressAsync(fixture.Services, state, admission);
            Assert.NotNull(before.RootCompletion);
            Assert.Null(before.LocalReservation);
            Assert.Equal(1, capturer.Calls);
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
            }
        });
        await using var scope = restarted.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        CountingCapturer forbidden = new(ActualCapturer(services), (_, _) => throw new InvalidOperationException("Root material was captured again."));
        ProcessDecisionRefreshCaptureCoordinator coordinator = new(services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>(),
            services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(),
            services.GetRequiredService<IProcessDecisionRootQuery>(), services.GetRequiredService<IProcessDecisionResultQuery>(), [forbidden],
            services.GetRequiredService<IProcessRuntimeStateStore>(), services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            services.GetRequiredService<TimeProvider>(), NullLogger<ProcessDecisionRefreshCaptureCoordinator>.Instance);
        var result = await coordinator.PrepareAsync(state, originalContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, result.Status);
        Assert.Equal(0, forbidden.Calls);
        var after = await ProgressAsync(services, state, admission);
        Assert.Equal(before.RootCompletion, after.RootCompletion);
        Assert.NotNull(after.Successor);
        Assert.Equal(before.RootCompletion.Scope!.State.Material!.Stamp, after.Successor.Decision!.Command.Material);
    }

    private static IProcessDecisionMaterialCapturer ActualCapturer(IServiceProvider services)
        => services.GetServices<IProcessDecisionMaterialCapturer>().Single(item => item.ContentContract == ProjectTaskPlanProcessContract.Content);

    private static ProcessDecisionRefreshCaptureCoordinator Coordinator(ProjectTaskPlanReviewFixture fixture,
        IProcessDecisionMaterialCapturer capturer, IProcessRuntimeUnitOfWork? owner = null, IServiceProvider? services = null) {
        services ??= fixture.Services;
        return new(services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>(), services.GetRequiredService<IProcessDecisionRefreshCaptureStore>(),
            services.GetRequiredService<IProcessDecisionRootQuery>(),
            services.GetRequiredService<IProcessDecisionResultQuery>(), [capturer], services.GetRequiredService<IProcessRuntimeStateStore>(),
            owner ?? services.GetRequiredService<IProcessRuntimeUnitOfWork>(), fixture.Clock, NullLogger<ProcessDecisionRefreshCaptureCoordinator>.Instance);
    }

    private static Task<ProcessDecisionRefreshCaptureProgress> ProgressAsync(IServiceProvider services, ProcessRuntimeStateSnapshot state, RuntimeCommandId admission)
        => services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadProgressAsync(state.RunId, admission);

    private static async Task<string> RetainedAsync(IServiceProvider services, ProcessRuntimeStateSnapshot state) {
        await using var context = Context(services);
        return JsonSerializer.Serialize(new {
            State = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId),
            Budgets = await context.DecisionBudgets.AsNoTracking().OrderBy(row => row.BudgetId).ToArrayAsync(),
            Allocations = await context.DecisionBudgetAllocations.AsNoTracking().OrderBy(row => row.BudgetId).ThenBy(row => row.AllocationId).ToArrayAsync(),
            Materials = await context.DecisionMaterials.CountAsync(), Generations = await context.DecisionGenerations.CountAsync(),
            Commands = await context.IdempotencyKeys.CountAsync(), Events = await context.RuntimeEvents.CountAsync(), Outbox = await context.OutboxMessages.CountAsync()
        });
    }

    private sealed class CountingCapturer(IProcessDecisionMaterialCapturer original,
        Func<ProcessDecisionMaterialCaptureRequest, CancellationToken, Task<ProcessDecisionMaterialCaptureResult>>? capture = null) : IProcessDecisionMaterialCapturer {
        private int calls;
        internal int Calls => calls;
        public ProcessDecisionContentContract ContentContract => original.ContentContract;
        public Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref calls);
            return capture is null ? original.CaptureAsync(request, cancellationToken) : capture(request, cancellationToken);
        }
    }

    private sealed class InterceptingCommit(IProcessRuntimeUnitOfWork owner, Func<ProcessRuntimeCommitRequest, ProcessRuntimeCommitResult, Task> after)
        : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            var result = await owner.CommitAsync(request, cancellationToken);
            await after(request, result);
            return result;
        }
    }
}
