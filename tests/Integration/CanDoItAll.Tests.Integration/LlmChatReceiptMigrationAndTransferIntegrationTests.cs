using CanDoItAll.Tests.Support;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatReceiptMigrationAndTransferIntegrationTests {
    private const string StartingMigration = "20260830104752_AddProviderHistoryExternalReference";
    private const string ReceiptMigration = "20260910163827_AddLlmChatDefinitionCreateReceipts";

    [Fact]
    public async Task Migration_upgrades_saved_chat_and_defers_only_the_receipt_parent_constraint() {
        await using var database = LlmChatsPostgreSqlTestDatabase.CreateUnmigrated("receipt-starting-upgrade");
        var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(Guid.NewGuid());
        await using (var before = database.CreateDbContext()) {
            await before.GetService<IMigrator>().MigrateAsync(StartingMigration);
            LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(before, document);
            before.Add(LlmConversationPersistenceMapper.ToRow(document));
            await before.SaveChangesAsync();
        }

        await using (var upgraded = database.CreateDbContext()) {
            await upgraded.Database.MigrateAsync();
            Assert.False(upgraded.Database.HasPendingModelChanges());
            Assert.Contains(ReceiptMigration, await upgraded.Database.GetAppliedMigrationsAsync());
            Assert.True(await upgraded.Database.SqlQueryRaw<bool>("""
                SELECT condeferrable AND condeferred AS "Value" FROM pg_constraint
                WHERE conname = 'FK_LlmChats_CreateReceipt_OriginalRevision'
                """).SingleAsync());
            Assert.False(await upgraded.Database.SqlQueryRaw<bool>("""
                SELECT condeferrable AS "Value" FROM pg_constraint
                WHERE conname = 'PK_LlmChats_DefinitionCreateReceipts'
                """).SingleAsync());
        }

        await using var owner = database.CreateSimpleChatsDbContext();
        var restored = await new EfLlmConversationStore(owner).TryGetAsync(document.ConversationId);
        Assert.NotNull(restored);
        Assert.Equal(document.Title, restored.Title);
        Assert.Equal(document.Provider, restored.Provider);
        await using var transaction = await owner.Database.BeginTransactionAsync();
        var receipt = new LlmChatDefinitionCreateReceipt(NewKey(), new(Guid.NewGuid()), new(1), 0, DateTimeOffset.UtcNow);
        Assert.True(await new EfLlmChatDefinitionRepository(owner).TryClaimAsync(new(receipt, new(new string('A', 64)))));
        var rejected = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, rejected.SqlState);
        await using var verification = database.CreateSimpleChatsDbContext();
        Assert.Empty(await verification.Set<LlmChatDefinitionCreateReceiptRow>().ToArrayAsync());
        Assert.Single(await verification.Set<LlmChatDefinitionRow>().ToArrayAsync());
    }

    [Fact]
    public async Task Empty_receipt_schema_can_be_reversed_and_reapplied_without_losing_saved_definitions() {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-empty-rollback");
        var definitionId = Guid.NewGuid();
        await using (var owner = database.CreateSimpleChatsDbContext()) {
            SeedDefinition(owner, definitionId, "Existing definition");
            await owner.SaveChangesAsync();
        }

        await using (var complete = database.CreateDbContext()) {
            await complete.GetService<IMigrator>().MigrateAsync(StartingMigration);
            Assert.DoesNotContain(ReceiptMigration, await complete.Database.GetAppliedMigrationsAsync());
            await complete.Database.MigrateAsync();
        }

        await using var restarted = database.CreateSimpleChatsDbContext();
        Assert.Equal("Existing definition", (await restarted.Set<LlmChatDefinitionRow>().SingleAsync()).Name);
        Assert.Equal(definitionId, (await restarted.Set<LlmChatDefinitionRevisionRow>().SingleAsync()).DefinitionId);
        Assert.Empty(await restarted.Set<LlmChatDefinitionCreateReceiptRow>().ToArrayAsync());
    }

    [Fact]
    public async Task Retained_receipt_blocks_schema_rollback_and_survives_restart() {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-retained-rollback");
        var created = await CreateAsync(database);
        await using (var complete = database.CreateDbContext()) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => complete.GetService<IMigrator>().MigrateAsync(StartingMigration));
            Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        }

        await using var restarted = database.CreateSimpleChatsDbContext();
        var receipt = await new EfLlmChatDefinitionRepository(restarted).TryGetReceiptAsync(created.Command.Key);
        Assert.Equal(created.Receipt, receipt!.Receipt);
        Assert.Equal(created.Receipt.DefinitionId.Value, (await restarted.Set<LlmChatDefinitionRow>().SingleAsync()).Id);
        await using var schema = database.CreateDbContext();
        Assert.Contains(ReceiptMigration, await schema.Database.GetAppliedMigrationsAsync());
        Assert.False(schema.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Transfer_preserves_replay_and_intervening_edits_after_restart_without_resolving_a_provider() {
        await using var source = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-transfer-source");
        await using var target = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-transfer-target");
        var created = await CreateAsync(source);
        var remappedProviderId = Guid.NewGuid();
        string fingerprint;
        await using (var humanEdit = source.CreateSimpleChatsDbContext()) {
            var definition = await humanEdit.Set<LlmChatDefinitionRow>().SingleAsync();
            definition.Name = "Human edited definition";
            definition.CurrentRevision = 2;
            definition.ConcurrencyToken = 1;
            definition.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var revision = LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(definition.Id, 2, null, definition.UpdatedAtUtc);
            revision.Name = definition.Name;
            revision.ProviderProfileId = remappedProviderId;
            humanEdit.Add(revision);
            await humanEdit.SaveChangesAsync();
            fingerprint = (await humanEdit.Set<LlmChatDefinitionCreateReceiptRow>().SingleAsync()).SemanticFingerprint;
        }

        await using (var legacyTarget = target.CreateSimpleChatsDbContext()) {
            SeedDefinition(legacyTarget, Guid.NewGuid(), "Replaced legacy target");
            await legacyTarget.SaveChangesAsync();
        }

        await using (var sourceComplete = source.CreateDbContext()) {
            await using var targetComplete = target.CreateDbContext();
            var context = TransferContext(source, target, sourceComplete, targetComplete);
            var handler = CreateTransferHandler(new(), TimeProvider.System);
            var preview = await handler.PreviewAsync(context);
            Assert.True(preview.IsAvailable);
            Assert.Equal(5, preview.SourceRecordCount);
            var result = await handler.TransferAsync(context);
            Assert.True(result.Success, result.Message);
            Assert.Equal(5, result.RecordsCopied);
        }

        await using var restarted = target.CreateSimpleChatsDbContext();
        var resolver = new Resolver { MustNotResolve = true };
        var replayed = await Writer(restarted, resolver).CreateOnceAsync(created.Command);
        Assert.True(replayed.IsSuccess);
        Assert.True(replayed.Value!.WasReplay);
        Assert.Equal(created.Receipt, replayed.Value.Receipt);
        Assert.Equal(0, resolver.Calls);
        var current = await restarted.Set<LlmChatDefinitionRow>().AsNoTracking().SingleAsync();
        Assert.Equal(("Human edited definition", 2, 1L), (current.Name, current.CurrentRevision, current.ConcurrencyToken));
        Assert.Equal(remappedProviderId, (await restarted.Set<LlmChatDefinitionRevisionRow>().SingleAsync(row => row.Revision == 2)).ProviderProfileId);
        Assert.Equal(fingerprint, (await restarted.Set<LlmChatDefinitionCreateReceiptRow>().SingleAsync()).SemanticFingerprint);
        Assert.Equal(2, await restarted.Set<LlmChatDefinitionRevisionRow>().CountAsync());
    }

    [Fact]
    public async Task Transfer_rejects_replacement_of_a_retained_target_receipt_without_changing_either_owner() {
        await using var source = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-protected-source");
        await using var target = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-protected-target");
        var incoming = await CreateAsync(source);
        var retained = await CreateAsync(target);
        await using (var sourceComplete = source.CreateDbContext()) {
            await using var targetComplete = target.CreateDbContext();
            var context = TransferContext(source, target, sourceComplete, targetComplete);
            var handler = CreateTransferHandler(new(), TimeProvider.System);
            var preview = await handler.PreviewAsync(context);
            Assert.False(preview.IsAvailable);
            Assert.Contains("retained creation receipts", preview.Warning, StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(context));
        }

        await using var restarted = target.CreateSimpleChatsDbContext();
        Assert.Equal(retained.Receipt, (await new EfLlmChatDefinitionRepository(restarted).TryGetReceiptAsync(retained.Command.Key))!.Receipt);
        Assert.Null(await new EfLlmChatDefinitionRepository(restarted).TryGetReceiptAsync(incoming.Command.Key));
        Assert.Equal(retained.Receipt.DefinitionId.Value, (await restarted.Set<LlmChatDefinitionRow>().SingleAsync()).Id);
        await using var sourceVerification = source.CreateSimpleChatsDbContext();
        Assert.Equal(incoming.Receipt.DefinitionId.Value, (await sourceVerification.Set<LlmChatDefinitionRow>().SingleAsync()).Id);
    }

    [Theory]
    [InlineData(ReceiptCorruption.Version)]
    [InlineData(ReceiptCorruption.Fingerprint)]
    [InlineData(ReceiptCorruption.Scope)]
    public async Task Transfer_rejects_invalid_receipt_metadata_before_touching_the_target(ReceiptCorruption corruption) {
        await using var source = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-invalid-source");
        await using var target = await LlmChatsPostgreSqlTestDatabase.CreateAsync("receipt-invalid-target");
        _ = await CreateAsync(source);
        await using (var damaged = source.CreateDbContext()) {
            if (corruption == ReceiptCorruption.Version) {
                await damaged.Set<LlmChatDefinitionCreateReceiptRow>().ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SemanticVersion, 99));
            } else if (corruption == ReceiptCorruption.Fingerprint) {
                await damaged.Set<LlmChatDefinitionCreateReceiptRow>().ExecuteUpdateAsync(setters => setters.SetProperty(row => row.SemanticFingerprint, new string('Z', 64)));
            } else {
                await damaged.Set<LlmChatDefinitionCreateReceiptRow>().ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Actor, " actor "));
            }
        }

        await using (var sourceComplete = source.CreateDbContext()) {
            await using var targetComplete = target.CreateDbContext();
            var handler = CreateTransferHandler(new(), TimeProvider.System);
            await Assert.ThrowsAsync<InvalidDataException>(() => handler.TransferAsync(TransferContext(source, target, sourceComplete, targetComplete)));
        }

        await using var verification = target.CreateSimpleChatsDbContext();
        Assert.Empty(await verification.Set<LlmChatDefinitionRow>().ToArrayAsync());
        Assert.Empty(await verification.Set<LlmChatDefinitionCreateReceiptRow>().ToArrayAsync());
    }

    private static async Task<(CreateLlmChatDefinitionOnceCommand Command, LlmChatDefinitionCreateReceipt Receipt)> CreateAsync(
        LlmChatsPostgreSqlTestDatabase database) {
        var command = new CreateLlmChatDefinitionOnceCommand(NewKey(), new("Original definition", "Synthetic summary", "",
            "Synthetic system prompt", Guid.NewGuid(), "fixture-model", new LlmModelSettings(0.3, "{}"),
            TimeSpan.FromSeconds(15), new(true, "{\"type\":\"object\"}", "answer", "Synthetic schema"),
            "Initial version", ["synthetic"]));
        await using var owner = database.CreateSimpleChatsDbContext();
        var created = await Writer(owner, new Resolver()).CreateOnceAsync(command);
        Assert.True(created.IsSuccess);
        return (command, created.Value!.Receipt);
    }

    private static LlmChatDefinitionApplicationService Writer(SimpleChatsDbContext owner, Resolver resolver) {
        var repository = new EfLlmChatDefinitionRepository(owner);
        return new(repository, new EfLlmChatDefinitionReadStore(owner),
            new EfLlmChatUnitOfWork(owner, UnfencedLlmChatCommitFence.Instance, LlmChatTestPersistence.TransactionsFor(owner)),
            resolver, TimeProvider.System, repository);
    }

    private static LlmChatDefinitionCreateKey NewKey()
        => new(new("migration-transfer-fixture", "operator", "synthetic-history"), new(Guid.NewGuid()));

    private static void SeedDefinition(SimpleChatsDbContext owner, Guid id, string name) {
        var now = DateTimeOffset.UtcNow;
        owner.Add(new LlmChatDefinitionRow {
            Id = id, Name = name, Summary = "", AvatarImageUrl = "", Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1, ConcurrencyToken = 0, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        owner.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(id, 1, null, now));
    }

    private static DatabaseTransferOperation TransferContext(LlmChatsPostgreSqlTestDatabase source,
        LlmChatsPostgreSqlTestDatabase target, AppDbContext sourceContext, AppDbContext targetContext)
        => new(Profile(source.ConnectionString), Profile(target.ConnectionString), true);

    private static ResolvedDatabaseProfile Profile(string connectionString)
        => new(new DatabaseProfileRecord {
            Id = Guid.NewGuid(), DisplayName = "Synthetic migration/transfer", ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection
        }, DatabaseProfileResolutionSource.ExplicitOverride, connectionString);

    private sealed class Resolver : ILlmChatProviderResolver {
        public bool MustNotResolve { get; init; }
        public int Calls { get; private set; }

        public Task<Result<LlmChatResolvedProvider>> ResolveAsync(Guid providerProfileId, string model,
            AgentReasoningEffortLevel? thinkingEffort, CancellationToken cancellationToken = default) {
            Calls++;
            Assert.False(MustNotResolve);
            var capability = new ProviderModelThinkingEffortCapability(model, AgentThinkingEffortSupportStatus.Supported,
                AgentThinkingEffortCapabilitySource.Defined, Enum.GetValues<AgentReasoningEffortLevel>());
            return Task.FromResult(Result<LlmChatResolvedProvider>.Success(new(providerProfileId, "Fixture provider",
                ProviderKind.OpenAi, model, capability, AgentReasoningEffortLevel.Medium)));
        }

        public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success([]));
    }

    public enum ReceiptCorruption { Version, Fingerprint, Scope }
    private static LlmChatsDatabaseTransferHandler CreateTransferHandler(LlmChatTransferOptions options, TimeProvider clock) {
        var sessions = new DatabaseTransferOwnerSessionRunner();
        return new(options, clock, DatabaseTransferTestSupport.Create(sessions), sessions);
    }

}
