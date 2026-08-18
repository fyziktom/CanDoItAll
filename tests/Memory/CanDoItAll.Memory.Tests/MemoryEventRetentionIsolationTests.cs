using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Memory.Tests.Persistence;

public sealed class MemoryEventRetentionIsolationTests
{
    [Fact]
    public async Task Event_poll_exception_does_not_starve_later_providers()
    {
        using var root = MemoryWorkerIntegrityTestData.CreateServiceProvider(services =>
        {
            services.RemoveAll<IMemoryProviderEventPollDriver>();
            services.AddSingleton<IMemoryProviderEventPollDriver, ThrowingEventDriver>();
            services.AddSingleton<IMemoryProviderEventPollDriver, SuccessfulEventDriver>();
        });
        using var scope = root.CreateScope();
        var services = scope.ServiceProvider;
        await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.a-events",
            MemoryProviderDriverKind.Mock,
            MemoryCapabilityIds.EventsHostPoll);
        await MemoryWorkerIntegrityTestData.SeedProfileAsync(
            services,
            "provider.b-events",
            MemoryProviderDriverKind.Http,
            MemoryCapabilityIds.EventsHostPoll);
        var worker = services.GetRequiredService<IMemoryProviderEventWorker>();

        var result = await worker.PollProviderEventsAsync();

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Retried);
        Assert.Equal(1, result.Enqueued);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Contains("provider-secret-detail", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Retention_exception_does_not_starve_later_records()
    {
        var store = new ScriptedRetentionStore();
        var worker = new MemoryRetentionWorker(
            store,
            new MemoryWorkerIntegrityTestData.FixedTimeProvider(),
            MemoryWorkerIntegrityTestData.Options);

        var result = await worker.ApplyDueRetentionAsync();

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Completed);
        Assert.Equal(1, result.Retried);
        Assert.Equal(["record-b"], store.AppliedRecordIds);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(result.Diagnostics, diagnostic =>
            diagnostic.Contains("database-secret-detail", StringComparison.Ordinal));
    }

    private sealed class ThrowingEventDriver : IMemoryProviderEventPollDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderEventPollResult> PollEventsAsync(
            MemoryProviderProfile provider,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider-secret-detail");
    }

    private sealed class SuccessfulEventDriver : IMemoryProviderEventPollDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Http;

        public Task<MemoryProviderEventPollResult> PollEventsAsync(
            MemoryProviderProfile provider,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MemoryProviderEventPollResult.FromEvents(
                [MemoryWorkerIntegrityTestData.CreateEvent("maintenance")],
                "event returned"));
    }

    private sealed class ScriptedRetentionStore : IMemoryRetentionProjectionStore
    {
        private readonly IReadOnlyList<MemoryRetentionCandidate> candidates =
        [
            new("operations", "record-a", MemoryLedgerRetentionDecision.Expire, MemoryWorkerIntegrityTestData.Now),
            new("operations", "record-b", MemoryLedgerRetentionDecision.Expire, MemoryWorkerIntegrityTestData.Now)
        ];

        public List<string> AppliedRecordIds { get; } = [];

        public Task<IReadOnlyList<MemoryRetentionCandidate>> ListDueAsync(
            DateTimeOffset nowUtc,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(candidates);

        public Task<MemoryRetentionApplicationResult> ApplyAsync(
            MemoryRetentionCandidate candidate,
            DateTimeOffset appliedAtUtc,
            string reason,
            CancellationToken cancellationToken = default)
        {
            if (candidate.RecordId == "record-a")
            {
                throw new InvalidOperationException("database-secret-detail");
            }

            AppliedRecordIds.Add(candidate.RecordId);
            return Task.FromResult(new MemoryRetentionApplicationResult(
                candidate,
                MemoryLedgerStatus.Expired,
                IpfsUnpinRequested: false));
        }
    }
}
