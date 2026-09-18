using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.LlmChats;

internal static class LlmChatTestPersistence {
    private static readonly ConditionalWeakTable<SimpleChatsDbContext, CoordinatedDatabaseTransaction> Transactions = new();

    internal static CoordinatedDatabaseTransaction TransactionsFor(SimpleChatsDbContext owner)
        => Transactions.GetValue(owner, static context => ForPostgreSql(context.Database.GetConnectionString()!));

    internal static CoordinatedDatabaseTransaction ForPostgreSql(string connectionString)
        => CoordinatedDatabaseTransaction.ForProfile(new ResolvedDatabaseProfile(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.PostgreSql },
            DatabaseProfileResolutionSource.ExplicitOverride,
            connectionString));

    internal static SimpleChatsDbContext CreateInMemoryContext(string databaseName) {
        var context = new SimpleChatsDbContext(new DbContextOptionsBuilder<SimpleChatsDbContext>()
            .UseInMemoryDatabase(databaseName).Options);
        Transactions.Add(context, CoordinatedDatabaseTransaction.ForProfile(new ResolvedDatabaseProfile(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride,
            databaseName)));
        return context;
    }

    internal static LlmChatHistoryProjection CreateProjection(SimpleChatsDbContext owner)
        => CreateHistoryServices(owner, TimeProvider.System).Projection;

    internal static HistoryServices CreateHistoryServices(SimpleChatsDbContext owner, TimeProvider clock) {
        var options = new DbContextOptionsBuilder<ProviderHistoryDbContext>()
            .UseNpgsql(owner.Database.GetConnectionString())
            .Options;
        var transactions = TransactionsFor(owner);
        var partitions = new HistoryPartitionStore(new HistoryFactory(options), options, transactions);
        var outbox = new HistoryOutboxWriter(options, transactions, clock);
        return new(partitions, outbox, transactions, new LlmChatHistoryProjection(partitions, outbox));
    }

    internal sealed record HistoryServices(
        HistoryPartitionStore Partitions,
        HistoryOutboxWriter Outbox,
        CoordinatedDatabaseTransaction Transactions,
        LlmChatHistoryProjection Projection);

    private sealed class HistoryFactory(DbContextOptions<ProviderHistoryDbContext> options) : IDbContextFactory<ProviderHistoryDbContext> {
        public ProviderHistoryDbContext CreateDbContext() => new(options);
    }
}

internal sealed class LlmChatOwnerOptionsFactory(DbContextOptions<SimpleChatsDbContext> options) : IDbContextFactory<SimpleChatsDbContext> {
    public SimpleChatsDbContext CreateDbContext() => new(options);
}
