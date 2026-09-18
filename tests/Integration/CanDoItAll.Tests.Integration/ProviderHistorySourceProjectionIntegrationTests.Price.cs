using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProviderHistorySourceProjectionIntegrationTests {
    public enum DecimalPriceBoundary { Zero, MinimumPositive, Maximum }

    [Theory]
    [InlineData(DecimalPriceBoundary.Zero)]
    [InlineData(DecimalPriceBoundary.MinimumPositive)]
    [InlineData(DecimalPriceBoundary.Maximum)]
    public async Task Historical_price_scale_matching_preserves_decimal_boundaries_and_rejects_changed_evidence(DecimalPriceBoundary boundary) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var amount = boundary switch {
            DecimalPriceBoundary.Zero => 0.0000000000000000000000000000m,
            DecimalPriceBoundary.MinimumPositive => 0.0000000000000000000000000001m,
            DecimalPriceBoundary.Maximum => decimal.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(boundary))
        };
        var mutation = Mutation(fixture);
        mutation = mutation with { Entry = mutation.Entry! with {
            Price = new(HistoryPriceState.ProviderReported, amount, "USD", "synthetic-price-hash", "synthetic-version")
        } };
        await fixture.Projection.ApplyAsync(mutation, default);
        await using var read = fixture.HistoryFactory.CreateDbContext();
        var entryBefore = JsonSerializer.Serialize(await read.Set<HistoryEntryRow>().AsNoTracking().SingleAsync());
        var sourceBefore = JsonSerializer.Serialize(await read.Set<HistorySourceRow>().AsNoTracking().SingleAsync());
        var replay = mutation with { Entry = mutation.Entry with {
            Price = mutation.Entry.Price with { Amount = boundary == DecimalPriceBoundary.Zero ? 0m : amount }
        } };
        await fixture.Projection.ApplyAsync(replay, default);
        var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Projection.ApplyAsync(
            replay with { Entry = replay.Entry with { Outcome = HistoryOutcome.Failed } }, default));
        Assert.Equal(HistoryFailure.Conflict, failure.Failure);
        Assert.Equal(entryBefore, JsonSerializer.Serialize(await read.Set<HistoryEntryRow>().AsNoTracking().SingleAsync()));
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(await read.Set<HistorySourceRow>().AsNoTracking().SingleAsync()));
    }
}
