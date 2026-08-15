using CanDoItAll.Modules.LlmChats;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class LlmChatWholeUseCaseProfileScopeTests
{
    [Fact]
    public async Task Profile_switch_after_first_read_rejects_authoritative_return()
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
        conversations.Seed(new LlmChatConversation(
            conversationId,
            definitionId,
            new LlmChatDefinitionRevisionNumber(1),
            "Conversation",
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            now,
            now,
            0));
        await engine.CreateAsync(conversationId, revision, "Conversation");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLlmChatsApplication();
        services.AddSingleton<ILlmChatRuntimeLeaseFactory>(leaseFactory);
        services.AddSingleton<ILlmChatOperationScopeAccessor, LlmChatOperationScopeAccessor>();
        services.AddSingleton<ILlmChatDefinitionRepository>(definitions);
        services.AddSingleton<ILlmChatConversationRepository>(new SwitchingConversationRepository(
            conversations,
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

    public Result EnsureCurrent()
        => IsCurrent
            ? Result.Success()
            : Result.Failure(Error.Failure(
                "Profile changed.",
                LlmChatErrorCodes.RuntimeProfileChanged));

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}

internal sealed class SwitchingConversationRepository(
    ILlmChatConversationRepository inner,
    Action switchProfile) : ILlmChatConversationRepository
{
    public async Task<LlmChatConversation?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.TryGetAsync(id, cancellationToken).ConfigureAwait(false);
        switchProfile();
        return result;
    }

    public Task CreateAsync(
        LlmChatConversation conversation,
        CancellationToken cancellationToken = default)
        => inner.CreateAsync(conversation, cancellationToken);

    public Task ReplaceAsync(
        LlmChatConversation conversation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
        => inner.ReplaceAsync(conversation, expectedConcurrencyToken, cancellationToken);
}
