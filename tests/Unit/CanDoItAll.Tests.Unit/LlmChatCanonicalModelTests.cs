using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;

namespace CanDoItAll.Tests.Unit;

public sealed class LlmChatCanonicalModelTests
{
    [Fact]
    public void Definition_name_and_conversation_title_are_distinct()
    {
        var now = DateTimeOffset.Parse("2026-08-14T12:00:00Z");
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var definition = new LlmChatDefinition(
            definitionId,
            "Support assistant",
            "Answers product questions",
            "https://example.test/support.png",
            LlmChatDefinitionStatus.Active,
            new LlmChatDefinitionRevisionNumber(1),
            now,
            now,
            0);
        var conversation = new LlmChatConversation(
            new LlmChatConversationId(Guid.NewGuid()),
            definitionId,
            new LlmChatDefinitionRevisionNumber(1),
            "Refund for order 42",
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            now,
            now,
            0);

        Assert.Equal("Support assistant", definition.Name);
        Assert.Equal("Refund for order 42", conversation.Title);
        Assert.NotEqual(definition.Name, conversation.Title);
    }

    [Fact]
    public void Definition_revision_rejects_invalid_revision_and_normalizes_values()
    {
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var providerId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new LlmChatDefinitionRevisionNumber(0));

        var revision = new LlmChatDefinitionRevision(
            definitionId,
            new LlmChatDefinitionRevisionNumber(1),
            "  Support assistant  ",
            "  Answers product questions  ",
            "  https://example.test/support.png  ",
            "Be concise.",
            providerId,
            ProviderKind.OpenAi,
            "  Primary OpenAI  ",
            "  gpt-5  ",
            new LlmModelSettings(0.2),
            TimeSpan.FromMinutes(2),
            null,
            DateTimeOffset.Parse("2026-08-14T12:00:00Z"),
            "  Initial revision  ");

        Assert.Equal("Support assistant", revision.Name);
        Assert.Equal("Answers product questions", revision.Summary);
        Assert.Equal("Primary OpenAI", revision.ProviderName);
        Assert.Equal("gpt-5", revision.Model);
        Assert.Equal("Initial revision", revision.Reason);
        Assert.NotEqual(default, revision.SettingsFingerprint);
    }

    [Fact]
    public void Operation_id_is_the_generic_turn_id()
    {
        var value = Guid.NewGuid();
        var operationId = new LlmChatOperationId(value);

        Assert.Equal(value, operationId.ToTurnId());
    }
}
