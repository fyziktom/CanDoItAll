using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed partial class LlmChatTurnTransactionIntegrationTests {
    public enum PriceReplay { Fresh, HistoricalAggregate, HistoricalAttempts }

    [Theory]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.ProviderReported, PriceReplay.Fresh)]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution, PriceReplay.Fresh)]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.ProviderReported, PriceReplay.HistoricalAggregate)]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution, PriceReplay.HistoricalAggregate)]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.ProviderReported, PriceReplay.HistoricalAttempts)]
    [InlineData(LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution, PriceReplay.HistoricalAttempts)]
    public async Task Priced_chat_history_survives_owner_round_trip_and_replays_exact_historical_evidence(
        LlmChatInvocationPricingEvidenceStatus pricing, PriceReplay replay) {
        await using var history = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var readContext = history.HistoryFactory.CreateDbContext();
        var ownerOptions = new DbContextOptionsBuilder<SimpleChatsDbContext>()
            .UseNpgsql(readContext.Database.GetConnectionString()).Options;
        await using var owner = new SimpleChatsDbContext(ownerOptions);
        var services = LlmChatTestPersistence.CreateHistoryServices(owner, history.Clock);
        var seeded = await SeedConversationAsync(owner, admitted: true);
        var source = new CanonicalEvidenceReference(history.Partition, HistorySourceKind.SimpleChat,
            new(seeded.TurnId.ToString("N")), new("1"));
        var attempt = history.Start() with { ContentOwner = source, Workload = HistoryWorkload.SimpleChat };
        const decimal amount = 0.000123m;
        var record = new LlmChatInvocationRecord(new(seeded.TurnId), seeded.Document.Provider.ProviderId,
            ProviderKind.OpenAi, "Synthetic price fixture", "model", null, null, 1, new(11, 7, 2),
            LlmChatInvocationOutcome.Succeeded, string.Empty, seeded.Now, seeded.Now.AddSeconds(1), "synthetic-price-replay",
            pricingStatus: pricing,
            providerCostUsd: pricing == LlmChatInvocationPricingEvidenceStatus.ProviderReported ? amount : null,
            calculatedCostUsd: pricing == LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution ? amount : null,
            pricingProfileHash: new string('a', ProviderPricingSnapshot.ProfileHashLength), pricingVersion: ProviderPricingSnapshot.Version) {
                HistoryAttempts = replay == PriceReplay.HistoricalAttempts
                    ? [HistoryAttemptEvidence.Create(attempt, history.Completion())] : []
            };
        var invocations = new EfLlmChatInvocationRecordRepository(owner, services.Projection);
        var unitOfWork = new EfLlmChatUnitOfWork(owner, UnfencedLlmChatCommitFence.Instance, services.Transactions);
        await unitOfWork.ExecuteAsync(async token => {
            await invocations.AppendAsync(record, token);
            return true;
        });
        var outbox = await readContext.Set<HistoryOutboxRow>().AsNoTracking().ToArrayAsync();
        var queued = Assert.Single(outbox, row => row.Mutation.Source == source).Mutation;
        var adapter = new LlmChatHistorySource(new LlmChatOwnerOptionsFactory(ownerOptions), services.Partitions,
            services.Outbox, services.Transactions);
        var restored = Assert.IsType<HistorySourceMutation>(await adapter.ReadAsync(source, default));
        Assert.Equal(queued.Entry!.Price, restored.Entry!.Price);
        Assert.Equal(HistoryMutationHash(queued), HistoryMutationHash(restored with {
            Entry = restored.Entry with { Price = restored.Entry.Price with { Amount = queued.Entry.Price.Amount } }
        }));
        Assert.Equal(HistoryMutationHash(queued), HistoryMutationHash(restored));
        var historical = replay == PriceReplay.Fresh ? queued : queued with {
            Entry = queued.Entry with { Price = queued.Entry.Price with { Amount = 0.00012300m } }
        };
        await history.Projection.ApplyAsync(historical, default);
        foreach (var other in outbox.Where(row => row.Mutation.Source != source)) {
            await history.Projection.ApplyAsync(other.Mutation, default);
        }
        var historicalEntry = JsonSerializer.Serialize(await readContext.Set<HistoryEntryRow>().AsNoTracking().SingleAsync());
        var historicalSource = JsonSerializer.Serialize(await readContext.Set<HistorySourceRow>().AsNoTracking()
            .SingleAsync(row => row.EvidenceId == source.Evidence.Value));
        var expectedQueued = replay == PriceReplay.HistoricalAttempts ? 2 : 1;
        Assert.Equal(expectedQueued, await history.Processor.ProcessAsync(history.Partition, 10, default));
        Assert.Equal(historicalEntry, JsonSerializer.Serialize(await readContext.Set<HistoryEntryRow>().AsNoTracking().SingleAsync()));
        Assert.Equal(historicalSource, JsonSerializer.Serialize(await readContext.Set<HistorySourceRow>().AsNoTracking()
            .SingleAsync(row => row.EvidenceId == source.Evidence.Value)));
        var before = await ReadHistoryPriceRowsAsync(readContext);
        var progress = await adapter.ProcessAsync(history.Maintenance, null, 10, default);
        Assert.True(progress.BackfillComplete);
        Assert.Equal(1, await history.Processor.ProcessAsync(history.Partition, 10, default));
        Assert.Empty(await readContext.Set<HistoryOutboxRow>().AsNoTracking().ToArrayAsync());
        Assert.Equal(HistoryMutationHash(historical), (await readContext.Set<HistorySourceRow>().AsNoTracking()
            .SingleAsync(row => row.EvidenceId == source.Evidence.Value)).MutationHash);
        var expectedPrice = replay == PriceReplay.HistoricalAttempts ? Assert.Single(record.HistoryAttempts).Price : historical.Entry!.Price;
        var entry = Assert.Single(await readContext.Set<HistoryEntryRow>().AsNoTracking().ToArrayAsync());
        Assert.Equal(expectedPrice.Amount, entry.Amount);
        Assert.Equal(replay == PriceReplay.HistoricalAttempts ? 2 : 1,
            await readContext.Set<HistoryOwnerRow>().CountAsync());
        Assert.Equal(before, await ReadHistoryPriceRowsAsync(readContext));
        var replayed = await ReadHistoryPriceRowsAsync(readContext);
        HistoryEntry[] changedEvidence = [
            restored.Entry with { Price = restored.Entry.Price with { Amount = amount + 0.000001m } },
            restored.Entry with { Price = restored.Entry.Price with { Currency = "EUR" } },
            restored.Entry with { Price = restored.Entry.Price with { ProfileHash = new string('b', ProviderPricingSnapshot.ProfileHashLength) } },
            restored.Entry with { Price = restored.Entry.Price with { Version = "different-version" } },
            restored.Entry with { Usage = restored.Entry.Usage with { InputTokens = 12 } },
            restored.Entry with { Outcome = HistoryOutcome.Failed }
        ];
        foreach (var changed in changedEvidence) {
            var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() => history.Projection.ApplyAsync(
                restored with { Entry = changed }, default));
            Assert.Equal(HistoryFailure.Conflict, failure.Failure);
        }
        if (replay == PriceReplay.HistoricalAttempts) {
            var originalAttempt = Assert.Single(restored.Attempts);
            HistoryEntry[] changedAttempts = [
                originalAttempt with { Price = originalAttempt.Price with { Amount = 0.02m } },
                originalAttempt with { Usage = originalAttempt.Usage with { InputTokens = 12 } }
            ];
            foreach (var changed in changedAttempts) {
                var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() => history.Projection.ApplyAsync(
                    restored with { Attempts = [changed] }, default));
                Assert.Equal(HistoryFailure.Conflict, failure.Failure);
            }
        }
        Assert.Equal(replayed, await ReadHistoryPriceRowsAsync(readContext));
    }

    private static string HistoryMutationHash(HistorySourceMutation mutation)
        => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(mutation)));

    private static async Task<string> ReadHistoryPriceRowsAsync(ProviderHistoryDbContext db) => JsonSerializer.Serialize(new {
        Entries = await db.Set<HistoryEntryRow>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
        Sources = await db.Set<HistorySourceRow>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
        Owners = await db.Set<HistoryOwnerRow>().AsNoTracking().OrderBy(row => row.SourceId).ThenBy(row => row.EntryId).ToArrayAsync()
    });
}
