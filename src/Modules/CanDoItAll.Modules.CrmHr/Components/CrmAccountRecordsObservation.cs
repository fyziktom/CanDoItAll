using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed class CrmAccountRecordsObservation(CrmHrChanges changes, EditingChangeFeed feed, IEditingUiAccess access,
    ILogger<EditingObservation> logger) : IAsyncDisposable {
    private EditingObservation? observation;
    private Task retirement = Task.CompletedTask;
    private readonly Queue<Guid> operations = new();
    private Guid editorId;
    private long opening;
    private bool disposed;
    public Guid? AccountId { get; private set; }
    public long ConnectionRevision { get; private set; }
    public required Func<Func<Task>, Task> Dispatch { get; init; }
    public required Action ConnectionsChanged { get; init; }
    public required Func<Task> ActivityChanged { get; init; }
    public required Action Retired { get; init; }
    public required Action Changed { get; init; }

    public async Task OpenAsync(Guid accountId, CancellationToken cancellationToken) {
        if (AccountId == accountId && observation is { } current) {
            await current.AuthorizeAsync(write: false, cancellationToken);
            return;
        }
        Clear();
        var generation = opening;
        await retirement;
        if (disposed || generation != opening) {
            throw new OperationCanceledException("The account record observation was superseded.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        AccountId = accountId;
        editorId = Guid.NewGuid();
        var source = new EditingObservation(feed, access, new(changes.Scope, EditingOwner.CrmAccountRecords, accountId), logger);
        observation = source;
        try {
            await source.StartAsync(batch => Dispatch(async () => {
                if (disposed || generation != opening) {
                    return;
                }
                var foreign = batch.Changes.Where(change => change.OriginEditorId != editorId || !operations.Contains(change.OperationId)).ToArray();
                if (batch.RequiresResync || foreign.Any(change => change.Kind != EditingChangeKind.Created)) {
                    ConnectionRevision++;
                    ConnectionsChanged();
                }
                if (batch.RequiresResync || foreign.Any(change => change.Kind is EditingChangeKind.Created or EditingChangeKind.Deleted)) {
                    await ActivityChanged();
                }
                Changed();
            }), () => Dispatch(() => {
                if (!disposed && generation == opening) {
                    Clear();
                    Retired();
                    Changed();
                }
                return Task.CompletedTask;
            }));
            cancellationToken.ThrowIfCancellationRequested();
            if (disposed || generation != opening) {
                throw new OperationCanceledException("The account record observation was superseded.");
            }
        } catch {
            if (ReferenceEquals(source, observation)) {
                Clear();
            }
            throw;
        }
    }

    public IDisposable EnterWrite(Guid accountId) {
        if (disposed || observation is null || AccountId != accountId) {
            throw new UnauthorizedAccessException("The account records no longer belong to this opening.");
        }
        var operation = Guid.NewGuid();
        operations.Enqueue(operation);
        while (operations.Count > 32) {
            operations.Dequeue();
        }
        return changes.Enter(new(editorId, operation));
    }

    public void Clear() {
        opening++;
        AccountId = null;
        ConnectionRevision = 0;
        operations.Clear();
        var previous = observation;
        observation = null;
        if (previous is not null) {
            retirement = RetireAsync(retirement, previous);
        }
    }

    private static async Task RetireAsync(Task earlier, EditingObservation previous) {
        await earlier;
        await previous.DisposeAsync();
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        Clear();
        await retirement;
    }
}
