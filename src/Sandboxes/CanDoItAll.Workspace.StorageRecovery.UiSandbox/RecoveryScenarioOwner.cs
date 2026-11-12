using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;

namespace CanDoItAll.Workspace.StorageRecovery.UiSandbox;

public enum RecoveryScenario {
    Ready, Loading, Empty, Denied, ReadOnly, Unavailable, EmptyOwnerPage,
    WorkflowPrepared, WorkflowReceipt, CancelledRun, ExternalTermination, RefreshFailure, UnknownOutcome, StaleContext
}

public sealed class RecoveryScenarioOwner(RecoveryScenario scenario = RecoveryScenario.Ready) : IStorageRecoveryOwner {
    public static readonly Guid ProfileId = Guid.Parse("85390372-c5cd-4c0d-b95a-4290c3d2530f");
    public static readonly Guid StorageId = Guid.Parse("f46678f5-f0d5-46ba-98bc-87e394a92177");
    public static readonly Guid PlacementId = Guid.Parse("1988b7b4-c92c-41cc-a946-b30261ae28ae");
    public static readonly Guid FollowUpId = Guid.Parse("72996c6e-3d32-4ca4-9d60-fd4dd5b9730f");
    public static readonly Guid ProjectId = Guid.Parse("4f7ae47a-bde6-41ee-a79d-876c3d850945");
    public static readonly Guid RunId = Guid.Parse("da789307-f6be-434d-a375-4f317d9e9ae9");
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private RecoveryContext context = new(ProfileId, 1);
    private bool committed;
    public List<RecoveryCommand> Commands { get; } = [];
    public bool FailRefresh { get; set; } = scenario == RecoveryScenario.RefreshFailure;
    public void Release() => released.TrySetResult();

    public async Task<RecoveryContext> GetContextAsync(CancellationToken cancellationToken) {
        if (scenario == RecoveryScenario.Loading) {
            await released.Task.WaitAsync(cancellationToken);
        }
        if (scenario == RecoveryScenario.Denied) {
            throw new RecoveryException(RecoveryFailure.Denied);
        }
        if (scenario == RecoveryScenario.Unavailable || committed && FailRefresh) {
            throw new RecoveryException(RecoveryFailure.Unavailable);
        }
        return context;
    }

    public Task<RecoveryPage> ReadPageAsync(RecoveryContext captured, Guid? storageId, RecoveryFeed feed, int offset, int take,
        CancellationToken cancellationToken) {
        Require(captured);
        if (scenario == RecoveryScenario.Empty || storageId is { } filter && filter != StorageId) {
            return Task.FromResult(new RecoveryPage([], null));
        }
        if (feed == RecoveryFeed.OwnerFollowUps && scenario == RecoveryScenario.EmptyOwnerPage && offset == 0) {
            return Task.FromResult(new RecoveryPage([], take));
        }
        var item = Item(feed == RecoveryFeed.Placements ? PlacementId : FollowUpId);
        return Task.FromResult(new RecoveryPage([item], null));
    }

    public Task<RecoveryDetail> ReadDetailAsync(RecoveryTarget target, CancellationToken cancellationToken) {
        Require(target.Context);
        var item = Item(target.IntentId);
        var action = committed || scenario == RecoveryScenario.ReadOnly ? RecoveryAction.None : scenario switch {
            RecoveryScenario.WorkflowReceipt => RecoveryAction.RecordWorkflowAssetReceipt,
            RecoveryScenario.CancelledRun => RecoveryAction.ReconcileCancelledRunReceipts,
            _ => RecoveryAction.CompletePreparedWorkflowAsset
        };
        return Task.FromResult(new RecoveryDetail(item, target.IntentId == FollowUpId ? action : RecoveryAction.None,
            committed ? "ReceiptRecorded" : "Ready", scenario == RecoveryScenario.CancelledRun ? RunId : null,
            target.IntentId == FollowUpId && scenario != RecoveryScenario.CancelledRun
                ? new(RunId, new string('a', 64), 3, new string('b', 64)) : null));
    }

    public async Task<RecoveryResult> ExecuteAsync(RecoveryCommand command, CancellationToken cancellationToken) {
        Require(command.Target.Context);
        var expected = await ReadDetailAsync(command.Target, cancellationToken);
        if (command.Action == RecoveryAction.None || command.Action != expected.Item.AvailableAction && command.Action != expected.OwnerAction ||
            command.Action == RecoveryAction.VerifyExternalTermination && !command.VerifiedExternalDispatchStopped ||
            command.Action is RecoveryAction.CompletePreparedWorkflowAsset or RecoveryAction.RecordWorkflowAssetReceipt && command.ExpectedWorkflow != expected.WorkflowIntent) {
            throw new RecoveryException(RecoveryFailure.Blocked);
        }
        Commands.Add(command);
        if (scenario == RecoveryScenario.StaleContext) {
            context = context with { Generation = context.Generation + 1 };
            throw new RecoveryException(RecoveryFailure.StaleContext);
        }
        if (scenario == RecoveryScenario.UnknownOutcome) {
            throw new RecoveryException(RecoveryFailure.Unavailable);
        }
        committed = true;
        var detail = await ReadDetailAsync(command.Target, cancellationToken);
        return new(detail.Item, detail);
    }

    private RecoveryItem Item(Guid id) => new(new(context, id), ProjectId, StorageId,
        committed || id == FollowUpId ? "Completed" : "Uncertain", new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero),
        committed || id == FollowUpId, committed || scenario == RecoveryScenario.WorkflowReceipt,
        id == FollowUpId || committed || scenario == RecoveryScenario.ReadOnly ? RecoveryAction.None :
            scenario == RecoveryScenario.ExternalTermination ? RecoveryAction.VerifyExternalTermination : RecoveryAction.Reconcile,
        scenario == RecoveryScenario.ReadOnly ? "Read-only access" : "See the available action below.",
        id == FollowUpId ? "Owner follow-up" : "File placement");

    private void Require(RecoveryContext captured) {
        if (captured != context) {
            throw new RecoveryException(RecoveryFailure.StaleContext);
        }
    }
}
