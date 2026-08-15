using System.Collections.Immutable;
using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Modules.LlmChats.Persistence.ReadModels;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class EfLlmConversationStoreIntegrationTests
{
    [Fact]
    public async Task Independent_stores_apply_one_cross_process_cas_winner()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatstorecas");
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstStore = new EfLlmConversationStore(firstContext);
        var secondStore = new EfLlmConversationStore(secondContext);
        var conversationId = Guid.NewGuid();
        var original = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(firstContext, original);
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
        await using var dbContext = database.CreateDbContext();
        var store = new EfLlmConversationStore(dbContext);
        var conversationId = Guid.NewGuid();
        var original = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(dbContext, original);
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

internal sealed class UnfencedLlmChatCommitFence : ILlmChatCommitFence
{
    public static UnfencedLlmChatCommitFence Instance { get; } = new();

    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}

internal sealed class UnfencedDatabaseRuntimeWriteFence : IDatabaseRuntimeWriteFence
{
    public static UnfencedDatabaseRuntimeWriteFence Instance { get; } = new();

    public Task<T> ExecuteAsync<T>(
        DatabaseRuntimeSnapshot expected,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}

internal sealed class LlmChatTestDbContextFactory(
    LlmChatsPostgreSqlTestDatabase database) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
        => database.CreateDbContext();

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

public sealed class LlmChatConversationTransactionIntegrationTests
{
    [Fact]
    public async Task Create_rolls_back_product_and_transcript_when_the_command_fails_after_store_flush()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatcreateatomic");
        await using var dbContext = database.CreateDbContext();
        var definitionId = await SeedDefinitionAsync(dbContext);
        var conversationId = Guid.NewGuid();
        var repository = new EfLlmChatConversationRepository(dbContext);
        var store = new EfLlmConversationStore(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var transcript = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId) with
        {
            Title = "Atomic create"
        };
        var conversation = CreateConversation(
            conversationId,
            definitionId,
            transcript.Title,
            transcript.CreatedAtUtc);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async cancellationToken =>
        {
            await repository.CreateAsync(conversation, cancellationToken);
            await store.CreateAsync(transcript, cancellationToken);
            throw new InvalidOperationException("Injected failure after transcript persistence.");
        }));

        dbContext.ChangeTracker.Clear();
        Assert.False(await dbContext.Set<LlmChatConversationRow>().AnyAsync(row => row.Id == conversationId));
        Assert.False(await dbContext.Set<LlmChatTranscriptRow>().AnyAsync(row => row.ConversationId == conversationId));
    }

    [Fact]
    public async Task Rename_rolls_back_product_and_transcript_when_the_command_fails_after_store_flush()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatrenameatomic");
        await using var dbContext = database.CreateDbContext();
        var definitionId = await SeedDefinitionAsync(dbContext);
        var conversationId = Guid.NewGuid();
        var repository = new EfLlmChatConversationRepository(dbContext);
        var store = new EfLlmConversationStore(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var originalTranscript = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId) with
        {
            Title = "Original"
        };
        var originalConversation = CreateConversation(
            conversationId,
            definitionId,
            originalTranscript.Title,
            originalTranscript.CreatedAtUtc);
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(originalTranscript));
        await repository.CreateAsync(originalConversation);
        await dbContext.SaveChangesAsync();

        var renamedAt = originalTranscript.UpdatedAtUtc.AddSeconds(1);
        var renamedConversation = new LlmChatConversation(
            originalConversation.Id,
            originalConversation.DefinitionId,
            originalConversation.DefinitionRevision,
            "Renamed",
            originalConversation.Status,
            originalConversation.Origin,
            originalConversation.CreatedAtUtc,
            renamedAt,
            1);
        var renamedTranscript = originalTranscript with
        {
            Title = renamedConversation.Title,
            UpdatedAtUtc = renamedAt,
            TranscriptRevision = 1
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async cancellationToken =>
        {
            await repository.ReplaceAsync(renamedConversation, 0, cancellationToken);
            await store.ReplaceAsync(renamedTranscript, 0, cancellationToken);
            throw new InvalidOperationException("Injected failure after transcript persistence.");
        }));

        dbContext.ChangeTracker.Clear();
        var storedConversation = await dbContext.Set<LlmChatConversationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == conversationId);
        var storedTranscript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == conversationId);
        Assert.Equal("Original", storedConversation.Title);
        Assert.Equal(0, storedTranscript.TranscriptRevision);
    }

    private static async Task<Guid> SeedDefinitionAsync(AppDbContext dbContext)
    {
        var definitionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Atomic conversation",
            Summary = "",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        dbContext.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
            definitionId,
            1,
            AgentReasoningEffortLevel.None,
            now));
        await dbContext.SaveChangesAsync();
        return definitionId;
    }

    private static LlmChatConversation CreateConversation(
        Guid conversationId,
        Guid definitionId,
        string title,
        DateTimeOffset now)
        => new(
            new LlmChatConversationId(conversationId),
            new LlmChatDefinitionId(definitionId),
            new LlmChatDefinitionRevisionNumber(1),
            title,
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            now,
            now,
            0);
}

