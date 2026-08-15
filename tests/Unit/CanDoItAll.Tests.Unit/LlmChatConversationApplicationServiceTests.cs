using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatConversationApplicationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-14T12:00:00Z");

    [Fact]
    public async Task Create_pins_the_exact_current_active_revision_and_preserves_trusted_application_origin()
    {
        var definitions = new InMemoryLlmChatDefinitionRepository();
        var definitionService = new LlmChatDefinitionApplicationService(
            definitions,
            definitions,
            new InlineLlmChatUnitOfWork(),
            new StubLlmChatProviderResolver(),
            new FixedTimeProvider(Now));
        var createdDefinition = await LlmChatDefinitionServiceTests.CreateDefinitionAsync(definitionService);
        var activeDefinition = await definitionService.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            createdDefinition.Definition.Id,
            LlmChatDefinitionStatus.Active,
            createdDefinition.Definition.ConcurrencyToken));
        var conversations = new InMemoryLlmChatConversationRepository();
        var engine = new StubLlmChatConversationEngine();
        var service = new LlmChatConversationApplicationService(
            definitions,
            conversations,
            new StubLlmChatConversationReadStore(),
            new StubLlmChatTurnStateRepository(),
            new InlineLlmChatUnitOfWork(),
            engine,
            new FixedTimeProvider(Now));

        var result = await service.CreateAsync(new CreateLlmChatConversationCommand(
            activeDefinition.Value!.Definition.Id,
            "Customer refund",
            LlmChatConversationOrigin.Application));

        Assert.True(result.IsSuccess);
        Assert.Equal(activeDefinition.Value.Definition.CurrentRevision, result.Value!.Conversation.DefinitionRevision);
        Assert.Equal(LlmChatConversationOrigin.Application, result.Value.Conversation.Origin);
        var engineCreate = Assert.Single(engine.Created);
        Assert.Equal(result.Value.Conversation.Id, engineCreate.Id);
        Assert.Equal(result.Value.Conversation.DefinitionRevision, engineCreate.Revision.Revision);
        Assert.Equal("Support assistant", result.Value.DefinitionName);
    }

    [Theory]
    [InlineData(LlmChatDefinitionStatus.Draft)]
    [InlineData(LlmChatDefinitionStatus.Suspended)]
    [InlineData(LlmChatDefinitionStatus.Archived)]
    public async Task Create_rejects_every_non_active_definition(LlmChatDefinitionStatus status)
    {
        var definitions = new InMemoryLlmChatDefinitionRepository();
        var definitionService = new LlmChatDefinitionApplicationService(
            definitions,
            definitions,
            new InlineLlmChatUnitOfWork(),
            new StubLlmChatProviderResolver(),
            new FixedTimeProvider(Now));
        var definition = await LlmChatDefinitionServiceTests.CreateDefinitionAsync(definitionService);
        if (status != LlmChatDefinitionStatus.Draft)
        {
            var active = await definitionService.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
                definition.Definition.Id,
                LlmChatDefinitionStatus.Active,
                definition.Definition.ConcurrencyToken));
            definition = active.Value!;
            var target = await definitionService.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
                definition.Definition.Id,
                status,
                definition.Definition.ConcurrencyToken));
            definition = target.Value!;
        }

        var service = new LlmChatConversationApplicationService(
            definitions,
            new InMemoryLlmChatConversationRepository(),
            new StubLlmChatConversationReadStore(),
            new StubLlmChatTurnStateRepository(),
            new InlineLlmChatUnitOfWork(),
            new StubLlmChatConversationEngine(),
            new FixedTimeProvider(Now));

        var result = await service.CreateAsync(new CreateLlmChatConversationCommand(
            definition.Definition.Id,
            "Blocked",
            LlmChatConversationOrigin.Api));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == LlmChatErrorCodes.DefinitionNotActive);
    }

    [Fact]
    public async Task Archived_conversation_is_read_only()
    {
        var harness = await CreateActiveHarnessAsync();
        var created = await harness.Service.CreateAsync(new CreateLlmChatConversationCommand(
            harness.DefinitionId,
            "Customer refund",
            LlmChatConversationOrigin.Api));
        var archived = await harness.Service.ArchiveAsync(new ArchiveLlmChatConversationCommand(
            created.Value!.Conversation.Id,
            created.Value.Conversation.ConcurrencyToken));

        var rename = await harness.Service.RenameAsync(new RenameLlmChatConversationCommand(
            created.Value.Conversation.Id,
            "Changed",
            archived.Value!.Conversation.ConcurrencyToken,
            archived.Value.Transcript.TranscriptRevision));

        Assert.True(rename.IsFailure);
        Assert.Contains(rename.Errors, error => error.Code == LlmChatErrorCodes.ConversationArchived);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Archive_rejects_active_or_nonterminal_turn_state(
        bool hasActiveTurn,
        bool hasNonterminalOperation)
    {
        var harness = await CreateActiveHarnessAsync(hasActiveTurn, hasNonterminalOperation);
        var created = await harness.Service.CreateAsync(new CreateLlmChatConversationCommand(
            harness.DefinitionId,
            "Customer refund",
            LlmChatConversationOrigin.Api));

        var archived = await harness.Service.ArchiveAsync(new ArchiveLlmChatConversationCommand(
            created.Value!.Conversation.Id,
            created.Value.Conversation.ConcurrencyToken));

        Assert.True(archived.IsFailure);
        Assert.Contains(archived.Errors, error => error.Code == LlmChatErrorCodes.ActiveTurnConflict);
    }

    private static async Task<(LlmChatConversationApplicationService Service, LlmChatDefinitionId DefinitionId)>
        CreateActiveHarnessAsync(
            bool hasActiveTurn = false,
            bool hasNonterminalOperation = false)
    {
        var definitions = new InMemoryLlmChatDefinitionRepository();
        var definitionService = new LlmChatDefinitionApplicationService(
            definitions,
            definitions,
            new InlineLlmChatUnitOfWork(),
            new StubLlmChatProviderResolver(),
            new FixedTimeProvider(Now));
        var definition = await LlmChatDefinitionServiceTests.CreateDefinitionAsync(definitionService);
        var active = await definitionService.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
            definition.Definition.Id,
            LlmChatDefinitionStatus.Active,
            definition.Definition.ConcurrencyToken));
        var service = new LlmChatConversationApplicationService(
            definitions,
            new InMemoryLlmChatConversationRepository(),
            new StubLlmChatConversationReadStore(),
            new StubLlmChatTurnStateRepository(
                hasActiveTurn: hasActiveTurn,
                hasNonterminalOperation: hasNonterminalOperation),
            new InlineLlmChatUnitOfWork(),
            new StubLlmChatConversationEngine(),
            new FixedTimeProvider(Now));
        return (service, active.Value!.Definition.Id);
    }
}
