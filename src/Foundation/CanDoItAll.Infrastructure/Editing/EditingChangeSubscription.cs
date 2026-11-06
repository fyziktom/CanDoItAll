using System.Threading.Channels;
using CanDoItAll.Editing;

namespace CanDoItAll.Infrastructure.Editing;

internal sealed class EditingChangeSubscription(EditingCursor joinedAt, Action<EditingChangeSubscription> remove)
    : IEditingChangeSubscription {
    private readonly Channel<EditingChange> channel = Channel.CreateBounded<EditingChange>(new BoundedChannelOptions(
        EditingChangeFeed.SubscriptionCapacity) { FullMode = BoundedChannelFullMode.Wait });
    private int gap;
    private int disposed;

    public EditingCursor JoinedAt { get; } = joinedAt;

    public void Enqueue(EditingChange change) {
        if (channel.Writer.TryWrite(change)) {
            return;
        }
        Interlocked.Exchange(ref gap, 1);
        channel.Reader.TryRead(out _);
        channel.Writer.TryWrite(change);
    }

    public async ValueTask<EditingChangeBatch> ReadAsync(CancellationToken cancellationToken) {
        var first = await channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var changes = new List<EditingChange> { first };
        while (changes.Count < EditingChangeFeed.SubscriptionCapacity && channel.Reader.TryRead(out var next)) {
            changes.Add(next);
        }
        return new(changes, Interlocked.Exchange(ref gap, 0) != 0);
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        remove(this);
        channel.Writer.TryComplete();
    }
}
