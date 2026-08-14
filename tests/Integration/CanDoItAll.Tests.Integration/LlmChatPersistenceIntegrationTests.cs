using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class EfLlmConversationStoreIntegrationTests
{
    [Fact]
    public async Task Independent_stores_apply_one_cross_process_cas_winner()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatstorecas");
        var firstStore = new EfLlmConversationStore(database.CreateFactory());
        var secondStore = new EfLlmConversationStore(database.CreateFactory());
        var conversationId = Guid.NewGuid();
        var original = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        await firstStore.CreateAsync(original);

        var first = AdmitTurn(original, Guid.NewGuid(), "first");
        var second = AdmitTurn(original, Guid.NewGuid(), "second");
        var outcomes = await Task.WhenAll(CaptureAsync(firstStore.ReplaceAsync(first, 0)), CaptureAsync(secondStore.ReplaceAsync(second, 0)));

        Assert.Single(outcomes, outcome => outcome.Document is not null);
        var conflict = Assert.Single(outcomes, outcome => outcome.Exception is not null).Exception;
        Assert.Equal(LlmConversationFailureKind.ConcurrencyConflict, Assert.IsType<LlmConversationException>(conflict).Kind);
        var stored = await firstStore.TryGetAsync(conversationId);
        Assert.NotNull(stored);
        Assert.Equal(1, stored.TranscriptRevision);
        Assert.Single(stored.Entries);
        Assert.NotNull(stored.ActiveTurn);
    }

    [Fact]
    public async Task Compensation_removes_only_the_exact_pending_entry()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatstorecompensation");
        var store = new EfLlmConversationStore(database.CreateFactory());
        var conversationId = Guid.NewGuid();
        var original = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        await store.CreateAsync(original);
        var admitted = AdmitTurn(original, Guid.NewGuid(), "pending");
        await store.ReplaceAsync(admitted, 0);

        var compensated = admitted with
        {
            TranscriptRevision = 2,
            UpdatedAtUtc = admitted.UpdatedAtUtc.AddSeconds(1),
            Entries = [],
            ActiveTurn = null
        };
        await store.ReplaceAsync(compensated, 1);

        var stored = await store.TryGetAsync(conversationId);
        Assert.NotNull(stored);
        Assert.Empty(stored.Entries);
        Assert.Null(stored.ActiveTurn);
        Assert.Equal(2, stored.TranscriptRevision);
    }

    private static LlmConversationDocument AdmitTurn(LlmConversationDocument document, Guid turnId, string text)
    {
        var admittedAt = document.UpdatedAtUtc.AddSeconds(1);
        var entry = new LlmConversationTranscriptEntry(Guid.NewGuid(), turnId, LlmMessageRole.User, text, admittedAt);
        return document with
        {
            TranscriptRevision = document.TranscriptRevision + 1,
            UpdatedAtUtc = admittedAt,
            Entries = document.Entries.Add(entry),
            ActiveTurn = new LlmConversationActiveTurn(
                turnId,
                entry.EntryId,
                admittedAt,
                document.TranscriptRevision + 1)
        };
    }

    private static async Task<StoreOutcome> CaptureAsync(Task<LlmConversationDocument> operation)
    {
        try
        {
            return new StoreOutcome(await operation, null);
        }
        catch (Exception exception)
        {
            return new StoreOutcome(null, exception);
        }
    }

    private sealed record StoreOutcome(LlmConversationDocument? Document, Exception? Exception);
}

