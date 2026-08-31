using Bunit;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

internal sealed class ProviderHistoryUiFixture : IProviderRequestHistory {
    internal static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    internal List<ProviderRequestHistoryQuery> Queries { get; } = [];
    internal int MetadataReads { get; private set; }
    internal List<CanonicalEvidenceReference?> ContentReads { get; } = [];
    internal Func<ProviderRequestHistoryQuery, CancellationToken, Task<HistoryPage>>? Search { get; set; }
    internal Func<CancellationToken, Task<HistoryDetail>>? Content { get; set; }
    internal HistoryEntry Entry { get; set; } = new(
        HistoryEntryId.New(), new(Guid.NewGuid(), Guid.NewGuid(), "test"), ProviderRequestId.New(), ProviderAttemptId.New(),
        HistoryGranularity.ProviderCallAttempt, Now, HistoryTimeBasis.AttemptStarted, Now, Now,
        new(new ProviderIdentity(Guid.NewGuid()), "Test provider", "Test", new("Vendor/Model"), new("Vendor/Model")),
        HistoryOperation.CompleteChat, HistoryWorkload.SharedRelay, HistoryOutcome.Succeeded,
        new(HistoryAuthenticationKind.ManagedCredential, new(Guid.NewGuid()), "issuer", "client"),
        new(HistoryUsageState.Complete, 12, 8), new(HistoryPriceState.CalculatedAtExecution, 0.01m, "USD"),
        HistoryMetadataAuthority.Standalone, HistoryRetentionAuthority.HistoryPolicy, HistoryDetailState.Captured);
    internal IReadOnlyList<HistoryOwnerLink> Owners { get; set; } = [];

    internal BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(TimeProvider.System);
        context.Services.AddSingleton<IProviderRequestHistory>(this);
        context.Services.AddSingleton<IDatabaseSwitchNotificationService, DatabaseSwitchNotificationService>();
        return context;
    }

    public Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken cancellationToken) {
        Queries.Add(query);
        return Search?.Invoke(query, cancellationToken) ??
            Task.FromResult(new HistoryPage([Entry], null, new(HistoryCoverageState.Partial, Now), Now));
    }

    public Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entryId, CancellationToken cancellationToken) {
        MetadataReads++;
        return Task.FromResult<HistoryMetadata?>(new(Entry, Owners));
    }

    public Task<HistoryDetail> GetDetailAsync(HistoryEntryId entryId, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) {
        ContentReads.Add(owner);
        return Content?.Invoke(cancellationToken) ?? Task.FromResult(new HistoryDetail(entryId, HistoryDetailState.Captured,
            new("<script>untrusted()</script>", 28, 28, HistoryDetailFlags.PriorContextNotCaptured)));
    }
}
