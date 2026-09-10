using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class SimpleChatsDbContextTests {
    [Fact]
    public void Runtime_model_maps_only_ten_chat_entities_and_rejects_foreign_queries() {
        using var context = new SimpleChatsDbContext(new DbContextOptionsBuilder<SimpleChatsDbContext>()
            .UseNpgsql("Host=localhost;Database=simple_chats_model")
            .Options);
        Type[] owned = [typeof(LlmChatDefinitionRow), typeof(LlmChatDefinitionRevisionRow), typeof(LlmChatDefinitionTagRow),
            typeof(LlmChatConversationRow), typeof(LlmChatTranscriptRow), typeof(LlmChatMessageRow),
            typeof(LlmChatOperationRow), typeof(LlmChatInvocationRecordRow), typeof(LlmChatOperationEventRow),
            typeof(LlmChatDefinitionCreateReceiptRow)];

        Assert.Equal(owned.OrderBy(type => type.Name), context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToQueryString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_preserves_owner_managed_long_versions(bool asynchronous) {
        await using var context = CreateInMemoryContext();
        var definition = new LlmChatDefinitionRow { Id = Guid.NewGuid(), Name = "Before", ConcurrencyToken = 17 };
        var conversation = new LlmChatConversationRow { Id = Guid.NewGuid(), Title = "Before", ConcurrencyToken = 23 };
        var transcript = new LlmChatTranscriptRow { ConversationId = conversation.Id, TranscriptRevision = 41 };
        var operation = new LlmChatOperationRow { Id = Guid.NewGuid(), ConversationId = conversation.Id, ConcurrencyToken = 59 };
        context.AddRange(definition, conversation, transcript, operation);
        await SaveAsync();
        AssertVersions(17, 23, 41, 59);

        definition.Name = "After";
        conversation.Title = "After";
        operation.FailureCode = "changed";
        transcript.Model = "changed";
        await SaveAsync();
        AssertVersions(17, 23, 41, 59);

        definition.ConcurrencyToken++;
        conversation.ConcurrencyToken++;
        transcript.TranscriptRevision++;
        operation.ConcurrencyToken++;
        await SaveAsync();
        AssertVersions(18, 24, 42, 60);

        Task SaveAsync() {
            if (asynchronous) {
                return context.SaveChangesAsync();
            }

            context.SaveChanges();
            return Task.CompletedTask;
        }

        void AssertVersions(long definitionVersion, long conversationVersion, long transcriptVersion, long operationVersion) {
            Assert.Equal(definitionVersion, definition.ConcurrencyToken);
            Assert.Equal(conversationVersion, conversation.ConcurrencyToken);
            Assert.Equal(transcriptVersion, transcript.TranscriptRevision);
            Assert.Equal(operationVersion, operation.ConcurrencyToken);
        }
    }

    [Theory]
    [InlineData(EvidenceWrite.RevisionModified)]
    [InlineData(EvidenceWrite.RevisionDeleted)]
    [InlineData(EvidenceWrite.InvocationModified)]
    [InlineData(EvidenceWrite.InvocationDeleted)]
    [InlineData(EvidenceWrite.EventModified)]
    [InlineData(EvidenceWrite.ReceiptModified)]
    [InlineData(EvidenceWrite.ReceiptDeleted)]
    public async Task Unit_of_work_preserves_append_only_evidence_checks(EvidenceWrite write) {
        var databaseName = $"simple-chats-append-only-{Guid.NewGuid():N}";
        await using var context = CreateInMemoryContext(databaseName);
        object row = write switch {
            EvidenceWrite.ReceiptModified or EvidenceWrite.ReceiptDeleted => new LlmChatDefinitionCreateReceiptRow {
                Producer = "fixture", Actor = "operator", HistoryNamespace = "history", IntentId = Guid.NewGuid(),
                SemanticVersion = 1, SemanticFingerprint = new string('a', 64), DefinitionId = Guid.NewGuid(), DefinitionRevision = 1
            },
            EvidenceWrite.RevisionModified or EvidenceWrite.RevisionDeleted => new LlmChatDefinitionRevisionRow {
                DefinitionId = Guid.NewGuid(), Revision = 1
            },
            EvidenceWrite.InvocationModified or EvidenceWrite.InvocationDeleted => new LlmChatInvocationRecordRow {
                OperationId = Guid.NewGuid(), Ordinal = 1
            },
            _ => new LlmChatOperationEventRow { OperationId = Guid.NewGuid(), Sequence = 1 }
        };
        context.Add(row);
        await context.SaveChangesAsync();
        context.Entry(row).State = write is EvidenceWrite.RevisionDeleted or EvidenceWrite.InvocationDeleted or EvidenceWrite.ReceiptDeleted
            ? EntityState.Deleted
            : EntityState.Modified;
        var transactions = CoordinatedDatabaseTransaction.ForProfile(new ResolvedDatabaseProfile(
            new DatabaseProfileRecord { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride,
            databaseName));
        var unitOfWork = new EfLlmChatUnitOfWork(context, new DirectFence(), transactions);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(_ => Task.FromResult(true)));
        var expected = write switch {
            EvidenceWrite.ReceiptModified or EvidenceWrite.ReceiptDeleted => "LLM Chat definition create receipts are immutable.",
            EvidenceWrite.RevisionModified or EvidenceWrite.RevisionDeleted => "LLM Chat definition revisions are append-only.",
            EvidenceWrite.InvocationModified or EvidenceWrite.InvocationDeleted => "LLM Chat invocation records are append-only.",
            _ => "LLM Chat operation events cannot be modified after append."
        };
        Assert.Equal(expected, exception.Message);
    }

    private static SimpleChatsDbContext CreateInMemoryContext(string? databaseName = null)
        => new(new DbContextOptionsBuilder<SimpleChatsDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"simple-chats-owner-{Guid.NewGuid():N}")
            .Options);

    public enum EvidenceWrite {
        RevisionModified,
        RevisionDeleted,
        InvocationModified,
        InvocationDeleted,
        EventModified,
        ReceiptModified,
        ReceiptDeleted
    }

    private sealed class DirectFence : ILlmChatCommitFence {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }
}