public sealed class LlmChatPersistenceIntegrationTests
{
    [Fact]
    public async Task Definition_revisions_append_and_preserve_provider_default_versus_explicit_none()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatrevisions");
        await using var dbContext = database.CreateDbContext();
        var repository = new EfLlmChatDefinitionRepository(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext);
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        var firstRevision = CreateRevision(definitionId, 1, AgentReasoningEffortLevel.None, now);
        var definition = new LlmChatDefinition(
            definitionId,
            firstRevision.Name,
            firstRevision.Summary,
            firstRevision.AvatarImageUrl,
            LlmChatDefinitionStatus.Active,
            firstRevision.Revision,
            now,
            now,
            0);
        await unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            await repository.CreateAsync(definition, firstRevision, cancellationToken);
            return true;
        });

        var secondRevision = CreateRevision(definitionId, 2, null, now.AddMinutes(1));
        var updated = new LlmChatDefinition(
            definitionId,
            secondRevision.Name,
            secondRevision.Summary,
            secondRevision.AvatarImageUrl,
            LlmChatDefinitionStatus.Active,
            secondRevision.Revision,
            now,
            now.AddMinutes(1),
            1);
        await unitOfWork.ExecuteAsync(async cancellationToken =>
        {
            await repository.ReplaceAsync(updated, 0, secondRevision, cancellationToken);
            return true;
        });

        var rows = await dbContext.Set<LlmChatDefinitionRevisionRow>()
            .AsNoTracking()
            .OrderBy(row => row.Revision)
            .ToArrayAsync();
        Assert.Equal(2, rows.Length);
        Assert.Equal(AgentReasoningEffortLevel.None, rows[0].ThinkingEffort);
        Assert.Null(rows[1].ThinkingEffort);
        Assert.NotEqual(rows[0].SettingsFingerprint, rows[1].SettingsFingerprint);
        var loaded = await repository.TryGetRevisionAsync(definitionId, new LlmChatDefinitionRevisionNumber(2));
        Assert.NotNull(loaded);
        Assert.Null(loaded.Settings.ThinkingEffort);
    }

    private static LlmChatDefinitionRevision CreateRevision(
        LlmChatDefinitionId definitionId,
        int revision,
        AgentReasoningEffortLevel? thinkingEffort,
        DateTimeOffset createdAtUtc)
        => new(
            definitionId,
            new LlmChatDefinitionRevisionNumber(revision),
            $"Definition {revision}",
            "Summary",
            "",
            "System prompt",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderKind.OpenAi,
            "Provider",
            "model",
            new LlmModelSettings(0.2, "{}") { ThinkingEffort = thinkingEffort },
            TimeSpan.FromMinutes(2),
            new LlmResponseFormat(true, "{\"type\":\"object\"}", "response", "response schema"),
            createdAtUtc,
            "test");
}

public sealed class LlmChatOperationDispatchClaimIntegrationTests
{
    [Fact]
    public async Task Independent_postgresql_repositories_admit_once_and_claim_dispatch_once()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatoperationclaim");
        var conversationId = await SeedConversationAsync(database);
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstRepository = new EfLlmChatOperationRepository(firstContext);
        var secondRepository = new EfLlmChatOperationRepository(secondContext);
        var operation = new LlmChatOperation(
            new LlmChatOperationId(Guid.NewGuid()),
            conversationId,
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('b', 64)),
            0,
            LlmChatOperationStatus.Pending,
            DateTimeOffset.UtcNow,
            0);

        var admissions = await Task.WhenAll(
            firstRepository.AdmitAsync(operation),
            secondRepository.AdmitAsync(operation));

        Assert.Single(admissions, admission => admission.Created);
        Assert.All(admissions, admission => Assert.Equal(operation.RequestFingerprint, admission.Operation.RequestFingerprint));

        var claims = await Task.WhenAll(
            firstRepository.TryClaimDispatchAsync(operation.Id, operation.RequestFingerprint),
            secondRepository.TryClaimDispatchAsync(operation.Id, operation.RequestFingerprint));

        var winner = Assert.Single(claims, claim => claim is not null);
        Assert.Equal(LlmChatOperationStatus.Running, winner!.Status);
        Assert.Equal(1, winner.ConcurrencyToken);
        var stored = await firstRepository.TryGetAsync(operation.Id);
        Assert.NotNull(stored);
        Assert.Equal(LlmChatOperationStatus.Running, stored.Status);
        Assert.Equal(1, stored.ConcurrencyToken);
    }

    private static async Task<LlmChatConversationId> SeedConversationAsync(LlmChatsPostgreSqlTestDatabase database)
    {
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateDbContext();
        context.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Operation claim",
            Summary = "",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        context.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
            definitionId,
            1,
            AgentReasoningEffortLevel.Low,
            now));
        context.Add(LlmChatsPostgreSqlTestDatabase.CreateTranscriptRow(conversationId, now));
        context.Add(new LlmChatConversationRow
        {
            Id = conversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 1,
            Title = "Operation claim",
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        await context.SaveChangesAsync();
        return new LlmChatConversationId(conversationId);
    }
}

