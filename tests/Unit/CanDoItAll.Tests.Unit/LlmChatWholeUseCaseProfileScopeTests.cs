using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatWholeUseCaseProfileScopeTests
{
    [Fact]
    public async Task Profile_switch_after_first_read_rejects_active_operation_projection()
    {
        var definitionId = LlmChatDefinitionId.New();
        var conversationId = LlmChatConversationId.New();
        var now = DateTimeOffset.UtcNow;
        var definitions = new InMemoryLlmChatDefinitionRepository();
        var conversations = new InMemoryLlmChatConversationRepository();
        var engine = new StubLlmChatConversationEngine();
        var runtimeLease = new MutableLlmChatRuntimeLease();
        var leaseFactory = new TestLlmChatRuntimeLeaseFactory(runtimeLease);
        var definition = new LlmChatDefinition(
            definitionId,
            "Definition",
            "",
            "",
            LlmChatDefinitionStatus.Active,
            new LlmChatDefinitionRevisionNumber(1),
            now,
            now,
            0);
        var revision = ProviderRuntimeTestData.CreateRevision(
            definitionId,
            1,
            ProviderRuntimeTestData.CreateProvider(),
            null);
        await definitions.CreateAsync(definition, revision);
        var conversation = new LlmChatConversation(
            conversationId,
            definitionId,
            new LlmChatDefinitionRevisionNumber(1),
            "Conversation",
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            now,
            now,
            0);
        conversations.Seed(conversation);
        var transcript = await engine.CreateAsync(conversationId, revision, "Conversation");
        var operationId = LlmChatOperationId.New();
        engine.SeedActiveTurn(conversationId, operationId);
        transcript = transcript with { ActiveOperationId = operationId };
        var providerModel = new LlmConversationProviderSnapshot(
            revision.ProviderProfileId,
            revision.ProviderName,
            revision.ProviderKind,
            revision.Model);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSimpleChatsApplication();
        services.AddSingleton<ILlmChatRuntimeLeaseFactory>(leaseFactory);
        services.AddSingleton<ILlmChatOperationScopeAccessor, LlmChatOperationScopeAccessor>();
        services.AddSingleton<ILlmChatDefinitionRepository>(definitions);
        services.AddSingleton<ILlmChatConversationRepository>(conversations);
        services.AddSingleton<ILlmChatConversationReadStore>(new SwitchingConversationReadStore(
            new LlmChatConversationReadModel(conversation, definition.Name, providerModel, transcript),
            () => runtimeLease.IsCurrent = false));
        services.AddSingleton<ILlmChatTurnStateRepository>(new StubLlmChatTurnStateRepository());
        services.AddSingleton<ILlmChatUnitOfWork>(new InlineLlmChatUnitOfWork());
        services.AddSingleton<ILlmChatConversationEngine>(engine);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider
            .GetRequiredService<ILlmChatConversationApplicationService>()
            .GetAsync(conversationId);

        Assert.True(leaseFactory.Acquired);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(LlmChatErrorCodes.RuntimeProfileChanged, Assert.Single(result.Errors).Code);
        Assert.Null(scope.ServiceProvider.GetRequiredService<ILlmChatOperationScopeAccessor>().Current);
    }
}

internal sealed class TestLlmChatRuntimeLeaseFactory(MutableLlmChatRuntimeLease lease) : ILlmChatRuntimeLeaseFactory
{
    public bool Acquired { get; private set; }

    public ValueTask<ILlmChatRuntimeLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        Acquired = true;
        return ValueTask.FromResult<ILlmChatRuntimeLease>(lease);
    }
}

internal sealed class MutableLlmChatRuntimeLease : ILlmChatRuntimeLease
{
    public LlmChatRuntimeIdentity Identity { get; } = new(
        Guid.Parse("cc000000-0000-0000-0000-000000000001"),
        "profile-fingerprint",
        7);

    public CancellationToken CancellationToken => CancellationToken.None;

    public bool IsCurrent { get; set; } = true;

    public int DisposeCount { get; private set; }

    public Result EnsureCurrent()
        => IsCurrent
            ? Result.Success()
            : Result.Failure(Error.Failure(
                "Profile changed.",
                LlmChatErrorCodes.RuntimeProfileChanged));

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class SwitchingConversationReadStore(
    LlmChatConversationReadModel model,
    Action switchProfile) : ILlmChatConversationReadStore
{
    public Task<LlmChatConversationReadModel?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LlmChatConversationReadModel? result = id == model.Conversation.Id ? model : null;
        switchProfile();
        return Task.FromResult(result);
    }

    public Task<LlmChatPage<LlmChatConversationReadModel, LlmChatConversationCursor>> ListPageAsync(
        int take,
        LlmChatConversationCursor? cursor,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatTranscriptReadModel?> TryGetTranscriptPageAsync(
        LlmChatConversationId id,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationTurnEvidence?> TryInspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
