using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectCatalogObservationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Private_recovery_reads_revalidate_access_before_returning_a_body(bool denyBefore) {
        var feed = new EditingChangeFeed();
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 4);
        var access = new EditingUiTestAccess();
        int reads = 0;
        int retired = 0;
        await using var session = new ProjectCatalogObservation(feed, access, scope, NullLogger<EditingObservation>.Instance);
        await session.StartAsync(() => Task.CompletedTask, () => retired++, action => action());
        if (denyBefore) {
            access.Allowed = false;
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => session.ReadAsync(_ => {
            reads++;
            access.Allowed = false;
            return Task.FromResult("Private retained operation body");
        }, default));

        Assert.Equal(denyBefore ? 0 : 1, reads);
        Assert.True(session.IsRetired);
        Assert.Equal(1, retired);
        Assert.Equal(0, feed.ActiveSubscriptions);
    }

    [Fact]
    public async Task Requests_during_a_read_queue_one_trailing_read_and_unrelated_scopes_are_quiet() {
        var feed = new EditingChangeFeed();
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 7);
        var access = new EditingUiTestAccess();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trailing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var active = 0;
        var maxActive = 0;
        var retired = 0;
        await using var session = new ProjectCatalogObservation(feed, access, scope, NullLogger<EditingObservation>.Instance);
        await session.StartAsync(async () => {
            var concurrent = Interlocked.Increment(ref active);
            maxActive = Math.Max(maxActive, concurrent);
            if (Interlocked.Increment(ref reads) == 1) {
                started.TrySetResult();
                await release.Task;
            } else {
                trailing.TrySetResult();
            }
            Interlocked.Decrement(ref active);
        }, () => retired++, action => action());
        var first = session.RefreshAsync();
        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            for (var index = 0; index < 80; index++) {
                _ = session.RefreshAsync();
            }
            Assert.Equal(1, reads);
        } finally {
            release.TrySetResult();
            await first;
        }
        await trailing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, reads);
        Assert.Equal(1, maxActive);
        Assert.Equal(0, retired);
        var delivered = feed.DeliveredHints;
        feed.Publish(new(scope with { ActivationGeneration = 8 }, EditingOwner.Project, Guid.NewGuid()), EditingChangeKind.Created);
        feed.Publish(new(scope, EditingOwner.ProjectAssignment, Guid.NewGuid()), EditingChangeKind.Created);
        Assert.Equal(delivered, feed.DeliveredHints);
        await session.DisposeAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        await session.RefreshAsync();
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task Access_is_held_across_the_read_and_revocation_cannot_publish_its_result() {
        var feed = new EditingChangeFeed();
        var scope = new EditingScope(EditingScopeKind.Dataset, Guid.NewGuid(), 4);
        var access = new EditingUiTestAccess();
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var committed = false;
        var retired = 0;
        await using var session = new ProjectCatalogObservation(feed, access, scope, NullLogger<EditingObservation>.Instance);
        await session.StartAsync(async () => {
            read.TrySetResult();
            await release.Task;
            await session.AuthorizeReadAsync(default);
            committed = true;
        }, () => retired++, action => action());
        var pending = session.RefreshAsync();
        try {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(5));
            access.Principal = "replacement-principal";
        } finally {
            release.TrySetResult();
            await pending;
        }
        Assert.False(committed);
        Assert.True(session.IsRetired);
        Assert.Equal(1, retired);
        await session.RefreshAsync();
        Assert.False(committed);
    }
}