public sealed class LlmChatsDatabaseTransferIntegrationTests
{
    [Fact]
    public async Task Transfer_round_trip_preserves_all_ids_revisions_operations_audit_and_references()
    {
        await using var source = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchattransfersource");
        await using var target = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchattransfertarget");
        var seeded = await SeedCompleteGraphAsync(source);
        await using var sourceContext = source.CreateDbContext();
        await using var targetContext = target.CreateDbContext();
        var handler = new LlmChatsDatabaseTransferHandler();
        var context = new DatabaseTransferContext(
            CreateProfile(source.ConnectionString),
            CreateProfile(target.ConnectionString),
            sourceContext,
            targetContext,
            ReplaceExisting: true);

        var result = await handler.TransferAsync(context);

        Assert.True(result.Success, result.Message);
        Assert.True(result.RecordsCopied >= 8);
        Assert.True(await targetContext.Set<LlmChatDefinitionRow>().AnyAsync(row => row.Id == seeded.DefinitionId));
        Assert.Equal(2, await targetContext.Set<LlmChatDefinitionRevisionRow>().CountAsync(row => row.DefinitionId == seeded.DefinitionId));
        Assert.True(await targetContext.Set<LlmChatDefinitionTagRow>().AnyAsync(row => row.DefinitionId == seeded.DefinitionId));
        Assert.True(await targetContext.Set<LlmChatConversationRow>().AnyAsync(row => row.Id == seeded.ConversationId));
        Assert.True(await targetContext.Set<LlmChatTranscriptRow>().AnyAsync(row => row.ConversationId == seeded.ConversationId));
        Assert.True(await targetContext.Set<LlmChatMessageRow>().AnyAsync(row => row.ConversationId == seeded.ConversationId));
        Assert.True(await targetContext.Set<LlmChatOperationRow>().AnyAsync(row => row.Id == seeded.OperationId));
        var audit = Assert.Single(await targetContext.Set<LlmChatInvocationRecordRow>().AsNoTracking().ToArrayAsync());
        Assert.Equal(seeded.OperationId, audit.OperationId);
        Assert.Equal(AgentReasoningEffortLevel.None, audit.RequestedThinkingEffort);
        Assert.Equal(AgentReasoningEffortLevel.Medium, audit.EffectiveThinkingEffort);
    }

