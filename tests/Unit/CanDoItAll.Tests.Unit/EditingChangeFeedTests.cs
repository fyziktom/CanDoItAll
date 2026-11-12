using System.Threading.Channels;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Tests.Unit.Editing;

public sealed class EditingChangeFeedTests {
    private readonly EditingScope scope = new(EditingScopeKind.Dataset, Guid.NewGuid());
    private readonly Guid resourceId = Guid.NewGuid();

    [Fact]
    public async Task Owners_with_the_same_dataset_and_resource_id_remain_separate_topics() {
        var feed = new EditingChangeFeed();
        using var prompt = feed.Subscribe(Topic());
        var partyTopic = Topic() with { Owner = EditingOwner.CrmHrParty };
        using var party = feed.Subscribe(partyTopic);
        using var accounts = feed.Subscribe(partyTopic with { Owner = EditingOwner.CrmAccountProfile, ResourceId = null });

        feed.Publish(partyTopic, EditingChangeKind.EditableContentChanged);

        Assert.Equal(1, feed.DeliveredHints);
        Assert.Equal(EditingOwner.CrmHrParty, Assert.Single((await party.ReadAsync(default)).Changes).Resource.Owner);
    }

    [Fact]
    public async Task Exact_key_and_catalog_delivery_does_not_wake_a_thousand_unrelated_editors() {
        var feed = new EditingChangeFeed();
        using var exact = feed.Subscribe(Topic());
        using var catalog = feed.Subscribe(Topic() with { ResourceId = null });
        var unrelated = Enumerable.Range(0, 1000).Select(_ => feed.Subscribe(Topic() with { ResourceId = Guid.NewGuid() })).ToArray();
        try {
            feed.Publish(Topic(), EditingChangeKind.EditableContentChanged);
            Assert.Equal(2, feed.DeliveredHints);
            Assert.Equal(resourceId, Assert.Single((await exact.ReadAsync(default)).Changes).Resource.ResourceId);
            Assert.Equal(resourceId, Assert.Single((await catalog.ReadAsync(default)).Changes).Resource.ResourceId);
        } finally {
            foreach (var subscription in unrelated) {
                subscription.Dispose();
            }
        }
    }

    [Fact]
    public async Task Overflow_is_bounded_and_explicitly_requires_resynchronization() {
        var feed = new EditingChangeFeed();
        using var subscription = feed.Subscribe(Topic());
        for (var index = 0; index < 100; index++) {
            feed.Publish(Topic(), EditingChangeKind.MetadataChanged);
        }
        var batch = await subscription.ReadAsync(default);
        Assert.True(batch.RequiresResync);
        Assert.Equal(EditingChangeFeed.SubscriptionCapacity, batch.Changes.Count);
        Assert.Equal(100, batch.Changes[^1].Cursor.Sequence);
        Assert.Equal(Enumerable.Range(85, 16).Select(value => (long)value), batch.Changes.Select(change => change.Cursor.Sequence));
    }

    [Fact]
    public async Task Joining_before_a_read_preserves_changes_after_the_captured_high_water() {
        var feed = new EditingChangeFeed();
        using var subscription = feed.Subscribe(Topic());
        var beforeRead = subscription.JoinedAt;
        feed.Publish(Topic(), EditingChangeKind.EditableContentChanged);
        var change = Assert.Single((await subscription.ReadAsync(default)).Changes);
        Assert.Equal(beforeRead.Epoch, change.Cursor.Epoch);
        Assert.True(change.Cursor.Sequence > beforeRead.Sequence);
        feed.Publish(Topic(), EditingChangeKind.LifecycleChanged);
        var later = Assert.Single((await subscription.ReadAsync(default)).Changes);
        Assert.True(later.Cursor.Sequence > change.Cursor.Sequence);
    }

    [Fact]
    public async Task Scope_and_activation_generation_do_not_alias_after_switching_back() {
        var feed = new EditingChangeFeed();
        using var oldOpening = feed.Subscribe(Topic());
        using var otherProfile = feed.Subscribe(Topic() with { Scope = scope with { Id = Guid.NewGuid() } });
        var returnedTopic = Topic() with { Scope = scope with { ActivationGeneration = 2 } };
        using var returnedOpening = feed.Subscribe(returnedTopic);
        feed.Publish(returnedTopic, EditingChangeKind.AccessChanged);
        Assert.Equal(1, feed.DeliveredHints);
        Assert.Equal(2, Assert.Single((await returnedOpening.ReadAsync(default)).Changes).Resource.Scope.ActivationGeneration);
    }

    [Fact]
    public async Task Two_editors_in_one_circuit_receive_each_others_exact_operation_identity() {
        var feed = new EditingChangeFeed();
        var operations = new EditingOperationContext();
        using var a = feed.Subscribe(Topic());
        using var b = feed.Subscribe(Topic());
        var first = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        using (operations.Enter(first)) {
            await Task.Yield();
            Assert.Equal(first, operations.Current);
            var nested = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
            using (operations.Enter(nested)) {
                Assert.Equal(nested, operations.Current);
            }
            Assert.Equal(first, operations.Current);
            feed.Publish(Topic(), EditingChangeKind.EditableContentChanged, operations.Current!.EditorId, operations.Current.OperationId);
        }
        Assert.Null(operations.Current);
        var seenA = Assert.Single((await a.ReadAsync(default)).Changes);
        var seenB = Assert.Single((await b.ReadAsync(default)).Changes);
        Assert.Equal(first.EditorId, seenB.OriginEditorId);
        Assert.Equal(first.OperationId, seenB.OperationId);
        Assert.Equal(seenA, seenB);
    }

    [Fact]
    public async Task Dispose_releases_waiting_reads_and_all_empty_partitions() {
        var feed = new EditingChangeFeed();
        for (var index = 0; index < 100; index++) {
            var subscription = feed.Subscribe(Topic());
            var pending = subscription.ReadAsync(default).AsTask();
            subscription.Dispose();
            subscription.Dispose();
            await Assert.ThrowsAsync<ChannelClosedException>(() => pending);
        }
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public void Restarted_feed_has_a_new_epoch_and_invalid_addresses_are_rejected() {
        var first = new EditingChangeFeed();
        var restarted = new EditingChangeFeed();
        using var a = first.Subscribe(Topic());
        using var b = restarted.Subscribe(Topic());
        Assert.NotEqual(a.JoinedAt.Epoch, b.JoinedAt.Epoch);
        Assert.Throws<ArgumentException>(() => first.Subscribe(Topic() with { ResourceId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => first.Subscribe(Topic() with { Scope = scope with { Id = Guid.Empty } }));
        Assert.Throws<ArgumentException>(() => first.Publish(Topic() with { ResourceId = null }, EditingChangeKind.Created));
    }

    private EditingTopic Topic() => new(scope, EditingOwner.PromptGallery, resourceId);
}
