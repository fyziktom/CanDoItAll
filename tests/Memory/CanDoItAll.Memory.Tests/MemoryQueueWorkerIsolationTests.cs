using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Runtime;

public sealed class MemoryQueueWorkerIsolationTests
{
    [Fact]
    public async Task Feedback_exception_does_not_starve_later_records()
    {
        using var root = MemoryWorkerIntegrityTestData.CreateServiceProvider();
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var failing = await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.a-feedback",
            MemoryProviderDriverKind.Mock,
            MemoryCapabilityIds.FeedbackDelayed);
        var succeeding = await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.b-feedback",
            MemoryProviderDriverKind.Http,
            MemoryCapabilityIds.FeedbackDelayed);
        var store = services.GetRequiredService<IMemoryFeedbackLedgerStore>();
        await store.SubmitAsync(MemoryWorkerIntegrityTestData.CreateFeedback(failing.InstanceId));
        await store.SubmitAsync(MemoryWorkerIntegrityTestData.CreateFeedback(succeeding.InstanceId));
        var worker = new MemoryFeedbackWorker(
            services.GetRequiredService<IMemoryProviderProfileStore>(),
            store,
            [new ThrowingFeedbackDriver(), new SuccessfulFeedbackDriver()],
            new MemoryWorkerIntegrityTestData.FixedTimeProvider(),
            MemoryWorkerIntegrityTestData.Options);

        var result = await worker.DeliverPendingFeedbackAsync();

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.Retried);
        AssertSafeFailure(result.Diagnostics);
    }

    [Fact]
    public async Task Outbox_exception_does_not_starve_later_records()
    {
        using var root = MemoryWorkerIntegrityTestData.CreateServiceProvider();
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var failing = await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.a-outbox",
            MemoryProviderDriverKind.Mock,
            MemoryCapabilityIds.EventsProviderPush);
        var succeeding = await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.b-outbox",
            MemoryProviderDriverKind.Http,
            MemoryCapabilityIds.EventsProviderPush);
        var store = services.GetRequiredService<IMemoryEventLedgerStore>();
        await store.EnqueueOutboxAsync(MemoryWorkerIntegrityTestData.CreateOutbox(failing.InstanceId));
        await store.EnqueueOutboxAsync(MemoryWorkerIntegrityTestData.CreateOutbox(succeeding.InstanceId));
        var processor = new MemoryProviderEventOutboxProcessor(
            services.GetRequiredService<IMemoryProviderProfileStore>(),
            store,
            [new ThrowingOutboxDriver(), new SuccessfulOutboxDriver()],
            new MemoryWorkerIntegrityTestData.FixedTimeProvider(),
            MemoryWorkerIntegrityTestData.Options);

        var result = await processor.DrainAsync(CancellationToken.None);

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.Retried);
        AssertSafeFailure(result.Diagnostics);
    }

    [Fact]
    public async Task Duplicate_feedback_drivers_fail_closed_without_dispatch()
    {
        using var root = MemoryWorkerIntegrityTestData.CreateServiceProvider();
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        var profile = await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.duplicate-feedback",
            MemoryProviderDriverKind.Mock,
            MemoryCapabilityIds.FeedbackDelayed);
        var store = services.GetRequiredService<IMemoryFeedbackLedgerStore>();
        await store.SubmitAsync(MemoryWorkerIntegrityTestData.CreateFeedback(profile.InstanceId));
        var first = new CountingFeedbackDriver();
        var second = new CountingFeedbackDriver();
        var worker = new MemoryFeedbackWorker(
            services.GetRequiredService<IMemoryProviderProfileStore>(),
            store,
            [first, second],
            new MemoryWorkerIntegrityTestData.FixedTimeProvider(),
            MemoryWorkerIntegrityTestData.Options);

        var result = await worker.DeliverPendingFeedbackAsync();

        Assert.Equal(0, result.Completed);
        Assert.Equal(1, result.Retried);
        Assert.Equal(0, first.DispatchCount);
        Assert.Equal(0, second.DispatchCount);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Contains("Multiple", StringComparison.Ordinal));
    }

    private static void AssertSafeFailure(IReadOnlyList<string> diagnostics)
    {
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, diagnostic =>
            diagnostic.Contains("provider-secret-detail", StringComparison.Ordinal));
    }

    private sealed class ThrowingFeedbackDriver : IMemoryProviderFeedbackDeliveryDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
            MemoryProviderProfile provider,
            MemoryFeedbackRecord feedback,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider-secret-detail");
    }

    private sealed class SuccessfulFeedbackDriver : IMemoryProviderFeedbackDeliveryDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Http;

        public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
            MemoryProviderProfile provider,
            MemoryFeedbackRecord feedback,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded("delivered"));
    }

    private sealed class CountingFeedbackDriver : IMemoryProviderFeedbackDeliveryDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public int DispatchCount { get; private set; }

        public Task<MemoryProviderQueueDispatchResult> DeliverFeedbackAsync(
            MemoryProviderProfile provider,
            MemoryFeedbackRecord feedback,
            CancellationToken cancellationToken = default)
        {
            DispatchCount++;
            return Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded("delivered"));
        }
    }

    private sealed class ThrowingOutboxDriver : IMemoryProviderEventOutboxDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
            MemoryProviderProfile provider,
            MemoryEventOutboxRecord outbox,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider-secret-detail");
    }

    private sealed class SuccessfulOutboxDriver : IMemoryProviderEventOutboxDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Http;

        public Task<MemoryProviderQueueDispatchResult> DeliverOutboxAsync(
            MemoryProviderProfile provider,
            MemoryEventOutboxRecord outbox,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MemoryProviderQueueDispatchResult.Succeeded("delivered"));
    }
}
