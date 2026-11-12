using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;

namespace CanDoItAll.Workspace.StorageRecovery.UI;

public sealed class RecoverySession(IStorageRecoveryOwner owner, Guid? storageId = null) : IDisposable {
    public const int PageSize = 8;
    private CancellationTokenSource? read;
    private long readGeneration;
    private bool disposed;
    private bool commandPending;
    private bool detailCurrent;
    private bool pagesCurrent;
    private RecoveryContext? context;

    public Guid? StorageId { get; } = storageId;
    public RecoveryPage Placements { get; private set; } = new([], null);
    public RecoveryPage FollowUps { get; private set; } = new([], null);
    public int PlacementOffset { get; private set; }
    public int FollowUpOffset { get; private set; }
    public RecoveryDetail? Selected { get; private set; }
    public RecoveryCommand? LastCommand { get; private set; }
    public RecoveryResult? LastResult { get; private set; }
    public bool ResultUnknown { get; private set; }
    public bool ExternalDispatchStopped { get; set; }
    public string Error { get; private set; } = string.Empty;
    public bool IsBusy => read is not null || commandPending;
    public bool CanAct => !disposed && !IsBusy && detailCurrent;
    public bool PagesCurrent => pagesCurrent;
    public IEnumerable<RecoveryItem> Rows => Placements.Items.Concat(FollowUps.Items);
    public event Action? Changed;

    public Task RefreshAsync() => ReadAsync(async (generation, token) => {
        var fresh = await owner.GetContextAsync(token);
        if (!Current(generation)) {
            return;
        }
        if (context != fresh) {
            ClearData();
            context = fresh;
        }
        await ReadPagesAsync(generation, fresh, token);
        if (Current(generation) && Selected is { } selected) {
            await ReadDetailAsync(generation, selected.Item.Target, token);
        }
    });

    public Task PageAsync(RecoveryFeed feed, bool next) {
        if (context is not { } current || commandPending || !pagesCurrent) {
            return Task.CompletedTask;
        }
        var offset = feed == RecoveryFeed.Placements ? PlacementOffset : FollowUpOffset;
        var nextOffset = feed == RecoveryFeed.Placements ? Placements.NextOffset : FollowUps.NextOffset;
        if (next && nextOffset is null) {
            return Task.CompletedTask;
        }
        return ReadAsync(async (generation, token) => {
            var desired = next ? nextOffset!.Value : Math.Max(0, offset - PageSize);
            var page = await owner.ReadPageAsync(current, StorageId, feed, desired, PageSize, token);
            if (!Current(generation)) {
                return;
            }
            if (feed == RecoveryFeed.Placements) {
                Placements = page;
                PlacementOffset = desired;
            } else {
                FollowUps = page;
                FollowUpOffset = desired;
            }
            pagesCurrent = true;
            if (Selected is { } selected) {
                await ReadDetailAsync(generation, selected.Item.Target, token);
            }
        }, invalidatePages: false);
    }

    public Task SelectAsync(RecoveryItem item) {
        if (commandPending || !pagesCurrent || item.Target.Context != context || !Rows.Contains(item)) {
            return Task.CompletedTask;
        }
        if (detailCurrent && Selected?.Item.Target == item.Target) {
            return Task.CompletedTask;
        }
        Selected = null;
        ExternalDispatchStopped = false;
        return ReadAsync((generation, token) => ReadDetailAsync(generation, item.Target, token), invalidatePages: false);
    }

    public async Task ExecuteAsync(RecoveryDetail rendered, RecoveryAction action) {
        if (!CanAct || !ReferenceEquals(rendered, Selected) || action == RecoveryAction.None ||
            action != rendered.Item.AvailableAction && action != rendered.OwnerAction ||
            action == RecoveryAction.VerifyExternalTermination && !ExternalDispatchStopped) {
            return;
        }
        if (action is RecoveryAction.RecordWorkflowAssetReceipt or RecoveryAction.CompletePreparedWorkflowAsset && rendered.WorkflowIntent is null) {
            return;
        }
        var command = new RecoveryCommand(rendered.Item.Target, action,
            action == RecoveryAction.VerifyExternalTermination && ExternalDispatchStopped, rendered.WorkflowIntent);
        commandPending = true;
        detailCurrent = false;
        ExternalDispatchStopped = false;
        LastCommand = command;
        LastResult = null;
        ResultUnknown = false;
        Error = string.Empty;
        Notify();
        var contextChanged = false;
        try {
            var result = await owner.ExecuteAsync(command, CancellationToken.None);
            if (result.Item.Target != command.Target) {
                throw new RecoveryException(RecoveryFailure.Unavailable);
            }
            LastResult = result;
            if (!disposed) {
                Selected = result.Detail ?? new(result.Item, RecoveryAction.None, "Refresh required", null, null);
            }
        } catch (Exception exception) {
            ResultUnknown = true;
            contextChanged = exception is RecoveryException { Failure: RecoveryFailure.StaleContext };
            if (!disposed) {
                HandleFailure(exception);
                Error += " The command outcome was not acknowledged; observe the original intent before another explicit action.";
            }
        } finally {
            commandPending = false;
            Notify();
        }
        if (!disposed && (LastResult is not null || contextChanged)) {
            var warning = Error;
            await RefreshAsync();
            if (contextChanged && string.IsNullOrEmpty(Error)) {
                Error = warning;
                Notify();
            }
        }
    }

