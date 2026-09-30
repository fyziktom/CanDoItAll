using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;
using CanDoItAll.Workspace.StorageRecovery.UI;
using CanDoItAll.Workspace.StorageRecovery.UiSandbox;

namespace CanDoItAll.Tests.Components.WorkspaceStorageRecoveryUi;

public sealed class RecoverySessionTests {
    [Fact]
    public async Task Empty_scanned_owner_page_advances_independently_without_an_effect() {
        var owner = new RecoveryScenarioOwner(RecoveryScenario.EmptyOwnerPage);
        using var session = new RecoverySession(owner, RecoveryScenarioOwner.StorageId);
        await session.RefreshAsync();
        Assert.Empty(session.FollowUps.Items);
        Assert.Equal(8, session.FollowUps.NextOffset);
        await session.PageAsync(RecoveryFeed.OwnerFollowUps, true);
        Assert.Equal(0, session.PlacementOffset);
        Assert.Equal(8, session.FollowUpOffset);
        Assert.Single(session.FollowUps.Items);
        await session.SelectAsync(session.FollowUps.Items[0]);
        await session.RefreshAsync();
        Assert.Empty(owner.Commands);
    }

    [Theory]
    [InlineData(RecoveryScenario.WorkflowPrepared, RecoveryAction.CompletePreparedWorkflowAsset)]
    [InlineData(RecoveryScenario.WorkflowReceipt, RecoveryAction.RecordWorkflowAssetReceipt)]
    [InlineData(RecoveryScenario.CancelledRun, RecoveryAction.ReconcileCancelledRunReceipts)]
    public async Task Owner_actions_capture_the_exact_observed_request(RecoveryScenario scenario, RecoveryAction action) {
        var owner = new RecoveryScenarioOwner(scenario);
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.FollowUps.Items[0]);
        var detail = session.Selected!;
        await session.ExecuteAsync(detail, action);
        var command = Assert.Single(owner.Commands);
        Assert.Equal(detail.Item.Target, command.Target);
        Assert.Equal(detail.WorkflowIntent, command.ExpectedWorkflow);
        Assert.Equal(action, command.Action);
        Assert.Equal(detail.Item.Target, session.LastResult!.Item.Target);
        Assert.True(session.LastResult.Item.NativeReceiptPresent);
    }

    [Fact]
    public async Task Same_acquired_detail_does_not_read_again_but_failed_detail_retries() {
        var owner = new ControlledOwner();
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        var item = session.Placements.Items[0];
        await session.SelectAsync(item);
        var original = session.Selected;
        await session.SelectAsync(item);
        Assert.Same(original, session.Selected);
        Assert.Equal(1, owner.Reads);
        owner.Detail = (_, _) => throw new RecoveryException(RecoveryFailure.Unavailable);
        await session.RefreshAsync();
        Assert.False(session.CanAct);
        owner.Detail = owner.Source.ReadDetailAsync;
        await session.RefreshAsync();
        Assert.True(session.CanAct);
        Assert.Equal(3, owner.Reads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_B_A_ignores_old_success_or_denial_and_releases_request_sources(bool denyOld) {
        var owner = new ControlledOwner();
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        var a = session.Placements.Items[0];
        var b = session.FollowUps.Items[0];
        var heldA = new TaskCompletionSource<RecoveryDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldB = new TaskCompletionSource<RecoveryDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = new List<CancellationToken>();
        owner.Detail = (target, token) => {
            requests.Add(token);
            return requests.Count switch {
                1 => heldA.Task,
                2 => heldB.Task,
                _ => owner.Source.ReadDetailAsync(target, token)
            };
        };
        var first = session.SelectAsync(a);
        var second = session.SelectAsync(b);
        await session.SelectAsync(a);
        var current = session.Selected;
        Assert.Equal(a.Target, current!.Item.Target);
        if (denyOld) {
            heldA.SetException(new RecoveryException(RecoveryFailure.Denied));
        } else {
            heldA.SetResult(await owner.Source.ReadDetailAsync(a.Target, default));
        }
        heldB.SetResult(await owner.Source.ReadDetailAsync(b.Target, default));
        await Task.WhenAll(first, second);
        Assert.Same(current, session.Selected);
        Assert.True(session.CanAct);
        Assert.Empty(session.Error);
        Assert.All(requests, token => Assert.Throws<ObjectDisposedException>(() => token.WaitHandle));
    }

    [Fact]
    public async Task Close_releases_a_read_even_when_the_dependency_ignores_cancellation() {
        var owner = new ControlledOwner();
        var session = new RecoverySession(owner);
        await session.RefreshAsync();
        var completion = new TaskCompletionSource<RecoveryDetail>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        owner.Detail = (_, token) => {
            captured = token;
            return completion.Task;
        };
        var item = session.Placements.Items[0];
        var pending = session.SelectAsync(item);
        var publications = 0;
        session.Changed += () => publications++;
        session.Dispose();
        Assert.True(captured.IsCancellationRequested);
        completion.SetResult(await owner.Source.ReadDetailAsync(item.Target, default));
        await pending;
        Assert.Null(session.Selected);
        Assert.Equal(0, publications);
        Assert.Throws<ObjectDisposedException>(() => captured.WaitHandle);
    }

    [Fact]
    public async Task Reentrant_and_stale_handlers_admit_one_command_and_closed_result_keeps_its_identity() {
        var owner = new ControlledOwner();
        var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.Placements.Items[0]);
        var detail = session.Selected!;
        var completion = new TaskCompletionSource<RecoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken commandToken = default;
        owner.Command = (_, token) => {
            commandToken = token;
            return completion.Task;
        };
        await session.ExecuteAsync(detail with { }, RecoveryAction.Reconcile);
        Assert.Equal(0, owner.Commands);
        var pending = session.ExecuteAsync(detail, RecoveryAction.Reconcile);
        await session.ExecuteAsync(detail, RecoveryAction.Reconcile);
        await session.RefreshAsync();
        Assert.Equal(1, owner.Commands);
        session.Dispose();
        var successor = new RecoverySession(owner);
        using (successor) {
            await successor.RefreshAsync();
            var result = new RecoveryResult(detail.Item with { StorageReceiptPresent = true });
            completion.SetResult(result);
            await pending;
            Assert.False(commandToken.CanBeCanceled);
            Assert.Same(result, session.LastResult);
            Assert.Null(successor.LastResult);
            Assert.Null(successor.Selected);
        }
    }

    [Fact]
    public async Task Acknowledged_progress_survives_refresh_failure_and_retry_only_observes() {
        var owner = new RecoveryScenarioOwner(RecoveryScenario.RefreshFailure);
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.Placements.Items[0]);
        await session.ExecuteAsync(session.Selected!, RecoveryAction.Reconcile);
        Assert.True(session.LastResult!.Item.StorageReceiptPresent);
        Assert.Equal(session.LastCommand!.Target, session.Selected!.Item.Target);
        Assert.False(session.CanAct);
        Assert.Contains("acknowledged result", session.Error, StringComparison.Ordinal);
        owner.FailRefresh = false;
        await session.RefreshAsync();
        Assert.Single(owner.Commands);
        Assert.True(session.Selected.Item.StorageReceiptPresent);
    }

    [Fact]
    public async Task Unknown_acknowledgement_never_replays_on_refresh() {
        var owner = new RecoveryScenarioOwner(RecoveryScenario.UnknownOutcome);
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.Placements.Items[0]);
        await session.ExecuteAsync(session.Selected!, RecoveryAction.Reconcile);
        Assert.True(session.ResultUnknown);
        Assert.Null(session.LastResult);
        Assert.False(session.CanAct);
        await session.RefreshAsync();
        Assert.Single(owner.Commands);
        Assert.True(session.ResultUnknown);
    }

    [Fact]
    public async Task Verification_requires_current_detail_and_explicit_true_attestation() {
        var owner = new RecoveryScenarioOwner(RecoveryScenario.ExternalTermination);
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.Placements.Items[0]);
        var detail = session.Selected!;
        await session.ExecuteAsync(detail, RecoveryAction.VerifyExternalTermination);
        Assert.Empty(owner.Commands);
        session.ExternalDispatchStopped = true;
        await session.ExecuteAsync(detail with { }, RecoveryAction.VerifyExternalTermination);
        Assert.Empty(owner.Commands);
        await session.ExecuteAsync(detail, RecoveryAction.VerifyExternalTermination);
        Assert.True(Assert.Single(owner.Commands).VerifiedExternalDispatchStopped);
    }

    [Fact]
    public async Task Current_denial_retires_rows_and_direct_actions() {
        var owner = new ControlledOwner();
        using var session = new RecoverySession(owner);
        await session.RefreshAsync();
        await session.SelectAsync(session.Placements.Items[0]);
        var stale = session.Selected!;
        owner.Detail = (_, _) => throw new RecoveryException(RecoveryFailure.Denied);
        await session.RefreshAsync();
        Assert.Null(session.Selected);
        Assert.Empty(session.Rows);
        await session.ExecuteAsync(stale, RecoveryAction.Reconcile);
        Assert.Equal(0, owner.Commands);
    }

    private sealed class ControlledOwner : IStorageRecoveryOwner {
        public RecoveryScenarioOwner Source { get; } = new();
        public Func<RecoveryTarget, CancellationToken, Task<RecoveryDetail>> Detail { get; set; }
        public Func<RecoveryCommand, CancellationToken, Task<RecoveryResult>> Command { get; set; }
        public int Reads { get; private set; }
        public int Commands { get; private set; }
        public ControlledOwner() {
            Detail = Source.ReadDetailAsync;
            Command = Source.ExecuteAsync;
        }
        public Task<RecoveryContext> GetContextAsync(CancellationToken token) => Source.GetContextAsync(token);
        public Task<RecoveryPage> ReadPageAsync(RecoveryContext context, Guid? storageId, RecoveryFeed feed, int offset, int take, CancellationToken token) =>
            Source.ReadPageAsync(context, storageId, feed, offset, take, token);
        public Task<RecoveryDetail> ReadDetailAsync(RecoveryTarget target, CancellationToken token) {
            Reads++;
            return Detail(target, token);
        }
        public Task<RecoveryResult> ExecuteAsync(RecoveryCommand command, CancellationToken token) {
            Commands++;
            return Command(command, token);
        }
    }
}
