using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.UiSandbox;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal enum HistoryBrowserMode { Normal, HoldSearch, FailSearch, HoldMetadata, HoldContent, Denied }
internal sealed class HistoryBrowserState {
    public HistoryBrowserMode Mode { get; set; }
    public string Model { get; } = "History/Fixture-" + Guid.NewGuid().ToString("N");
    public int Searches;
    public int MetadataReads;
    public int ContentReads;
    public bool Ready { get; set; }
}

internal sealed class HistoryBrowserReads(IProviderRequestHistory canonical, GovernanceBrowserState fixture) : IProviderRequestHistory {
    public const string Forbidden = "history-browser-private-sentinel";
    public async Task<HistoryPage> SearchAsync(ProviderRequestHistoryQuery query, CancellationToken token) {
        Interlocked.Increment(ref fixture.History.Searches);
        var mode = fixture.History.Mode;
        var result = await canonical.SearchAsync(query, token);
        if (mode == HistoryBrowserMode.FailSearch) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable, Forbidden);
        }
        if (mode == HistoryBrowserMode.HoldSearch) {
            await fixture.HoldAsync(token);
        }
        return result;
    }
    public async Task<HistoryMetadata?> GetMetadataAsync(HistoryEntryId entry, CancellationToken token) {
        Interlocked.Increment(ref fixture.History.MetadataReads);
        var mode = fixture.History.Mode;
        var result = await canonical.GetMetadataAsync(entry, token);
        if (mode == HistoryBrowserMode.HoldMetadata) {
            await fixture.HoldAsync(token);
        }
        return result;
    }
    public async Task<HistoryDetail> GetDetailAsync(HistoryEntryId entry, CanonicalEvidenceReference? owner, CancellationToken token) {
        Interlocked.Increment(ref fixture.History.ContentReads);
        var mode = fixture.History.Mode;
        var result = await canonical.GetDetailAsync(entry, owner, token);
        if (mode == HistoryBrowserMode.Denied) {
            throw new ProviderHistoryException(HistoryFailure.Denied, Forbidden);
        }
        if (mode == HistoryBrowserMode.HoldContent) {
            await fixture.HoldAsync(token);
        }
        return result;
    }

    public static async Task SeedAsync(IServiceProvider services, HistoryBrowserState state) {
        await using var scope = services.CreateAsyncScope();
        var provider = (await scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>().ListProvidersAsync())
            .Single(value => value.Name == "Governance rendering provider");
        var partition = await scope.ServiceProvider.GetRequiredService<IProviderHistoryPartition>().GetAsync(default);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        HistoryCaptureMode original;
        await using (var db = await factory.CreateDbContextAsync()) {
            var policy = await db.Set<HistoryPolicyRow>().SingleAsync(value => value.PartitionId == partition.StorageLineageId);
            original = policy.CaptureMode;
            policy.CaptureMode = HistoryCaptureMode.Detailed;
            await db.SaveChangesAsync();
        }
        try {
            var recorder = scope.ServiceProvider.GetRequiredService<IProviderHistoryRecorder>();
            for (var index = 0; index < 15; index++) {
                var context = HistoryInvocationContext.Create(caller: new(HistoryAuthenticationKind.TrustedLocalOperator),
                    currentTurn: new(HistorySandboxFixture.SyntheticInput + " {\"api_key\":\"" + Forbidden + "\"}", 0));
                var start = await recorder.BeginAsync(new(new(new(provider.Id), provider.Name, provider.Kind.ToString(),
                    new(state.Model), new(state.Model)), HistoryOperation.CompleteChat, context), default);
                await recorder.CompleteAsync(start, new(HistoryOutcome.Succeeded, start.StartedAtUtc.AddMilliseconds(1),
                    new(HistoryUsageState.Complete, 12, 8), new(HistoryPriceState.CalculatedAtExecution, 0.01m, "USD")),
                    HistorySandboxFixture.SyntheticResponse, default);
            }
        } finally {
            await using var db = await factory.CreateDbContextAsync();
            var policy = await db.Set<HistoryPolicyRow>().SingleAsync(value => value.PartitionId == partition.StorageLineageId);
            policy.CaptureMode = original;
            await db.SaveChangesAsync();
        }
        state.Ready = true;
    }
}