    private async Task ReadPagesAsync(long generation, RecoveryContext captured, CancellationToken token) {
        var placements = await owner.ReadPageAsync(captured, StorageId, RecoveryFeed.Placements, PlacementOffset, PageSize, token);
        if (!Current(generation)) {
            return;
        }
        var followUps = await owner.ReadPageAsync(captured, StorageId, RecoveryFeed.OwnerFollowUps, FollowUpOffset, PageSize, token);
        if (!Current(generation)) {
            return;
        }
        Placements = placements;
        FollowUps = followUps;
        pagesCurrent = true;
    }

    private async Task ReadDetailAsync(long generation, RecoveryTarget target, CancellationToken token) {
        var detail = await owner.ReadDetailAsync(target, token);
        if (!Current(generation)) {
            return;
        }
        if (detail.Item.Target != target) {
            throw new RecoveryException(RecoveryFailure.Unavailable);
        }
        Selected = detail;
        detailCurrent = true;
        ExternalDispatchStopped = false;
    }

    private async Task ReadAsync(Func<long, CancellationToken, Task> operation, bool invalidatePages = true) {
        if (disposed || commandPending) {
            return;
        }
        read?.Cancel();
        var request = new CancellationTokenSource();
        read = request;
        var generation = ++readGeneration;
        detailCurrent = false;
        if (invalidatePages) {
            pagesCurrent = false;
        }
        Error = string.Empty;
        Notify();
        try {
            await operation(generation, request.Token);
        } catch (OperationCanceledException) when (request.IsCancellationRequested) {
        } catch (Exception exception) {
            if (Current(generation)) {
                HandleFailure(exception);
                if (exception is RecoveryException { Failure: RecoveryFailure.StaleContext }) {
                    try {
                        var fresh = await owner.GetContextAsync(request.Token);
                        if (Current(generation)) {
                            context = fresh;
                            await ReadPagesAsync(generation, fresh, request.Token);
                        }
                    } catch (OperationCanceledException) when (request.IsCancellationRequested) {
                    } catch (Exception refreshFailure) {
                        if (Current(generation)) {
                            HandleFailure(refreshFailure);
                        }
                    }
                }
                if (Current(generation) && LastResult is not null) {
                    Error += " The acknowledged result below is retained. Refresh only observes; it does not repeat the command.";
                }
            }
        } finally {
            if (ReferenceEquals(read, request)) {
                read = null;
            }
            request.Dispose();
            Notify();
        }
    }

    private void HandleFailure(Exception exception) {
        detailCurrent = false;
        pagesCurrent = false;
        ExternalDispatchStopped = false;
        if (exception is RecoveryException { Failure: RecoveryFailure.Denied or RecoveryFailure.StaleContext }) {
            ClearData();
            context = null;
        }
        Error = exception is RecoveryException recovery ? recovery.Failure switch {
            RecoveryFailure.Denied => "Current access does not allow this recovery operation.",
            RecoveryFailure.StaleContext => "The database selection changed. The previous action was not redirected; refresh its status.",
            RecoveryFailure.NotFound => "The original intent is no longer available. Refresh the list.",
            RecoveryFailure.Blocked => "The original owner no longer offers this action. Refresh its status.",
            _ => "Recovery is currently unavailable. Refresh its status before another action."
        } : "The recovery result could not be observed. Refresh its status before another action.";
    }

    private void ClearData() {
        Placements = new([], null);
        FollowUps = new([], null);
        Selected = null;
        PlacementOffset = 0;
        FollowUpOffset = 0;
        pagesCurrent = false;
        detailCurrent = false;
        ExternalDispatchStopped = false;
    }

    private bool Current(long generation) => !disposed && generation == readGeneration;
    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        readGeneration++;
        read?.Cancel();
        read = null;
        Changed = null;
    }
}
