using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class CrmAccountRecordsObservationTests {
    [Fact]
    public async Task One_exact_subscription_filters_own_and_other_account_changes_and_releases_one_hundred_openings() {
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var activity = 0;
        var connections = 0;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observer = new CrmAccountRecordsObservation(changes, feed, new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance) {
            Dispatch = work => work(), ConnectionsChanged = () => connections++,
            ActivityChanged = () => {
                activity++;
                return Task.CompletedTask;
            }, Retired = () => { },
            Changed = () => received.TrySetResult()
        };
        var account = Guid.NewGuid();
        for (var index = 0; index < 100; index++) {
            await observer.OpenAsync(account, default);
            Assert.Equal(1, feed.ActiveSubscriptions);
            observer.Clear();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }
        await observer.OpenAsync(account, default);
        using (observer.EnterWrite(account)) {
            changes.PublishAccountRecords(account, EditingChangeKind.Created);
        }
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, activity);
        Assert.Equal(0, connections);
        changes.PublishAccountRecords(Guid.NewGuid(), EditingChangeKind.Created);
        received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        changes.PublishAccountRecords(account, EditingChangeKind.EditableContentChanged);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, activity);
        Assert.Equal(1, connections);
        Assert.Equal(1, observer.ConnectionRevision);
        received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        changes.PublishAccountRecords(account, EditingChangeKind.Created);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, activity);
        Assert.Equal(1, connections);
    }
}