public sealed class LlmChatTurnTransactionIntegrationTests
{
    [Fact]
    public async Task Admission_rolls_back_claim_pending_message_active_turn_and_evidence_together()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatadmissionatomic");
        await using var dbContext = database.CreateDbContext();
        var seeded = await SeedConversationAsync(dbContext, admitted: false);
        var operationRepository = new EfLlmChatOperationRepository(dbContext);
        var invocationRepository = new EfLlmChatInvocationRecordRepository(dbContext);
        var store = new EfLlmConversationStore(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var evidenceSink = new LlmChatOperationEvidenceService(
            operationRepository,
            invocationRepository,
            unitOfWork,
            new LlmChatOperationScopeAccessor(),
            TimeProvider.System);
        var operation = CreateOperation(seeded.ConversationId, seeded.TurnId, seeded.Now);
        var admitted = Admit(seeded.Document, seeded.TurnId, seeded.Now.AddSeconds(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async token =>
        {
            await operationRepository.AdmitAsync(operation, token);
            await store.ReplaceAsync(admitted, seeded.Document.TranscriptRevision, token);
            await evidenceSink.MarkTurnAdmittedAsync(operation.Id, admitted.UpdatedAtUtc, token);
            throw new InvalidOperationException("Injected failure after atomic admission writes.");
        }));

        dbContext.ChangeTracker.Clear();
        Assert.False(await dbContext.Set<LlmChatOperationRow>().AnyAsync(row => row.Id == seeded.TurnId));
        Assert.False(await dbContext.Set<LlmChatMessageRow>().AnyAsync(row => row.ConversationId == seeded.ConversationId));
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == seeded.ConversationId);
        Assert.Equal(0, transcript.TranscriptRevision);
        Assert.Null(transcript.ActiveTurnId);
    }

    [Fact]
    public async Task Success_finalization_rolls_back_assistant_usage_and_terminal_status_together()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatsuccessatomic");
        await using var dbContext = database.CreateDbContext();
        var seeded = await SeedConversationAsync(dbContext, admitted: true);
        var operationRepository = new EfLlmChatOperationRepository(dbContext);
        var invocationRepository = new EfLlmChatInvocationRecordRepository(dbContext);
        var store = new EfLlmConversationStore(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var evidenceSink = new LlmChatOperationEvidenceService(
            operationRepository,
            invocationRepository,
            unitOfWork,
            new LlmChatOperationScopeAccessor(),
            TimeProvider.System);
        var assistant = new LlmConversationTranscriptEntry(
            Guid.NewGuid(),
            seeded.TurnId,
            LlmMessageRole.Assistant,
            "answer",
            seeded.Now.AddSeconds(2),
            "model",
            new LlmUsage(5, 3, 1));
        var completed = seeded.Document with
        {
            TranscriptRevision = seeded.Document.TranscriptRevision + 1,
            UpdatedAtUtc = assistant.CreatedAtUtc,
            Entries = seeded.Document.Entries.Add(assistant),
            ActiveTurn = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async token =>
        {
            await operationRepository.TryGetForUpdateAsync(new LlmChatOperationId(seeded.TurnId), token);
            await store.ReplaceAsync(completed, seeded.Document.TranscriptRevision, token);
            await evidenceSink.CompleteTranscriptAsync(
                new LlmChatOperationId(seeded.TurnId),
                completed.UpdatedAtUtc,
                completed.TranscriptRevision,
                assistant.EntryId,
                token);
            throw new InvalidOperationException("Injected failure after atomic success writes.");
        }));

        await AssertAdmittedAndNonterminalAsync(dbContext, seeded, LlmChatOperationStatus.Running);
        Assert.False(await dbContext.Set<LlmChatMessageRow>().AnyAsync(row => row.EntryId == assistant.EntryId));
    }

    [Fact]
    public async Task Failure_compensation_rolls_back_turn_clear_and_terminal_failure_together()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatcompensationatomic");
        await using var dbContext = database.CreateDbContext();
        var seeded = await SeedConversationAsync(dbContext, admitted: true);
        var operationRepository = new EfLlmChatOperationRepository(dbContext);
        var invocationRepository = new EfLlmChatInvocationRecordRepository(dbContext);
        var store = new EfLlmConversationStore(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var evidenceSink = new LlmChatOperationEvidenceService(
            operationRepository,
            invocationRepository,
            unitOfWork,
            new LlmChatOperationScopeAccessor(),
            TimeProvider.System);
        var compensated = seeded.Document with
        {
            TranscriptRevision = seeded.Document.TranscriptRevision + 1,
            UpdatedAtUtc = seeded.Now.AddSeconds(2),
            Entries = [],
            ActiveTurn = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync<bool>(async token =>
        {
            await operationRepository.TryGetForUpdateAsync(new LlmChatOperationId(seeded.TurnId), token);
            await store.ReplaceAsync(compensated, seeded.Document.TranscriptRevision, token);
            await evidenceSink.CompleteFailureAsync(
                new LlmChatOperationId(seeded.TurnId),
                compensated.UpdatedAtUtc,
                LlmChatErrorCodes.ProviderUnavailable,
                token);
            throw new InvalidOperationException("Injected failure after atomic compensation writes.");
        }));

        await AssertAdmittedAndNonterminalAsync(dbContext, seeded, LlmChatOperationStatus.Running);
    }

    private static async Task AssertAdmittedAndNonterminalAsync(
        AppDbContext dbContext,
        SeededTurn seeded,
        LlmChatOperationStatus expectedStatus)
    {
        dbContext.ChangeTracker.Clear();
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == seeded.ConversationId);
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == seeded.TurnId);
        Assert.Equal(seeded.Document.TranscriptRevision, transcript.TranscriptRevision);
        Assert.Equal(seeded.TurnId, transcript.ActiveTurnId);
        Assert.Equal(expectedStatus, operation.Status);
        Assert.Null(operation.CompletedAtUtc);
    }

    private static async Task<SeededTurn> SeedConversationAsync(AppDbContext dbContext, bool admitted)
    {
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var original = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        var document = admitted ? Admit(original, turnId, now.AddSeconds(1)) : original;
        dbContext.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Atomic turn",
            Summary = "",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        dbContext.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
            definitionId,
            1,
            AgentReasoningEffortLevel.Low,
            now));
        dbContext.Add(new LlmChatConversationRow
        {
            Id = conversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 1,
            Title = document.Title,
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            ConcurrencyToken = 0
        });
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(document));
        for (var index = 0; index < document.Entries.Length; index++)
        {
            dbContext.Add(LlmConversationPersistenceMapper.ToRow(
                conversationId,
                index + 1,
                document.Entries[index]));
        }

        if (admitted)
        {
            var operation = CreateOperation(conversationId, turnId, now) with
            {
                Status = LlmChatOperationStatus.Running,
                TurnAdmittedAtUtc = document.UpdatedAtUtc,
                ProviderDispatchStartedAtUtc = document.UpdatedAtUtc,
                ProviderDispatchReturnedAtUtc = document.UpdatedAtUtc.AddMilliseconds(1),
                ConcurrencyToken = 2
            };
            dbContext.Add(LlmChatPersistenceMapper.ToRow(operation));
        }

        await dbContext.SaveChangesAsync();
        return new SeededTurn(conversationId, turnId, now, document);
    }

    private static LlmConversationDocument Admit(
        LlmConversationDocument original,
        Guid turnId,
        DateTimeOffset admittedAtUtc)
    {
        var user = new LlmConversationTranscriptEntry(
            Guid.NewGuid(),
            turnId,
            LlmMessageRole.User,
            "pending",
            admittedAtUtc);
        return original with
        {
            TranscriptRevision = original.TranscriptRevision + 1,
            UpdatedAtUtc = admittedAtUtc,
            Entries = original.Entries.Add(user),
            ActiveTurn = new LlmConversationActiveTurn(
                turnId,
                user.EntryId,
                admittedAtUtc,
                original.TranscriptRevision + 1)
        };
    }

    private static LlmChatOperation CreateOperation(
        Guid conversationId,
        Guid turnId,
        DateTimeOffset startedAtUtc)
        => new(
            new LlmChatOperationId(turnId),
            new LlmChatConversationId(conversationId),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('c', 64)),
            0,
            LlmChatOperationStatus.Pending,
            startedAtUtc,
            0);

    private sealed record SeededTurn(
        Guid ConversationId,
        Guid TurnId,
        DateTimeOffset Now,
        LlmConversationDocument Document);
}

