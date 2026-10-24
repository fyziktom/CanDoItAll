using System.Text.Json;
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
public sealed class ProcessDecisionRefreshDeliveryTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Registered_delivery_stops_accounts_advances_and_replays_without_more_work(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var delivery = fixture.Services.GetRequiredService<ProcessDecisionRefreshDeliveryCoordinator>();
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(await delivery.AdvanceAsync(fixture.State.RunId, admission));
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Equal(ProcessRuntimeStepStatus.Ready, state.Steps.Single().Status);
        Assert.Equal(JsonSerializer.Serialize(fixture.State.Claims), JsonSerializer.Serialize(state.Claims));
        Assert.Equal(JsonSerializer.Serialize(fixture.State.AppliedResults), JsonSerializer.Serialize(state.AppliedResults));
        await using var database = Context(fixture.Services);
        Assert.All(await database.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
        Assert.All(await database.RefreshCaptures.AsNoTracking().ToArrayAsync(), row => Assert.Equal(ProcessDecisionRefreshCapturePhase.Settled, row.Phase));
        var saved = await SnapshotAsync(fixture.Services, state);
        await using var restarted = fixture.Services.CreateAsyncScope();
        CaptureProbe forbidden = new(restarted.ServiceProvider, (_, _) => throw new InvalidOperationException("A completed capture must not repeat."));
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.Advanced>(await Delivery(restarted.ServiceProvider, forbidden)
            .AdvanceAsync(state.RunId, admission));
        Assert.Equal(0, forbidden.Calls);
        Assert.Equal(saved, await SnapshotAsync(fixture.Services, state));
        var original = (await fixture.Services.GetRequiredService<IProcessDecisionRefreshReceiptReader>().ReadAsync(state.RunId, admission))!;
        var events = await database.RuntimeEvents.AsNoTracking().Where(row => row.RunId == state.RunId.Value && row.OccurredAtUtc == state.UpdatedAtUtc).ToArrayAsync();
        Assert.Equal(fixture.State.Status == ProcessRuntimeStatus.Active ? 2 : 3, events.Length);
        var actor = ((ProcessDecisionBeginRefresh)original.Decision.Command).Intent.Actor;
        Assert.All(events, item => {
            Assert.Equal(actor.Kind.ToString(), item.ActorKind);
            Assert.Equal(actor.Id.Value, item.ActorId);
        });
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        Assert.Single(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Accepted_original_effect_completes_and_replays_without_capture(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        await fixture.PublishAsync();
        var admission = await AdmitAsync(fixture);
        CaptureProbe forbidden = new(fixture.Services, (_, _) => throw new InvalidOperationException("An accepted effect must not refresh."));
        var delivery = Delivery(fixture.Services, forbidden);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.CompletedOriginal>(await delivery.AdvanceAsync(fixture.State.RunId, admission));
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.State.RunId))!;
        Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
        var saved = await SnapshotAsync(fixture.Services, state);
        Assert.IsType<ProcessDecisionRefreshDeliveryResult.CompletedOriginal>(await delivery.AdvanceAsync(state.RunId, admission));
        Assert.Equal(saved, await SnapshotAsync(fixture.Services, state));
        Assert.Equal(0, forbidden.Calls);
        await fixture.Entry.Files.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(ProcessDecisionChildStopResult.ActiveWork)]
    [InlineData(ProcessDecisionChildStopResult.LeaseBusy)]
    [InlineData(ProcessDecisionChildStopResult.MissingJournal)]
    [InlineData(ProcessDecisionChildStopResult.UnresolvedEffects)]
    [InlineData(ProcessDecisionChildStopResult.CompletionPending)]
    public async Task Pending_stop_preserves_original_grant_and_does_not_account_or_capture(ProcessDecisionChildStopResult reason) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var before = await SnapshotAsync(fixture.Services, fixture.State);
        var actual = Assert.Single(fixture.Services.GetServices<IProcessDecisionRefreshChildStopper>());
        StopProbe held = new(actual, (binding, action, _) => {
            Assert.Equal(fixture.Parent, binding);
            Assert.Equal(admission, action);
            return Task.FromResult(reason);
        });
        CaptureProbe capture = new(fixture.Services);
        var pending = Assert.IsType<ProcessDecisionRefreshDeliveryResult.PendingStop>(await Delivery(fixture.Services, capture, stoppers: [held])
            .AdvanceAsync(fixture.State.RunId, admission));
        Assert.Equal(reason, pending.Reason);
        Assert.Equal(1, held.Calls);
        Assert.Equal(0, capture.Calls);
        Assert.Equal(before, await SnapshotAsync(fixture.Services, fixture.State));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true)]
    [InlineData(ReadBasisMode.StepSnapshot, true)]
    public async Task Cancellation_during_stop_hands_original_accounting_to_cancellation(ReadBasisMode mode, bool beforeStop) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var actual = Assert.Single(fixture.Services.GetServices<IProcessDecisionRefreshChildStopper>());
        StopProbe stopped = new(actual, async (binding, action, token) => {
            if (beforeStop) {
                await CancelAsync(fixture.Services, fixture.State, fixture.CommandContext);
                return await actual.StopAsync(binding, action, token);
            }
            var result = await actual.StopAsync(binding, action, token);
            await CancelAsync(fixture.Services, fixture.State, fixture.CommandContext);
            return result;
        });
        CaptureProbe capture = new(fixture.Services);
        var delivered = await Delivery(fixture.Services, capture, stoppers: [stopped]).AdvanceAsync(fixture.State.RunId, admission);
        if (beforeStop) {
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.Contended>(delivered);
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(await Delivery(fixture.Services, capture)
                .AdvanceAsync(fixture.State.RunId, admission));
        } else {
            Assert.IsType<ProcessDecisionRefreshDeliveryResult.Stopped>(delivered);
        }
        Assert.Null(await fixture.Services.GetRequiredService<IProcessIdempotencyStore>()
            .FindCompletedCommandAsync(new ProcessDecisionRefreshAccounting(admission).CommandId));
        Assert.Null(await CancelUntilAsync(fixture.Services));
        await using var database = Context(fixture.Services);
        Assert.All(await database.DecisionBudgets.AsNoTracking().ToArrayAsync(), row => Assert.Equal(0, row.ReservedWorkTicks));
        Assert.Equal(0, capture.Calls);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Another_admission_cannot_stop_or_mutate_the_original_child() {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(freezeClock: false);
        await AdmitAsync(fixture);
        var before = await SnapshotAsync(fixture.Services, fixture.State);
        var child = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Services.GetRequiredService<ProcessDecisionRefreshDeliveryCoordinator>()
            .AdvanceAsync(fixture.State.RunId, RuntimeCommandId.New()));
        Assert.Equal(before, await SnapshotAsync(fixture.Services, fixture.State));
        Assert.Equal(child, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunAsync(fixture.Waiting.Id)));
    }
}