    private static async Task<SeededGraph> SeedCompleteGraphAsync(LlmChatsPostgreSqlTestDatabase database)
    {
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateDbContext();
        context.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Transferred",
            Summary = "Summary",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 2,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 1
        });
        context.AddRange(
            LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(definitionId, 1, AgentReasoningEffortLevel.None, now),
            LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(definitionId, 2, null, now.AddSeconds(1)));
        context.Add(new LlmChatDefinitionTagRow { DefinitionId = definitionId, Tag = "support" });
        context.Add(LlmChatsPostgreSqlTestDatabase.CreateTranscriptRow(conversationId, now));
        context.Add(new LlmChatMessageRow
        {
            EntryId = Guid.NewGuid(),
            ConversationId = conversationId,
            Sequence = 1,
            TurnId = operationId,
            Role = LlmMessageRole.User,
            Text = "Hello",
            CreatedAtUtc = now,
            Model = ""
        });
        context.Add(new LlmChatConversationRow
        {
            Id = conversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 2,
            Title = "Transferred conversation",
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        context.Add(new LlmChatOperationRow
        {
            Id = operationId,
            ConversationId = conversationId,
            Kind = LlmChatOperationKind.SendTurn,
            RequestFingerprint = new string('a', 64),
            ExpectedTranscriptRevision = 0,
            Status = LlmChatOperationStatus.Succeeded,
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(1),
            ResultingTranscriptRevision = 1,
            ConcurrencyToken = 1
        });
        context.Add(new LlmChatInvocationRecordRow
        {
            OperationId = operationId,
            Ordinal = 1,
            ProviderProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderKind = ProviderKind.OpenAi,
            ProviderName = "Provider",
            Model = "model",
            RequestedThinkingEffort = AgentReasoningEffortLevel.None,
            EffectiveThinkingEffort = AgentReasoningEffortLevel.Medium,
            InputTokens = 10,
            OutputTokens = 4,
            CachedInputTokens = 2,
            Outcome = LlmChatInvocationOutcome.Succeeded,
            FailureCode = "",
            StartedAtUtc = now,
            CompletedAtUtc = now.AddSeconds(1),
            CorrelationId = "transfer-test"
        });
        await context.SaveChangesAsync();
        return new SeededGraph(definitionId, conversationId, operationId);
    }

    private static ResolvedDatabaseProfile CreateProfile(string connectionString)
        => new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test",
                ProviderKind = DatabaseProviderKind.PostgreSql,
                SourceKind = DatabaseProfileSourceKind.PostgresConnection
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            connectionString);

    private sealed record SeededGraph(Guid DefinitionId, Guid ConversationId, Guid OperationId);
}

internal sealed class LlmChatsPostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly PostgresTestDatabaseLease lease;

    private LlmChatsPostgreSqlTestDatabase(PostgresTestDatabaseLease lease)
    {
        this.lease = lease;
    }

    public string ConnectionString => lease.ConnectionString;

    public static async Task<LlmChatsPostgreSqlTestDatabase> CreateAsync(string key)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var lease = PostgresTestDatabaseLease.Create(key);
        var database = new LlmChatsPostgreSqlTestDatabase(lease);
        await using var context = database.CreateDbContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public AppDbContext CreateDbContext()
        => new(lease.CreateAppDbContextOptions());

    public IDbContextFactory<AppDbContext> CreateFactory()
        => new LlmChatTestDbContextFactory(lease.CreateAppDbContextOptions());

    public ValueTask DisposeAsync()
        => lease.DisposeAsync();

    public static LlmConversationDocument CreateDocument(Guid conversationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new LlmConversationDocument(
            conversationId,
            "Conversation",
            new LlmConversationProviderSnapshot(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Provider",
                ProviderKind.OpenAi,
                "model"),
            now,
            now,
            0,
            ImmutableArray<LlmConversationTranscriptEntry>.Empty);
    }

    public static LlmChatDefinitionRevisionRow CreateRevisionRow(
        Guid definitionId,
        int revision,
        AgentReasoningEffortLevel? thinkingEffort,
        DateTimeOffset createdAtUtc)
        => new()
        {
            DefinitionId = definitionId,
            Revision = revision,
            Name = $"Revision {revision}",
            Summary = "Summary",
            AvatarImageUrl = "",
            SystemPrompt = "System",
            ProviderProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderKind = ProviderKind.OpenAi,
            ProviderName = "Provider",
            Model = "model",
            Temperature = 0.2,
            ThinkingEffort = thinkingEffort,
            ModelParameterConfigurationJson = "{}",
            SettingsFingerprint = new string((char)('a' + revision), 64),
            CreatedAtUtc = createdAtUtc,
            Reason = "test"
        };

    public static LlmChatTranscriptRow CreateTranscriptRow(Guid conversationId, DateTimeOffset now)
        => new()
        {
            ConversationId = conversationId,
            Title = "Transferred conversation",
            ProviderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderName = "Provider",
            ProviderKind = ProviderKind.OpenAi,
            Model = "model",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            TranscriptRevision = 1,
            EntryCount = 1
        };

    private sealed class LlmChatTestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => new(options);
    }
}
