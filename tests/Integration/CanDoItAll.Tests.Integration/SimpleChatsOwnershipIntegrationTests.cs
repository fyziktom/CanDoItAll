using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class SimpleChatsOwnershipIntegrationTests {
    [Fact]
    public async Task Runtime_model_retains_all_ten_complete_schema_mappings() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<SimpleChatsDbContext>();
        var complete = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var completeModel = complete.GetService<IDesignTimeModel>().Model;
        Assert.Equal(10, ownerModel.GetEntityTypes().Count());
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var existing = Assert.IsAssignableFrom<IEntityType>(completeModel.FindEntityType(entity.ClrType));
            Assert.Equal(existing.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    [Fact]
    public async Task Legacy_transcript_survives_restart_and_remains_bound_to_its_profile() {
        await using var environment = CanDoItAllTestEnvironment.Create("simple-chats-owner-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(Guid.NewGuid()) with {
            Title = "Legacy conversation",
            TranscriptRevision = 7
        };
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original };
        await using (var before = await TestApplication.CreateAsync(options)) {
            var factory = before.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var complete = await factory.CreateDbContextAsync();
            LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(complete, document);
            complete.Add(LlmConversationPersistenceMapper.ToRow(document));
            await complete.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var boundary = restartedScope.ServiceProvider.GetRequiredService<ILlmChatRuntimePersistenceBoundary>();
        var restored = await boundary.ConversationStore.TryGetAsync(document.ConversationId);
        Assert.NotNull(restored);
        Assert.Equal(document.Title, restored.Title);
        Assert.Equal(7, restored.TranscriptRevision);
        Assert.Equal(document.Provider, restored.Provider);

        await using var otherApplication = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = other
        });
        await using var otherScope = otherApplication.Services.CreateAsyncScope();
        Assert.Null(await otherScope.ServiceProvider.GetRequiredService<ILlmChatRuntimePersistenceBoundary>()
            .ConversationStore.TryGetAsync(document.ConversationId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scoped_runtime_stores_share_nested_unit_of_work_and_publish_only_after_commit(bool rollback) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<SimpleChatsDbContext>();
        var factory = services.GetRequiredService<IDbContextFactory<SimpleChatsDbContext>>();
        var unitOfWork = services.GetRequiredService<ILlmChatUnitOfWork>();
        var repository = services.GetRequiredService<ILlmChatConversationRepository>();
        var boundary = services.GetRequiredService<ILlmChatRuntimePersistenceBoundary>();
        var runtime = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        using var execution = services.GetRequiredService<ILlmChatOperationScopeAccessor>().Push(new(
            new LlmChatOperationId(Guid.NewGuid()),
            new LlmChatRuntimeIdentity(runtime.ActiveProfileId!.Value, runtime.ActiveFingerprint!, runtime.Generation)));
        var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(Guid.NewGuid());
        var definitionId = Guid.NewGuid();
        owner.Add(new LlmChatDefinitionRow {
            Id = definitionId,
            Name = "Shared scoped context",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.CreatedAtUtc
        });
        owner.Add(LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(definitionId, 1, null, document.CreatedAtUtc));
        await owner.SaveChangesAsync();
        var conversation = new LlmChatConversation(new(document.ConversationId), new(definitionId), new(1),
            document.Title, LlmChatConversationStatus.Active, LlmChatConversationOrigin.Api,
            document.CreatedAtUtc, document.UpdatedAtUtc, 0);
        var callbackObservedCommit = false;

        async Task<bool> WriteAsync(CancellationToken token) {
            await repository.CreateAsync(conversation, token);
            await unitOfWork.ExecuteAsync(async nestedToken => {
                await boundary.ConversationStore.CreateAsync(document, nestedToken);
                unitOfWork.RegisterPostCommit(() => {
                    using var observer = factory.CreateDbContext();
                    Assert.Null(owner.Database.CurrentTransaction);
                    callbackObservedCommit = observer.Set<LlmChatConversationRow>().AsNoTracking()
                        .Any(row => row.Id == document.ConversationId);
                });
                return true;
            }, token);
            Assert.False(callbackObservedCommit);
            if (rollback) {
                throw new InvalidOperationException("Injected failure after the shared runtime store flushed.");
            }

            return true;
        }

        if (rollback) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(WriteAsync));
        } else {
            await unitOfWork.ExecuteAsync(WriteAsync);
        }

        Assert.Equal(!rollback, callbackObservedCommit);
        await using var verification = await factory.CreateDbContextAsync();
        Assert.Equal(!rollback, await verification.Set<LlmChatConversationRow>().AnyAsync(row => row.Id == document.ConversationId));
        Assert.Equal(!rollback, await verification.Set<LlmChatTranscriptRow>().AnyAsync(row => row.ConversationId == document.ConversationId));
    }

    [Fact]
    public async Task Unmanaged_outer_transaction_is_rejected_before_operation_or_callback_registration() {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("simple-chats-unmanaged-transaction");
        await using var owner = database.CreateSimpleChatsDbContext();
        await using var outer = await owner.Database.BeginTransactionAsync();
        var unitOfWork = new EfLlmChatUnitOfWork(owner, UnfencedLlmChatCommitFence.Instance, LlmChatTestPersistence.TransactionsFor(owner));
        var operationInvoked = false;
        var callbackInvoked = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteAsync(_ => {
            operationInvoked = true;
            return Task.FromResult(true);
        }));
        Assert.Throws<InvalidOperationException>(() => unitOfWork.RegisterPostCommit(() => callbackInvoked = true));
        Assert.False(operationInvoked);
        Assert.False(callbackInvoked);
        Assert.Same(outer, owner.Database.CurrentTransaction);
        await outer.RollbackAsync();
    }
}
