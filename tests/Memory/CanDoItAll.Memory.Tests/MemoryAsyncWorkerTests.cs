using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryAsyncWorkerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T12:00:00Z");
    private static readonly MemoryProviderInstanceId ProviderId = MemoryProviderInstanceId.Parse("provider.mock");

    [Fact]
    public async Task OP001_Accepted_operation_polls_provider_and_completes()
    {
        var time = new ManualMemoryTimeProvider(Now);
        var statusDriver = ScriptedOperationStatusDriver.FromResult(MemoryOperationStatus.Succeeded);
        using var rootProvider = CreateServiceProvider(time, statusDriver: statusDriver);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var operation = await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));

        var worker = provider.GetRequiredService<IMemoryAsyncOperationWorker>();
        using var cancellation = new CancellationTokenSource();
        var result = await worker.PollOperationsAsync(cancellation.Token);
        var persisted = await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(operation.OperationId);

        Assert.Equal(1, result.Scanned);
        Assert.Equal(1, result.Completed);
        Assert.Equal(MemoryLedgerStatus.Completed, persisted?.Status);
        Assert.Equal(1, statusDriver.Calls);
        Assert.All(statusDriver.ObservedTokens, token => Assert.True(token.CanBeCanceled));
    }

    [Fact]
    public async Task OP002_Operation_worker_times_out_expired_async_operation_without_driver_call()
    {
        var time = new ManualMemoryTimeProvider(Now);
        var statusDriver = ScriptedOperationStatusDriver.FromResult(MemoryOperationStatus.Succeeded);
        using var rootProvider = CreateServiceProvider(time, statusDriver: statusDriver);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var operation = await SeedAcceptedOperationAsync(provider, Now.AddMinutes(-5));

        var result = await provider.GetRequiredService<IMemoryAsyncOperationWorker>().PollOperationsAsync();
        var persisted = await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(operation.OperationId);

        Assert.Equal(1, result.TimedOut);
        Assert.Equal(MemoryLedgerStatus.TimedOut, persisted?.Status);
        Assert.Equal(0, statusDriver.Calls);
    }

    [Fact]
    public async Task OP003_Operation_worker_cancels_running_operation_explicitly()
    {
        var time = new ManualMemoryTimeProvider(Now);
        using var rootProvider = CreateServiceProvider(time);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var operation = await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));
        await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .TransitionAsync(operation.OperationId, MemoryLedgerStatus.Running, Now.AddSeconds(-5), "provider running");

        var cancelled = await provider.GetRequiredService<IMemoryAsyncOperationWorker>()
            .CancelOperationAsync(operation.OperationId, "user cancelled operation");

        Assert.Equal(MemoryLedgerStatus.Cancelled, cancelled.Status);
        Assert.Equal("user cancelled operation", cancelled.StatusReason);
    }

    [Fact]
    public async Task OP004_Operation_worker_dead_letters_after_retry_budget()
    {
        var time = new ManualMemoryTimeProvider(Now);
        var statusDriver = ScriptedOperationStatusDriver.FromRetryableFailure("provider temporarily unavailable");
        using var rootProvider = CreateServiceProvider(time, statusDriver: statusDriver, maxRetryAttempts: 2);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var operation = await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));
        var worker = provider.GetRequiredService<IMemoryAsyncOperationWorker>();

        var first = await worker.PollOperationsAsync();
        var second = await worker.PollOperationsAsync();
        var persisted = await provider.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(operation.OperationId);

        Assert.Equal(1, first.Retried);
        Assert.Equal(1, second.DeadLettered);
        Assert.Equal(MemoryLedgerStatus.Failed, persisted?.Status);
        Assert.Equal(2, statusDriver.Calls);
    }

    [Fact]
    public async Task RT001_Retention_worker_forgets_feedback_and_requests_ipfs_unpin()
    {
        var time = new ManualMemoryTimeProvider(Now);
        using var rootProvider = CreateServiceProvider(time);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var feedback = CreateFeedbackRecord(
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(-2), Now.AddDays(-1)),
            new MemoryIpfsSnapshotMetadata("ipfs://bafy-feedback", MemoryIpfsPinState.Pinned, Now.AddDays(-3), null, null));
        await provider.GetRequiredService<IMemoryFeedbackLedgerStore>().SubmitAsync(feedback);

        var result = await provider.GetRequiredService<IMemoryRetentionWorker>().ApplyDueRetentionAsync();
        var persisted = Assert.Single(await provider.GetRequiredService<IMemoryFeedbackLedgerStore>()
            .ListByProviderAsync(ProviderId));

        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.IpfsUnpinRequests);
        Assert.Equal(MemoryLedgerStatus.Forgotten, persisted.Status);
        Assert.Equal(MemoryIpfsPinState.UnpinRequested, persisted.IpfsSnapshot?.PinState);
    }

    [Fact]
    public async Task EV001_Event_worker_dedupes_polled_events_and_drains_inbox_outbox()
    {
        var time = new ManualMemoryTimeProvider(Now);
        var providerEvent = CreateProviderEvent();
        var eventDriver = new ScriptedEventPollDriver([providerEvent, providerEvent]);
        var outboxDriver = new RecordingOutboxDriver(MemoryProviderQueueDispatchResult.Succeeded("ack delivered"));
        using var rootProvider = CreateServiceProvider(time, eventDriver: eventDriver, outboxDriver: outboxDriver, maxBatchSize: 2);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await SeedProviderProfileAsync(provider);

        var worker = provider.GetRequiredService<IMemoryProviderEventWorker>();
        var poll = await worker.PollProviderEventsAsync();
        var drainInbox = await worker.DrainInboxAsync();
        var drainOutbox = await worker.DrainOutboxAsync();
        var inbox = await provider.GetRequiredService<IMemoryEventLedgerStore>()
            .ListPendingInboxAsync(ProviderId);

        Assert.Equal(1, poll.Enqueued);
        Assert.Equal(1, poll.Duplicates);
        Assert.Equal(1, drainInbox.Completed);
        Assert.Equal(1, drainInbox.Enqueued);
        Assert.Equal(1, drainOutbox.Completed);
        Assert.Empty(inbox);
        Assert.Equal(1, outboxDriver.Calls);
    }

    [Fact]
    public async Task OP005_Operation_worker_respects_bounded_batch_size()
    {
        var time = new ManualMemoryTimeProvider(Now);
        var statusDriver = ScriptedOperationStatusDriver.FromResult(MemoryOperationStatus.Succeeded);
        using var rootProvider = CreateServiceProvider(time, statusDriver: statusDriver, maxBatchSize: 2);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));
        await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));
        await SeedAcceptedOperationAsync(provider, Now.AddSeconds(-10));

        var result = await provider.GetRequiredService<IMemoryAsyncOperationWorker>().PollOperationsAsync();

        Assert.Equal(2, result.Scanned);
        Assert.Equal(2, statusDriver.Calls);
    }

    private static ServiceProvider CreateServiceProvider(
        TimeProvider timeProvider,
        IMemoryProviderOperationStatusDriver? statusDriver = null,
        IMemoryProviderEventPollDriver? eventDriver = null,
        IMemoryProviderEventOutboxDriver? outboxDriver = null,
        int maxBatchSize = 25,
        int maxRetryAttempts = 3)
    {
        var services = new ServiceCollection();
        services.AddSingleton(timeProvider);
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-workers-{Guid.NewGuid():N}"));
        if (statusDriver is not null)
        {
            services.AddSingleton(statusDriver);
        }

        if (eventDriver is not null)
        {
            services.AddSingleton(eventDriver);
        }

        if (outboxDriver is not null)
        {
            services.AddSingleton(outboxDriver);
        }

        services.AddGenericMemoryModule(options =>
        {
            options.WorkerOptions = MemoryAsyncWorkerOptions.Default with
            {
                MaxBatchSize = maxBatchSize,
                MaxRetryAttempts = maxRetryAttempts,
                PollingStaleAfter = TimeSpan.Zero
            };
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task<MemoryOperationRecord> SeedAcceptedOperationAsync(
        IServiceProvider provider,
        DateTimeOffset createdAtUtc)
    {
        await SeedProviderProfileAsync(provider);
        var operation = CreateOperationRecord(createdAtUtc);
        var store = provider.GetRequiredService<IMemoryOperationLedgerStore>();
        await store.CreateAsync(operation);
        return await store.TransitionAsync(operation.OperationId, MemoryLedgerStatus.Accepted, createdAtUtc, "provider accepted");
    }

    private static async Task SeedProviderProfileAsync(IServiceProvider provider)
    {
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(), Now);
    }

    private static MemoryProviderProfile CreateProviderProfile()
    {
        return new MemoryProviderProfile(
            ProviderId,
            DisplayName: "Mock memory provider",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["test"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "1", Supported: true),
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.OperationStatus, "1", Supported: true),
                    new MemoryCapabilityDescriptor(MemoryCapabilityIds.EventsHostPoll, "1", Supported: true)
                ],
                new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: false,
                    SupportsAsynchronousOperations: true,
                    SupportsSourceRequests: false,
                    SupportsFeedback: true,
                    SupportsProviderEvents: true),
                UiSurfaces: [],
                new MemoryProviderLimits(12, 100, 4, TimeSpan.FromMinutes(1)),
                MemoryExtensionData.Empty));
    }

    private static MemoryOperationRecord CreateOperationRecord(DateTimeOffset createdAtUtc)
    {
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            ProviderId,
            MemoryCapabilityIds.ContextQueryAsync,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30)),
            createdAtUtc);
    }

    private static MemoryFeedbackRecord CreateFeedbackRecord(
        MemoryLedgerRetentionPolicy retention,
        MemoryIpfsSnapshotMetadata ipfs)
    {
        return MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            ProviderId,
            MemoryFeedbackStage.ContextUsed,
            MemoryFeedbackOutcome.Useful,
            CreateRequester(),
            unmatchedReason: "feedback submitted without delivery correlation",
            retention,
            Now.AddDays(-3),
            ipfsSnapshot: ipfs);
    }

    private static MemoryProviderEvent CreateProviderEvent()
    {
        return new MemoryProviderEvent(
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.VerificationRequest,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            "verify memory assertion",
            MemoryPayload.FromText("verify"));
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-7",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private sealed class ManualMemoryTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ScriptedOperationStatusDriver(
        Func<MemoryOperationRecord, MemoryProviderOperationPollResult> next) : IMemoryProviderOperationStatusDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public int Calls { get; private set; }

        public List<CancellationToken> ObservedTokens { get; } = [];

        public static ScriptedOperationStatusDriver FromResult(MemoryOperationStatus status)
        {
            return new ScriptedOperationStatusDriver(operation =>
                MemoryProviderOperationPollResult.FromResult(
                    new MemoryOperationResult(operation.OperationId, status, MemoryPayload.FromText("done"), [], [], []),
                    $"provider returned '{status}'"));
        }

        public static ScriptedOperationStatusDriver FromRetryableFailure(string diagnostic)
        {
            return new ScriptedOperationStatusDriver(_ =>
                MemoryProviderOperationPollResult.RetryableFailure(diagnostic));
        }

        public Task<MemoryProviderOperationPollResult> PollOperationAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            ObservedTokens.Add(cancellationToken);
            return Task.FromResult(next(operation));
        }
    }

    private sealed class ScriptedEventPollDriver(
        IReadOnlyList<MemoryProviderEvent> events) : IMemoryProviderEventPollDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderEventPollResult> PollEventsAsync(
            MemoryProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryProviderEventPollResult.FromEvents(events, "events returned"));
        }
    }

    private sealed class RecordingOutboxDriver(
        MemoryProviderQueueDispatchResult result) : IMemoryProviderEventOutboxDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public int Calls { get; private set; }

        public Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
            MemoryProviderProfile provider,
            MemoryEventOutboxRecord outbox,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }
}