public sealed class LlmChatCanonicalTitleMigrationIntegrationTests
{
    private const string PreviousMigrationId = "20260814163458_AddLlmChats";

    [Fact]
    public async Task Migration_preserves_the_canonical_conversation_title_and_removes_the_duplicate_column()
    {
        await using var database = LlmChatsPostgreSqlTestDatabase.CreateUnmigrated("llmchattitlemigration");
        await using var dbContext = database.CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigrationId);
        var conversationId = await SeedPreviousSchemaAsync(dbContext, "Canonical", "Canonical");

        await migrator.MigrateAsync();

        var title = await dbContext.Database.SqlQueryRaw<string>(
                """SELECT "Title" AS "Value" FROM "LlmChats_Conversations" WHERE "Id" = {0}""",
                conversationId)
            .SingleAsync();
        var duplicateColumnCount = await dbContext.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::integer AS "Value"
                FROM information_schema.columns
                WHERE table_name = 'LlmChats_Transcripts'
                  AND column_name IN ('Title', 'CreatedAtUtc', 'UpdatedAtUtc')
                """)
            .SingleAsync();
        Assert.Equal("Canonical", title);
        Assert.Equal(0, duplicateColumnCount);
    }

    [Fact]
    public async Task Migration_fails_closed_when_existing_title_copies_disagree()
    {
        await using var database = LlmChatsPostgreSqlTestDatabase.CreateUnmigrated("llmchattitleconflict");
        await using var dbContext = database.CreateDbContext();
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigrationId);
        await SeedPreviousSchemaAsync(dbContext, "Canonical", "Divergent");

        var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Contains("Cannot canonicalize LLM Chat titles", exception.MessageText, StringComparison.Ordinal);
    }

    private static async Task<Guid> SeedPreviousSchemaAsync(
        AppDbContext dbContext,
        string conversationTitle,
        string transcriptTitle)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "LlmChats_Transcripts"
                ALTER COLUMN "Title" SET DEFAULT '',
                ALTER COLUMN "CreatedAtUtc" SET DEFAULT CURRENT_TIMESTAMP,
                ALTER COLUMN "UpdatedAtUtc" SET DEFAULT CURRENT_TIMESTAMP;
            """);
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Title migration",
            Summary = "",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        dbContext.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
            definitionId,
            1,
            AgentReasoningEffortLevel.None,
            now));
        dbContext.Add(new LlmChatTranscriptRow
        {
            ConversationId = conversationId,
            ProviderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderName = "Provider",
            ProviderKind = ProviderKind.OpenAi,
            Model = "model",
            TranscriptRevision = 0,
            EntryCount = 0
        });
        dbContext.Add(new LlmChatConversationRow
        {
            Id = conversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 1,
            Title = conversationTitle,
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            """UPDATE "LlmChats_Transcripts" SET "Title" = {0} WHERE "ConversationId" = {1};""",
            transcriptTitle,
            conversationId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "LlmChats_Transcripts"
                ALTER COLUMN "Title" DROP DEFAULT,
                ALTER COLUMN "CreatedAtUtc" DROP DEFAULT,
                ALTER COLUMN "UpdatedAtUtc" DROP DEFAULT;
            """);
        return conversationId;
    }
}

public sealed class LlmChatPersistenceIntegrationTests
{
    [Fact]
    public async Task Definition_revisions_append_and_preserve_provider_default_versus_explicit_none()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatrevisions");
        await using var dbContext = database.CreateDbContext();
        var repository = new EfLlmChatDefinitionRepository(dbContext);
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
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

public sealed class LlmChatBoundedReadModelIntegrationTests
{
    [Fact]
    public async Task Large_transcript_and_collection_reads_remain_keyset_bounded_with_constant_query_counts()
    {
        const int messageCount = 2_000;
        const int contextLimit = 12;
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatboundedreads");
        var seeded = await SeedAsync(database, messageCount);
        var interceptor = new QueryCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var dbContext = new AppDbContext(options);

        var definitionStore = new EfLlmChatDefinitionReadStore(dbContext);
        var definitions = await definitionStore.ListPageAsync(10, null, null);

        Assert.Equal(10, definitions.Items.Count);
        Assert.NotNull(definitions.NextCursor);
        Assert.Equal(2, interceptor.Commands.Count);
        Assert.All(definitions.Items, item => Assert.Equal(item.Definition.CurrentRevision, item.Revision.Revision));

        interceptor.Clear();
        var nextDefinitions = await definitionStore.ListPageAsync(10, definitions.NextCursor, null);

        Assert.Equal(10, nextDefinitions.Items.Count);
        Assert.Empty(definitions.Items.Select(item => item.Definition.Id)
            .Intersect(nextDefinitions.Items.Select(item => item.Definition.Id)));
        Assert.Equal(2, interceptor.Commands.Count);

        interceptor.Clear();
        var conversationStore = new EfLlmChatConversationReadStore(dbContext);
        var conversations = await conversationStore.ListPageAsync(10, null, null);

        Assert.Single(conversations.Items);
        Assert.Equal(messageCount, conversations.Items[0].Transcript.TranscriptRevision);
        Assert.Single(interceptor.Commands);

        interceptor.Clear();
        var transcript = await conversationStore.TryGetTranscriptPageAsync(seeded.ConversationId, 25, null);

        Assert.NotNull(transcript);
        Assert.Equal(25, transcript.Entries.Count);
        Assert.NotNull(transcript.NextCursor);
        Assert.Equal(seeded.SystemEntryId, transcript.Entries[0].EntryId);
        Assert.Equal(2, interceptor.Commands.Count);
        Assert.Contains(interceptor.Commands, command =>
            command.CommandText.Contains("LlmChats_Messages", StringComparison.Ordinal) &&
            command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));

        interceptor.Clear();
        var provider = CreateProvider(seeded.ProviderId);
        var conversationService = new LlmConversationService(
            new NotInvokedLlmInvocationPort(),
            new EfLlmConversationStore(dbContext),
            new EfLlmConversationTurnStore(dbContext),
            new RecencyBoundedContextWindowPolicy(),
            TimeProvider.System,
            new LlmConversationServiceOptions
            {
                MaximumContextWindowMessages = contextLimit
            });
        var admission = await conversationService.ResumeAdmittedTurnAsync(
            new LlmConversationAdmittedTurnRequest(
                seeded.ConversationId.Value,
                seeded.PendingTurnId,
                provider,
                "model"));

        Assert.Equal(contextLimit, admission.InvocationRequest.Messages.Count());
        Assert.Equal(LlmMessageRole.System, admission.InvocationRequest.Messages[0].Role);
        Assert.Equal("pending", admission.InvocationRequest.Messages[^1].Text);
        Assert.Equal(messageCount, admission.PersistedEntryCount);
        Assert.Equal(3, interceptor.Commands.Count);
        Assert.Equal(2, interceptor.Commands.Count(command =>
            command.CommandText.Contains("LlmChats_Messages", StringComparison.Ordinal)));
        Assert.All(
            interceptor.Commands.Where(command => command.CommandText.Contains("LlmChats_Messages", StringComparison.Ordinal)),
            command => Assert.Contains("LIMIT", command.CommandText, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<SeededBoundedConversation> SeedAsync(
        LlmChatsPostgreSqlTestDatabase database,
        int messageCount)
    {
        await using var dbContext = database.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var providerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var definitionIds = Enumerable.Range(0, 24).Select(_ => Guid.NewGuid()).ToArray();
        for (var index = 0; index < definitionIds.Length; index++)
        {
            var definitionId = definitionIds[index];
            var updatedAt = now.AddMinutes(-index);
            dbContext.Add(new LlmChatDefinitionRow
            {
                Id = definitionId,
                Name = $"Definition {index:D2}",
                Summary = "Summary",
                AvatarImageUrl = "",
                Status = LlmChatDefinitionStatus.Active,
                CurrentRevision = 1,
                CreatedAtUtc = updatedAt,
                UpdatedAtUtc = updatedAt,
                ConcurrencyToken = 0
            });
            var revision = LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
                definitionId,
                1,
                AgentReasoningEffortLevel.None,
                updatedAt);
            revision.SettingsFingerprint = LlmChatFingerprints.CreateSettings(
                revision.ProviderProfileId,
                revision.ProviderKind,
                revision.Model,
                new LlmModelSettings(revision.Temperature, revision.ModelParameterConfigurationJson)
                {
                    ThinkingEffort = revision.ThinkingEffort
                }).Value;
            dbContext.Add(revision);
            dbContext.Add(new LlmChatDefinitionTagRow
            {
                DefinitionId = definitionId,
                Tag = $"tag-{index:D2}"
            });
        }

        var conversationId = LlmChatConversationId.New();
        var pendingTurnId = Guid.NewGuid();
        var pendingEntryId = Guid.NewGuid();
        var systemEntryId = Guid.NewGuid();
        dbContext.Add(new LlmChatConversationRow
        {
            Id = conversationId.Value,
            DefinitionId = definitionIds[0],
            DefinitionRevision = 1,
            Title = "Large transcript",
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        dbContext.Add(new LlmChatTranscriptRow
        {
            ConversationId = conversationId.Value,
            ProviderId = providerId,
            ProviderName = "Provider",
            ProviderKind = ProviderKind.OpenAi,
            Model = "model",
            TranscriptRevision = messageCount,
            EntryCount = messageCount,
            ActiveTurnId = pendingTurnId,
            PendingUserEntryId = pendingEntryId,
            TurnAdmittedAtUtc = now,
            TurnAdmittedRevision = messageCount
        });
        dbContext.Add(new LlmChatMessageRow
        {
            EntryId = systemEntryId,
            ConversationId = conversationId.Value,
            Sequence = 1,
            TurnId = Guid.NewGuid(),
            Role = LlmMessageRole.System,
            Text = "System",
            CreatedAtUtc = now.AddMinutes(-messageCount),
            Model = ""
        });
        for (var sequence = 2; sequence < messageCount; sequence++)
        {
            var turnNumber = (sequence - 2) / 2;
            var turnId = DeterministicGuid(turnNumber + 1);
            var assistant = sequence % 2 != 0;
            dbContext.Add(new LlmChatMessageRow
            {
                EntryId = Guid.NewGuid(),
                ConversationId = conversationId.Value,
                Sequence = sequence,
                TurnId = turnId,
                Role = assistant ? LlmMessageRole.Assistant : LlmMessageRole.User,
                Text = $"message-{sequence:D4}",
                CreatedAtUtc = now.AddSeconds(sequence - messageCount),
                Model = assistant ? "model" : "",
                InputTokens = assistant ? 1 : null,
                OutputTokens = assistant ? 1 : null,
                CachedInputTokens = assistant ? 0 : null
            });
        }

        dbContext.Add(new LlmChatMessageRow
        {
            EntryId = pendingEntryId,
            ConversationId = conversationId.Value,
            Sequence = messageCount,
            TurnId = pendingTurnId,
            Role = LlmMessageRole.User,
            Text = "pending",
            CreatedAtUtc = now,
            Model = ""
        });
        await dbContext.SaveChangesAsync();
        return new SeededBoundedConversation(
            conversationId,
            providerId,
            pendingTurnId,
            systemEntryId);
    }

    private static ProviderProfile CreateProvider(Guid providerId)
        => new(
            providerId,
            "Provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "TEST_API_KEY",
            "model",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: "",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["model"]);

    private static Guid DeterministicGuid(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private sealed class NotInvokedLlmInvocationPort : ILlmInvocationPort
    {
        public Task<LlmInvocationResult> InvokeAsync(
            LlmInvocationRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The bounded context proof must not invoke a provider.");
    }

    private sealed record CapturedCommand(string CommandText);

    private sealed class QueryCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();

        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public void Clear()
        {
            while (commands.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Enqueue(new CapturedCommand(command.CommandText));
            return ValueTask.FromResult(result);
        }
    }

    private sealed record SeededBoundedConversation(
        LlmChatConversationId ConversationId,
        Guid ProviderId,
        Guid PendingTurnId,
        Guid SystemEntryId);
}

public sealed class LlmChatOperationDispatchClaimIntegrationTests
{
    [Fact]
    public async Task Independent_postgresql_services_admit_once_and_claim_execution_lease_once()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatoperationclaim");
        var conversationId = await SeedConversationAsync(database);
        var operation = new LlmChatOperation(
            new LlmChatOperationId(Guid.NewGuid()),
            conversationId,
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('b', 64)),
            0,
            LlmChatOperationStatus.Pending,
            DateTimeOffset.UtcNow,
            0)
        {
            TurnAdmittedAtUtc = DateTimeOffset.UtcNow
        };

        await using (var seedContext = database.CreateDbContext())
        {
            var admission = await new EfLlmChatOperationRepository(seedContext).AdmitAsync(operation);
            Assert.True(admission.Created);
        }

        var options = new LlmChatExecutionLeaseOptions();
        await using var firstProvider = CreateExecutionLeaseProvider(database, options);
        await using var secondProvider = CreateExecutionLeaseProvider(database, options);
        using var firstScope = firstProvider.CreateScope();
        using var secondScope = secondProvider.CreateScope();
        var firstLeaseService = firstScope.ServiceProvider.GetRequiredService<LlmChatExecutionLeaseService>();
        var secondLeaseService = secondScope.ServiceProvider.GetRequiredService<LlmChatExecutionLeaseService>();
        var claims = await Task.WhenAll(
            firstLeaseService.TryClaimAsync(operation.Id, LlmChatExecutionOwnerId.New()),
            secondLeaseService.TryClaimAsync(operation.Id, LlmChatExecutionOwnerId.New()));

        var winner = Assert.Single(claims, claim => claim.Claimed);
        Assert.Equal(LlmChatOperationStatus.Running, winner.Operation!.Status);
        Assert.Equal(1, winner.Operation.ConcurrencyToken);
        Assert.Equal(1, winner.Operation.ExecutionEpoch);
        await using var readContext = database.CreateDbContext();
        var firstRepository = new EfLlmChatOperationRepository(readContext);
        var stored = await firstRepository.TryGetAsync(operation.Id);
        Assert.NotNull(stored);
        Assert.Equal(LlmChatOperationStatus.Running, stored.Status);
        Assert.Equal(1, stored.ConcurrencyToken);
    }

    [Fact]
    public async Task Remote_cancellation_is_observed_by_the_current_owner_heartbeat()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatremotecancel");
        var conversationId = await SeedConversationAsync(database);
        var now = DateTimeOffset.UtcNow;
        var operation = new LlmChatOperation(
            LlmChatOperationId.New(),
            conversationId,
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('c', 64)),
            0,
            LlmChatOperationStatus.Pending,
            now,
            0)
        {
            TurnAdmittedAtUtc = now
        };
        await using (var seedContext = database.CreateDbContext())
        {
            await new EfLlmChatOperationRepository(seedContext).AdmitAsync(operation);
        }

        var options = new LlmChatExecutionLeaseOptions();
        await using var ownerProvider = CreateExecutionLeaseProvider(database, options);
        using var ownerScope = ownerProvider.CreateScope();
        var claim = await ownerScope.ServiceProvider
            .GetRequiredService<LlmChatExecutionLeaseService>()
            .TryClaimAsync(operation.Id, LlmChatExecutionOwnerId.New());
        Assert.True(claim.Claimed);

        await using (var remoteContext = database.CreateDbContext())
        {
            var remoteRepository = new EfLlmChatOperationRepository(remoteContext);
            var remoteUnitOfWork = new EfLlmChatUnitOfWork(
                remoteContext,
                UnfencedLlmChatCommitFence.Instance);
            var remoteEvidence = new LlmChatOperationEvidenceService(
                remoteRepository,
                new EfLlmChatInvocationRecordRepository(remoteContext),
                remoteUnitOfWork,
                new LlmChatOperationScopeAccessor(),
                TimeProvider.System);
            await remoteEvidence.RequestCancellationAsync(operation.Id, DateTimeOffset.UtcNow);
        }

        var heartbeat = new DatabaseProfileLlmChatExecutionLeaseHeartbeatStore(
            new LlmChatTestDbContextFactory(database),
            UnfencedDatabaseRuntimeWriteFence.Instance);
        var observedAt = DateTimeOffset.UtcNow;
        var observation = await heartbeat.RenewAndObserveAsync(
            claim.Lease!.Value,
            new LlmChatRuntimeIdentity(Guid.NewGuid(), "test-runtime", 0),
            observedAt,
            observedAt + options.LeaseDuration);

        Assert.True(observation.IsCurrentOwner);
        Assert.True(observation.CancellationRequested);
    }

    private static ServiceProvider CreateExecutionLeaseProvider(
        LlmChatsPostgreSqlTestDatabase database,
        LlmChatExecutionLeaseOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => database.CreateDbContext());
        services.AddScoped<ILlmChatOperationRepository, EfLlmChatOperationRepository>();
        services.AddScoped<ILlmChatCommitFence>(_ => UnfencedLlmChatCommitFence.Instance);
        services.AddScoped<ILlmChatUnitOfWork, EfLlmChatUnitOfWork>();
        services.AddScoped<LlmChatExecutionLeaseService>();
        return services.BuildServiceProvider();
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

    public static LlmChatsPostgreSqlTestDatabase CreateUnmigrated(string key)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        return new LlmChatsPostgreSqlTestDatabase(PostgresTestDatabaseLease.Create(key));
    }

    public AppDbContext CreateDbContext()
        => new(lease.CreateAppDbContextOptions());

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

    public static void SeedConversationRoot(
        AppDbContext dbContext,
        LlmConversationDocument document)
    {
        var definitionId = Guid.NewGuid();
        dbContext.Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Store integration",
            Summary = "",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            ConcurrencyToken = 0
        });
        dbContext.Add(CreateRevisionRow(
            definitionId,
            1,
            AgentReasoningEffortLevel.None,
            document.CreatedAtUtc));
        dbContext.Add(new LlmChatConversationRow
        {
            Id = document.ConversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 1,
            Title = document.Title,
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            ConcurrencyToken = 0
        });
    }

    public static LlmChatTranscriptRow CreateTranscriptRow(Guid conversationId, DateTimeOffset now)
        => new()
        {
            ConversationId = conversationId,
            ProviderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderName = "Provider",
            ProviderKind = ProviderKind.OpenAi,
            Model = "model",
            TranscriptRevision = 1,
            EntryCount = 1
        };

}
