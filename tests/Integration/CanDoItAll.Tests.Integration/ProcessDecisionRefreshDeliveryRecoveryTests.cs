using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshCaptureFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshDeliveryFixture;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshJournalFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshDeliveryRecoveryTests {
    public enum LostAcknowledgement { Stop, Accounting, Return, Successor }
    public enum CaptureInterruption { BeforeBegin, AfterBegin, AfterReturn }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, LostAcknowledgement.Stop)]
    [InlineData(ReadBasisMode.StepSnapshot, LostAcknowledgement.Stop)]
    [InlineData(ReadBasisMode.RunSnapshot, LostAcknowledgement.Accounting)]
    [InlineData(ReadBasisMode.StepSnapshot, LostAcknowledgement.Accounting)]
    [InlineData(ReadBasisMode.RunSnapshot, LostAcknowledgement.Return)]
    [InlineData(ReadBasisMode.StepSnapshot, LostAcknowledgement.Return)]
    [InlineData(ReadBasisMode.RunSnapshot, LostAcknowledgement.Successor)]
    [InlineData(ReadBasisMode.StepSnapshot, LostAcknowledgement.Successor)]
    public async Task Original_receipts_resume_each_lost_acknowledgement_without_repeating_capture(ReadBasisMode mode, LostAcknowledgement point) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var actual = Assert.Single(fixture.Services.GetServices<IProcessDecisionRefreshChildStopper>());
        StopProbe stop = new(actual, async (binding, action, token) => {
            var result = await actual.StopAsync(binding, action, token);
            if (point == LostAcknowledgement.Stop) {
                throw new IOException("The original stop acknowledgement was lost.");
            }
            return result;
        });
        CaptureProbe capture = new(fixture.Services);
        ProcessDecisionRefreshCaptureIdentity identity = new(admission);
        var owner = new AfterCommit(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), request =>
            point == LostAcknowledgement.Accounting && request.RefreshAccounting is not null ||
            point == LostAcknowledgement.Successor && request.CommandId == identity.Successor);
        var journal = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        var first = Delivery(fixture.Services, capture, point == LostAcknowledgement.Return
            ? new InterruptedStore(journal, Interruption.AfterReturn) : journal, owner, [stop]);
        if (point == LostAcknowledgement.Successor) {
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(await first.AdvanceAsync(fixture.State.RunId, admission));
        } else {
            await Assert.ThrowsAsync<IOException>(() => first.AdvanceAsync(fixture.State.RunId, admission));
        }
        var capturedBeforeRestart = capture.Calls;
        await using var restarted = fixture.Services.CreateAsyncScope();
        CaptureProbe next = new(restarted.ServiceProvider);
        var resumed = await Delivery(restarted.ServiceProvider, next).AdvanceAsync(fixture.State.RunId, admission);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(resumed);
        Assert.Equal(1, capturedBeforeRestart + next.Calls);
        await using var database = Context(fixture.Services);
        Assert.Equal(1, await database.IdempotencyKeys.CountAsync(row => row.CommandId == identity.Successor.Value));
        Assert.Equal(1, await database.IdempotencyKeys.CountAsync(row => row.CommandId == new ProcessDecisionRefreshAccounting(admission).CommandId.Value));
        Assert.All(await database.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
        var before = await SnapshotAsync(fixture.Services, fixture.State);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(await Delivery(restarted.ServiceProvider, next)
            .AdvanceAsync(fixture.State.RunId, admission));
        Assert.Equal(before, await SnapshotAsync(fixture.Services, fixture.State));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Cancellation_before_capture_finishes_through_its_original_owners(ReadBasisMode mode, bool accounted) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        if (accounted) {
            await AccountAsync(fixture, admission);
        }
        await CancelAsync(fixture.Services, fixture.State, fixture.CommandContext);
        CaptureProbe forbidden = new(fixture.Services, (_, _) => throw new InvalidOperationException("Cancelled refresh must not capture."));
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(await Delivery(fixture.Services, forbidden)
            .AdvanceAsync(fixture.State.RunId, admission));
        Assert.Null(await CancelUntilAsync(fixture.Services));
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, current.Status);
        await using var database = Context(fixture.Services);
        Assert.All(await database.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
        Assert.Empty(await database.RefreshCaptures.ToArrayAsync());
        var before = await SnapshotAsync(fixture.Services, current);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(await Delivery(fixture.Services, forbidden)
            .AdvanceAsync(current.RunId, admission));
        Assert.Equal(before, await SnapshotAsync(fixture.Services, current));
        Assert.Equal(0, forbidden.Calls);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureInterruption.BeforeBegin, false)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureInterruption.BeforeBegin, false)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureInterruption.AfterBegin, false)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureInterruption.AfterBegin, false)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureInterruption.AfterReturn, false)]
    [InlineData(ReadBasisMode.StepSnapshot, CaptureInterruption.AfterReturn, false)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureInterruption.BeforeBegin, true)]
    [InlineData(ReadBasisMode.RunSnapshot, CaptureInterruption.AfterReturn, true)]
    public async Task Cancellation_drains_known_capture_but_retains_unknown_work(ReadBasisMode mode, CaptureInterruption point, bool finalizeDecisionFirst) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        CaptureProbe capture = new(fixture.Services);
        var journal = fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureStore>();
        await Assert.ThrowsAsync<IOException>(() => Delivery(fixture.Services, capture, new InterruptedStore(journal, (Interruption)point))
            .AdvanceAsync(fixture.State.RunId, admission));
        var saved = await SavedAsync(fixture.Services, fixture.State, admission, mode);
        await CancelAsync(fixture.Services, fixture.State, fixture.CommandContext);
        ProcessCancellationRecoveryClaim? cancellation = null;
        if (finalizeDecisionFirst) {
            cancellation = await CancelUntilAsync(fixture.Services, pauseAtScope: true);
            await using var database = Context(fixture.Services);
            Assert.Equal(ProcessDecisionPhase.Cancelled, (await database.DecisionGenerations.AsNoTracking().SingleAsync()).Phase);
        }
        CaptureProbe forbidden = new(fixture.Services, (_, _) => throw new InvalidOperationException("Cancelled capture cannot be invoked again."));
        var before = await SnapshotAsync(fixture.Services, fixture.State);
        var result = await Delivery(fixture.Services, forbidden).AdvanceAsync(fixture.State.RunId, admission);
        Assert.Equal(0, forbidden.Calls);
        if (point == CaptureInterruption.AfterBegin) {
            Assert.Equal(ProcessDecisionRefreshDeliveryCapturePending.UnknownInvocation,
                Assert.IsType<ProcessDecisionRefreshDeliveryResult.PendingCapture>(result).Reason);
            Assert.Equal(before, await SnapshotAsync(fixture.Services, fixture.State));
            await using var database = Context(fixture.Services);
            var allocation = await database.DecisionBudgetAllocations.AsNoTracking().SingleAsync(row =>
                row.BudgetId == saved.Reservation.Budget.Expectation.BudgetId.Value && row.AllocationId == saved.Reservation.Budget.AllocationId.Value);
            Assert.Null(allocation.OwnerWorkRevision);
            Assert.Equal(saved.Reservation.Budget.Grant.WorkTicks, allocation.GrantedWorkTicks);
            return;
        }
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(result);
        var settled = await SavedAsync(fixture.Services, fixture.State, admission, mode);
        Assert.Equal(ProcessDecisionRefreshCapturePhase.Settled, settled.Phase);
        Assert.Equal(ProcessDecisionRefreshCaptureDisposition.Aborted, settled.Disposition);
        if (point == CaptureInterruption.BeforeBegin) {
            Assert.Equal(ProcessDecisionRefreshCaptureOutcomeKind.NeverInvoked, settled.Outcome!.Kind);
            Assert.Equal(0, settled.Outcome.Work.Consumed.WorkTicks);
        } else {
            Assert.Equal(saved.Outcome, settled.Outcome);
        }
        Assert.Null(await CancelUntilAsync(fixture.Services, cancellation));
        var current = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Cancelled, current.Status);
        await using var inspection = Context(fixture.Services);
        Assert.All(await inspection.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
        Assert.Empty(await inspection.RefreshCapturePayloads.ToArrayAsync());
        var final = await SnapshotAsync(fixture.Services, current);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(await Delivery(fixture.Services, forbidden).AdvanceAsync(current.RunId, admission));
        Assert.Equal(final, await SnapshotAsync(fixture.Services, current));
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Legacy_reservation_remains_explicitly_unknown_without_reinvocation_or_refund(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        CaptureProbe capture = new(fixture.Services);
        AfterCommit older = new(fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(), request => request.RefreshCapture is not null, legacy: true);
        Assert.Equal(ProcessDecisionRefreshDeliveryCapturePending.LegacyUnknown,
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.PendingCapture>(await Delivery(fixture.Services, capture, owner: older)
                .AdvanceAsync(fixture.State.RunId, admission)).Reason);
        var before = await SnapshotAsync(fixture.Services, fixture.State);
        var result = await Delivery(fixture.Services, capture).AdvanceAsync(fixture.State.RunId, admission);
        Assert.Equal(ProcessDecisionRefreshDeliveryCapturePending.LegacyUnknown,
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.PendingCapture>(result).Reason);
        Assert.Equal(0, capture.Calls);
        Assert.Equal(before, await SnapshotAsync(fixture.Services, fixture.State));
        await using var database = Context(fixture.Services);
        Assert.Contains(await database.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => row.ReservedWorkTicks > 0);
        Assert.Empty(await database.RefreshCaptures.ToArrayAsync());
    }
}
