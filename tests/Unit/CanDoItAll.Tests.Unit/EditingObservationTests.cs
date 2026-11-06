using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Editing;

public sealed class EditingObservationTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Invalidation_during_authorization_cannot_return_a_read_or_write_admission(bool write, bool stop) {
        var feed = new EditingChangeFeed();
        var access = new Access();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observation = new EditingObservation(feed, access, Topic(), NullLogger<EditingObservation>.Instance);
        await observation.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);
        access.BeforeValidation = _ => {
            entered.TrySetResult();
            return new(release.Task);
        };
        var authority = observation.AuthorizeAsync(write, default).AsTask();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (stop) {
                observation.Stop();
            } else {
                access.Grant!.Revoke();
            }
        } finally {
            release.TrySetResult();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authority);
    }

    [Fact]
    public async Task Stop_releases_the_subscription_and_refuses_later_reads_or_writes_before_disposal() {
        var feed = new EditingChangeFeed();
        var access = new Access();
        var topic = Topic();
        int delivered = 0;
        await using var observation = new EditingObservation(feed, access, topic, NullLogger<EditingObservation>.Instance);
        await observation.StartAsync(_ => {
            delivered++;
            return Task.CompletedTask;
        }, () => Task.CompletedTask);

        observation.Stop();

        Assert.True(observation.Lifetime.IsCancellationRequested);
        Assert.Equal(0, feed.ActiveSubscriptions);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => observation.AuthorizeAsync(false, default).AsTask());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => observation.AuthorizeAsync(true, default).AsTask());
        feed.Publish(new(topic.Scope, topic.Owner, topic.ResourceId!.Value), EditingChangeKind.EditableContentChanged);
        Assert.Equal(0, delivered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalidation_during_subscription_acquisition_cannot_leave_a_new_subscription_registered(bool stop) {
        var feed = new EditingChangeFeed();
        var access = new Access();
        EditingObservation? observation = null;
        var observer = new StopOnSubscribe(feed, () => {
            if (stop) {
                observation!.Stop();
            } else {
                access.Grant!.Revoke();
            }
        });
        await using var owned = new EditingObservation(observer, access, Topic(), NullLogger<EditingObservation>.Instance);
        observation = owned;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owned.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask));

        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.True(access.Grant!.IsDisposed);
    }

    [Fact]
    public async Task Disposal_does_not_wait_for_a_retirement_effect_queued_on_the_disposing_UI_dispatcher() {
        var feed = new EditingChangeFeed(TimeProvider.System);
        var access = new Access();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observation = new EditingObservation(feed, access, Topic(), NullLogger<EditingObservation>.Instance);
        await observation.StartAsync(_ => Task.CompletedTask, () => {
            entered.TrySetResult();
            return release.Task;
        });
        access.Grant!.Revoke();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await observation.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        } finally {
            release.TrySetResult();
        }
    }

    [Fact]
    public async Task Scope_disposal_cancels_observation_without_emitting_an_access_loss_effect_into_the_retired_host() {
        var feed = new EditingChangeFeed(TimeProvider.System);
        var access = new Access();
        var retired = 0;
        await using var observation = new EditingObservation(feed, access, Topic(), NullLogger<EditingObservation>.Instance);
        await observation.StartAsync(_ => Task.CompletedTask, () => {
            retired++;
            return Task.CompletedTask;
        });
        var token = access.Grant!.Lifetime;

        access.Grant.Dispose();
        await observation.DisposeAsync();

        Assert.True(access.Grant.IsDisposed);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(0, retired);
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Revocation_retires_the_live_observation_and_releases_its_subscription() {
        var feed = new EditingChangeFeed(TimeProvider.System);
        var access = new Access();
        var retired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var observation = new EditingObservation(feed, access, Topic(), NullLogger<EditingObservation>.Instance);
        await observation.StartAsync(_ => Task.CompletedTask, () => {
            retired.TrySetResult();
            return Task.CompletedTask;
        });

        access.Grant!.Revoke();
        await retired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(access.Grant.IsDisposed);
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Failed_initial_validation_releases_the_opened_grant_and_never_subscribes() {
        var feed = new EditingChangeFeed(TimeProvider.System);
        var access = new Access { Denied = true };
        await using var observation = new EditingObservation(feed, access, Topic(), NullLogger<EditingObservation>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => observation.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask));

        Assert.True(access.Grant!.IsDisposed);
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    private static EditingTopic Topic() => new(new(EditingScopeKind.Dataset, Guid.NewGuid()), EditingOwner.CrmHrParty, Guid.NewGuid());
    private sealed class StopOnSubscribe(EditingChangeFeed feed, Action stop) : IEditingChangeObserver {
        public IEditingChangeSubscription Subscribe(EditingTopic topic) {
            var subscription = feed.Subscribe(topic);
            stop();
            return subscription;
        }
    }

    private sealed class Access : IEditingUiAccess {
        public EditingUiGrant? Grant { get; private set; }
        public bool Denied { get; init; }
        public Func<CancellationToken, ValueTask>? BeforeValidation { get; set; }
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) =>
            Task.FromResult(Grant = new(topic, "native-test-principal", "test-actor"));
        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) => Denied
            ? ValueTask.FromException(new UnauthorizedAccessException("Test admission denied."))
            : BeforeValidation?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;
    }
}
