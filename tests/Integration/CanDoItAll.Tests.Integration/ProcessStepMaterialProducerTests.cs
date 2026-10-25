using System.Text;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessStepMaterialProducerTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_producer_uses_real_material_and_enrolls_one_original_child(ReadBasisMode mode) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync(mode);
        var coordinator = fixture.Services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>();
        var result = await coordinator.PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20));
        Assert.Equal(ProcessDecisionStepPreparationStatus.Ready, result.Status);
        Assert.True(result.EnrolledNow);
        var child = Assert.IsType<ProcessDecisionChildMaterialBinding>(result.Child);
        Assert.Equal(mode, child.Scope.ReadBasisMode);
        Assert.Equal(TimeSpan.FromSeconds(20).Ticks, child.Child.Grant.WorkTicks);
        await using var context = Context(fixture.Services);
        var material = Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        Assert.Equal(material.MaterialId, child.Child.MaterialId.Value);
        var basis = ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(material.PayloadJson));
        Assert.Equal(fixture.Root.Sql.Project, basis.Project.Expectation.Project);
        Assert.Single(await context.DecisionHeads.ToArrayAsync());
        Assert.Single(await context.DecisionChildren.ToArrayAsync());
        Assert.Equal(1, await context.DecisionBudgetAllocations.CountAsync(item =>
            item.BudgetId == child.Child.BudgetId.Value && item.OwnerWorkRevision != null));
        var replay = await coordinator.PrepareAsync(result.State, fixture.Claim, TimeSpan.FromSeconds(20));
        Assert.Equal(child, replay.Child);
        Assert.False(replay.EnrolledNow);
        Assert.Single(await context.DecisionChildren.ToArrayAsync());
        Assert.Equal(fixture.Claim.ClaimToken, replay.State.Steps[0].ActiveClaimToken);
    }

    [Fact]
    public async Task Competing_capture_preserves_winner_claim_and_invokes_one_capturer_outside_transaction() {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var first = Context(fixture.Services);
        await using var second = Context(fixture.Services);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new CaptureProbe(fixture.Root.Capturer, async token => {
            Assert.Null(first.Database.CurrentTransaction);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        var winner = fixture.Coordinator(first, capture).PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try {
            var state = await fixture.Owner(second).LoadAsync(fixture.State.RunId);
            var pending = await fixture.Coordinator(second, capture).PrepareAsync(state!, fixture.Claim, TimeSpan.FromSeconds(20));
            Assert.Equal(ProcessDecisionStepPreparationStatus.Pending, pending.Status);
            Assert.Null(pending.Child);
            Assert.Equal(fixture.Claim.ClaimToken, pending.State.Steps[0].ActiveClaimToken);
            Assert.Empty(await second.DecisionChildren.ToArrayAsync());
        } finally {
            release.TrySetResult();
        }
        Assert.True((await winner).EnrolledNow);
        Assert.Equal(1, capture.Calls);
    }

    [Theory]
    [InlineData(LostPhase.Reserve)]
    [InlineData(LostPhase.Attach)]
    [InlineData(LostPhase.Enroll)]
    public async Task Lost_acknowledgement_never_repeats_capture_or_fresh_enrollment(LostPhase phase) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var capture = new CaptureProbe(fixture.Root.Capturer);
        var commit = new LostAcknowledgement(fixture.Owner(context), phase);
        var coordinator = fixture.Coordinator(context, capture, commit);
        if (phase == LostPhase.Attach) {
            Assert.True((await coordinator.PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20))).EnrolledNow);
        } else {
            await Assert.ThrowsAsync<IOException>(() => coordinator.PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20)));
        }
        var state = await fixture.Owner(context).LoadAsync(fixture.State.RunId);
        var recovered = await coordinator.PrepareAsync(state!, fixture.Claim, TimeSpan.FromSeconds(20));
        Assert.Equal(phase == LostPhase.Reserve ? ProcessDecisionStepPreparationStatus.Pending : ProcessDecisionStepPreparationStatus.Ready,
            recovered.Status);
        Assert.False(recovered.EnrolledNow);
        Assert.Equal(phase == LostPhase.Reserve ? 0 : 1, capture.Calls);
        Assert.Equal(phase == LostPhase.Reserve ? 0 : 1, await context.DecisionChildren.CountAsync());
    }

    [Theory]
    [InlineData(ClosedCapture.ClaimExpired)]
    [InlineData(ClosedCapture.MaterialExpired)]
    [InlineData(ClosedCapture.RootCancelled)]
    [InlineData(ClosedCapture.Overrun)]
    [InlineData(ClosedCapture.FailedRead)]
    [InlineData(ClosedCapture.CallerCancelled)]
    public async Task Closed_capture_is_accounted_without_attaching_invalid_material(ClosedCapture ending) {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync(claimDuration: TimeSpan.FromMinutes(5));
        await using var context = Context(fixture.Services);
        var clock = new WorkClock();
        using var caller = new CancellationTokenSource();
        var capture = new CaptureProbe(fixture.Root.Capturer, after: async token => {
            clock.Elapsed = ending == ClosedCapture.Overrun ? TimeSpan.FromSeconds(11) : TimeSpan.FromSeconds(1);
            switch (ending) {
                case ClosedCapture.ClaimExpired:
                    clock.Offset = TimeSpan.FromMinutes(6);
                    break;
                case ClosedCapture.MaterialExpired:
                    clock.Offset = TimeSpan.FromMinutes(11);
                    break;
                case ClosedCapture.RootCancelled:
                    await using (var other = Context(fixture.Services)) {
                        var owner = fixture.Owner(other);
                        var original = await owner.LoadAsync(fixture.State.RunId);
                        await ProcessRootCancellationFixture.StopAsync(owner, original!);
                    }
                    break;
                case ClosedCapture.FailedRead:
                    throw new IOException("The owned local read closed with an error.");
                case ClosedCapture.CallerCancelled:
                    caller.Cancel();
                    break;
            }
        });
        var operation = fixture.Coordinator(context, capture, clock: clock)
            .PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20), caller.Token);
        if (ending == ClosedCapture.MaterialExpired) {
            await Assert.ThrowsAsync<ProcessDecisionMaterialExpiredException>(() => operation);
        } else if (ending == ClosedCapture.FailedRead) {
            await Assert.ThrowsAsync<IOException>(() => operation);
        } else if (ending == ClosedCapture.CallerCancelled) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        } else {
            Assert.Equal(ProcessDecisionStepPreparationStatus.ReviewRequired, (await operation).Status);
        }
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Empty(await context.DecisionChildren.ToArrayAsync());
        var budget = await new EfProcessDecisionBudgetStore(context).ReadAsync(fixture.State.RunId, fixture.Claim.StepInstanceId);
        Assert.Equal(clock.Elapsed.Ticks, budget!.ConsumedWorkTicks);
        Assert.Equal(0, budget.ReservedWorkTicks);
        Assert.Equal(ending == ClosedCapture.Overrun, budget.OverrunDetected);
        Assert.Equal(1, capture.Calls);
    }

    [Fact]
    public async Task Assignment_repair_between_selection_and_attachment_is_fenced_in_the_transaction() {
        await using var fixture = await ProcessStepMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var capture = new CaptureProbe(fixture.Root.Capturer);
        var commit = new RepairBeforeAttachment(fixture, fixture.Owner(context));
        var result = await fixture.Coordinator(context, capture, commit).PrepareAsync(fixture.State, fixture.Claim, TimeSpan.FromSeconds(20));
        Assert.Equal(ProcessDecisionStepPreparationStatus.ReviewRequired, result.Status);
        Assert.True(commit.Repaired);
        Assert.Equal(1, capture.Calls);
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Equal(0, (await new EfProcessDecisionBudgetStore(context).ReadAsync(fixture.State.RunId, fixture.Claim.StepInstanceId))!.ReservedWorkTicks);
    }

    public enum LostPhase { Reserve, Attach, Enroll }
    public enum ClosedCapture { ClaimExpired, MaterialExpired, RootCancelled, Overrun, FailedRead, CallerCancelled }

    private sealed class CaptureProbe(IProcessDecisionMaterialCapturer inner, Func<CancellationToken, Task>? before = null,
        Func<CancellationToken, Task>? after = null) : IProcessDecisionMaterialCapturer {
        internal int Calls { get; private set; }
        public ProcessDecisionContentContract ContentContract => inner.ContentContract;
        public async Task<ProcessDecisionMaterialCaptureResult> CaptureAsync(ProcessDecisionMaterialCaptureRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            if (before is not null) {
                await before(cancellationToken);
            }
            var result = await inner.CaptureAsync(request, cancellationToken);
            if (after is not null) {
                await after(cancellationToken);
            }
            return result;
        }
    }

    private sealed class LostAcknowledgement(IProcessRuntimeUnitOfWork inner, LostPhase phase) : IProcessRuntimeUnitOfWork {
        private bool thrown;
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            var result = await inner.CommitAsync(request, cancellationToken);
            if (!thrown && (phase == LostPhase.Reserve && request.Decision is null && request.DecisionBudget is ProcessDecisionBudgetReservation ||
                    phase == LostPhase.Attach && request.Decision is ProcessDecisionStart ||
                    phase == LostPhase.Enroll && request.Decision is ProcessDecisionEnrollment)) {
                thrown = true;
                throw new IOException("The original commit acknowledgement was lost.");
            }
            return result;
        }
    }

    private sealed class RepairBeforeAttachment(ProcessStepMaterialFixture fixture, IProcessRuntimeUnitOfWork inner) : IProcessRuntimeUnitOfWork {
        internal bool Repaired { get; private set; }
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            if (!Repaired && request.Decision is ProcessDecisionStart) {
                await using var other = Context(fixture.Services);
                await other.RuntimeStepAssignments.Where(item => item.RunId == fixture.State.RunId.Value)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadinessHash, "sha256:repaired"), cancellationToken);
                Repaired = true;
            }
            return await inner.CommitAsync(request, cancellationToken);
        }
    }

    private sealed class WorkClock : TimeProvider {
        internal TimeSpan Elapsed { get; set; }
        internal TimeSpan Offset { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + Offset;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Elapsed.Ticks;
    }
}
