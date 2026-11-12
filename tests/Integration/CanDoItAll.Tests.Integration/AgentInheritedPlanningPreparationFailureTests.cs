using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningPreparationFailureTests {
    [Theory]
    [InlineData(FaultStage.Preparation, 1)]
    [InlineData(FaultStage.Binding, 3)]
    [InlineData(FaultStage.Parent, 7)]
    public async Task Known_local_failure_retains_exact_original_identity_and_measured_work(FaultStage stage, int seconds) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var primary = new IOException("local-preparation-failed");
        var preparation = Preparation(fixture, request.ExecutionRunId);
        var beginning = fixture.Clock.Now;
        var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() =>
            Owner(fixture).AdmitWorkspaceAsync(request, (_, _) => {
                fixture.Clock.Advance(TimeSpan.FromSeconds(4));
                throw primary;
            }, _ => {
                fixture.Clock.Advance(TimeSpan.FromSeconds(1));
                if (stage == FaultStage.Preparation) {
                    throw primary;
                }
                return Task.FromResult(preparation with { BindAsync = async (context, token) => {
                    fixture.Clock.Advance(TimeSpan.FromSeconds(2));
                    if (stage == FaultStage.Binding) {
                        throw primary;
                    }
                    return await preparation.BindAsync(context, token);
                } });
            }, default));
        Assert.Same(primary, failure.InnerException);
        var evidence = failure.Failure;
        Assert.Equal(request.ExecutionRunId, evidence.ExecutionRunId);
        Assert.Equal(request.AgentId, evidence.AgentId);
        Assert.Equal(request.Owner, evidence.Owner);
        Assert.Equal(request.ContextScope, evidence.Scope);
        Assert.Equal(request.ParentBinding, evidence.ParentBinding);
        Assert.Equal(request.Start.Allocation, evidence.Allocation);
        Assert.Equal(fixture.Files.Profile, evidence.Profile);
        Assert.Equal(beginning, evidence.StartedAtUtc);
        Assert.Equal(beginning.AddSeconds(seconds), evidence.ClosedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(seconds).Ticks, evidence.WorkTicks);
        Assert.Equal(RunOutcome.Failed, evidence.Outcome);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(request.ExecutionRunId));
        await using var released = await fixture.Files.NewStore().AcquireToolDispatchLeaseAsync(request.ExecutionRunId);
    }

    [Theory]
    [InlineData(false, 31)]
    [InlineData(true, 2)]
    public async Task Cancellation_and_overrun_keep_actual_closed_work_without_clamping(bool cancel, int seconds) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        using var cancellation = new CancellationTokenSource();
        var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() =>
            Owner(fixture).AdmitWorkspaceAsync(request, request.RequireOriginalParent, token => {
                fixture.Clock.Advance(TimeSpan.FromSeconds(seconds));
                if (cancel) {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }
                throw new IOException("closed-overrun");
            }, cancellation.Token));
        Assert.Equal(TimeSpan.FromSeconds(seconds).Ticks, failure.Failure.WorkTicks);
        Assert.Equal(cancel ? RunOutcome.Cancelled : RunOutcome.Failed, failure.Failure.Outcome);
        if (!cancel) {
            Assert.True(failure.Failure.WorkTicks > request.Start.Allocation.Grant.WorkTicks);
        }
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(request.ExecutionRunId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_clock_cannot_issue_closed_evidence_and_releases_the_lease(bool wallClock) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var primary = new IOException("invalid-clock");
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() =>
            Owner(fixture).AdmitWorkspaceAsync(request, request.RequireOriginalParent, _ => {
                if (wallClock) {
                    fixture.Clock.Now -= TimeSpan.FromSeconds(1);
                } else {
                    fixture.Clock.Timestamp = -1;
                }
                throw primary;
            }, default)));
        await using var released = await fixture.Files.NewStore().AcquireToolDispatchLeaseAsync(request.ExecutionRunId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_failure_is_preserved_and_cleanup_time_is_not_preparation_work(bool failRelease) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var primary = new IOException("closed-before-release");
        var cleanup = new IOException("release-failed");
        var store = fixture.Files.NewStore();
        var leases = new ReleaseStore(store, () => fixture.Clock.Advance(TimeSpan.FromSeconds(10)), failRelease ? cleanup : null);
        var owner = new AgentInheritedPlanningRunAdmission(store, leases, fixture.NewJournal(store));
        var attempted = owner.AdmitWorkspaceAsync(request, request.RequireOriginalParent, _ => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            throw primary;
        }, default);
        if (failRelease) {
            var aggregate = await Assert.ThrowsAsync<AggregateException>(() => attempted);
            Assert.Equal(new Exception[] { primary, cleanup }, aggregate.InnerExceptions);
        } else {
            var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() => attempted);
            Assert.Equal(TimeSpan.FromSeconds(2).Ticks, failure.Failure.WorkTicks);
            Assert.Equal(fixture.Clock.Now - TimeSpan.FromSeconds(10), failure.Failure.ClosedAtUtc);
        }
        await using var released = await store.AcquireToolDispatchLeaseAsync(request.ExecutionRunId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_save_attempt_never_becomes_no_effect_evidence_even_when_acknowledgement_is_lost(bool saveBeforeThrow) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var primary = new IOException("save-unknown");
        var store = fixture.Files.NewStore();
        var fault = new SaveStore(store, primary, saveBeforeThrow);
        var owner = new AgentInheritedPlanningRunAdmission(fault, store, fixture.NewJournal(store));
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => owner.AdmitWorkspaceAsync(request,
            request.RequireOriginalParent, _ => Task.FromResult(Preparation(fixture, request.ExecutionRunId)), default)));
        Assert.Equal(1, fault.Attempts);
        var saved = await store.GetExecutionRunAsync(request.ExecutionRunId);
        Assert.Equal(saveBeforeThrow, saved is not null);
        var recovery = request with { Start = request.Start with { Mode = AgentInheritedPlanningStartMode.ExistingOnly } };
        if (saveBeforeThrow) {
            var retained = await Owner(fixture).AdmitWorkspaceAsync(recovery, request.RequireOriginalParent, NoPreparation, default);
            Assert.False(retained.Created);
            Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(retained.Run));
        } else {
            var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() =>
                Owner(fixture).AdmitWorkspaceAsync(recovery, request.RequireOriginalParent, NoPreparation, default));
            Assert.Equal(AgentInheritedPlanningRunAdmission.MissingOriginalCode, denied.Code);
        }
    }

    [Fact]
    public async Task Existing_only_waiter_cannot_repeat_closed_fresh_preparation() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var preparations = 0;
        var fresh = Owner(fixture).AdmitWorkspaceAsync(request, request.RequireOriginalParent, async _ => {
            Interlocked.Increment(ref preparations);
            entered.SetResult();
            await release.Task;
            throw new IOException("fresh-closed");
        }, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var recovery = Owner(fixture).AdmitWorkspaceAsync(request with {
            Start = request.Start with { Mode = AgentInheritedPlanningStartMode.ExistingOnly }
        }, request.RequireOriginalParent, NoPreparation, default);
        release.SetResult();
        await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() => fresh);
        var refusal = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => recovery);
        Assert.Equal(AgentInheritedPlanningRunAdmission.MissingOriginalCode, refusal.Code);
        Assert.Equal(1, preparations);
    }

    [Fact]
    public async Task Public_delegate_admission_cannot_mint_owner_evidence() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var request = Request(fixture);
        var primary = new IOException("untrusted-delegate");
        Assert.Same(primary, await Assert.ThrowsAsync<IOException>(() => Owner(fixture).AdmitAsync(request.ExecutionRunId,
            request.Start, request.RequireOriginalParent, _ => throw primary)));
        Assert.Empty(typeof(AgentInheritedPlanningPreparationFailure).GetConstructors());
    }

    [Fact]
    public async Task Registered_workspace_preserves_closed_failure_without_provider_work() {
        await using var fixture = await AgentInheritedPlanningWorkspaceFixture.CreateAsync();
        var request = fixture.Request with { OriginalInput = string.Empty };
        var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() => fixture.ExecuteAsync(request));
        Assert.IsType<ArgumentException>(failure.InnerException);
        Assert.Equal(request.ExecutionRunId, failure.Failure.ExecutionRunId);
        Assert.Equal(request.ParentBinding, failure.Failure.ParentBinding);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Equal(0, fixture.Bindings);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(request.ExecutionRunId));
    }

    internal static AgentInheritedPlanningWorkspaceRequest Request(AgentInheritedPlanningFixture fixture) {
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        return new(Guid.NewGuid(), new(decision.InheritedOrigin!.Allocation, AgentInheritedPlanningStartMode.FreshEnrollment),
            fixture.Original.AgentId, "Prepare the original task plan.", AgentExecutionOperationId.New(), ExecutionInvocationContext.Empty,
            decision.Owner, fixture.Files.StorageScope, decision.InheritedOrigin.ParentBinding, (_, _) => Task.CompletedTask,
            _ => AgentPlanningDecisionTestData.Envelope());
    }

    internal static AgentInheritedPlanningRunPreparation Preparation(AgentInheritedPlanningFixture fixture, Guid id) {
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        return new(fixture.Original with { Id = id, MetadataJson = "{}", ToolAdmission = null }, "Prepare the original bounded plan.",
            decision.Owner, AgentPlanningDecisionTestData.Envelope().Digest, fixture.Files.StorageScope, decision.InheritedOrigin!.ParentBinding,
            (binding, _) => Task.FromResult(new AgentInheritedPlanningBinding(decision.OriginalTarget,
                decision.InheritedOrigin! with { Source = binding.Session.Reference.BackgroundSource! })));
    }

    private static AgentInheritedPlanningRunAdmission Owner(AgentInheritedPlanningFixture fixture) {
        var store = fixture.Files.NewStore();
        return new(store, store, fixture.NewJournal(store));
    }

    private static Task<AgentInheritedPlanningRunPreparation> NoPreparation(CancellationToken _)
        => throw new InvalidOperationException("Existing-only admission cannot prepare again.");

    public enum FaultStage { Preparation, Binding, Parent }

    private sealed class ReleaseStore(ISandboxWorkspaceExecutionRunLeaseStore store, Action release, Exception? failure)
        : ISandboxWorkspaceExecutionRunLeaseStore {
        public async ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(Guid id, CancellationToken cancellationToken)
            => new Handle(await store.AcquireToolDispatchLeaseAsync(id, cancellationToken), release, failure);

        private sealed class Handle(IAsyncDisposable lease, Action release, Exception? failure) : IAsyncDisposable {
            public async ValueTask DisposeAsync() {
                await lease.DisposeAsync();
                release();
                if (failure is not null) {
                    throw failure;
                }
            }
        }
    }

    private sealed class SaveStore(ISandboxWorkspaceExecutionRunStore store, Exception failure, bool saveBeforeThrow)
        : ISandboxWorkspaceExecutionRunStore {
        internal int Attempts { get; private set; }
        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Startup must read the selected child.");
        public Task<ExecutionRunRecord?> GetExecutionRunAsync(Guid id, CancellationToken cancellationToken = default)
            => store.GetExecutionRunAsync(id, cancellationToken);
        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(Guid id, CancellationToken cancellationToken = default)
            => store.GetExecutionRunDetailAsync(id, cancellationToken);
        public async Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(ExecutionRunDetail detail, CancellationToken cancellationToken = default) {
            Attempts++;
            if (saveBeforeThrow) {
                await store.SaveExecutionRunDetailAsync(detail, cancellationToken);
            }
            throw failure;
        }
    }
}
