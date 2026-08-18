using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

namespace CanDoItAll.Web.Api;

internal static class LlmChatApiIds
{
    public static bool TryCreateDefinitionId(
        Guid value,
        out LlmChatDefinitionId id,
        out IResult? error)
    {
        if (value == Guid.Empty)
        {
            id = default;
            error = LlmChatApiResults.InvalidRequest("A non-empty definition id is required.");
            return false;
        }

        id = new LlmChatDefinitionId(value);
        error = null;
        return true;
    }

    public static bool TryCreateConversationId(
        Guid value,
        out LlmChatConversationId id,
        out IResult? error)
    {
        if (value == Guid.Empty)
        {
            id = default;
            error = LlmChatApiResults.InvalidRequest("A non-empty conversation id is required.");
            return false;
        }

        id = new LlmChatConversationId(value);
        error = null;
        return true;
    }

    public static bool TryCreateOperationId(
        Guid value,
        out LlmChatOperationId id,
        out IResult? error)
    {
        if (value == Guid.Empty)
        {
            id = default;
            error = LlmChatApiResults.InvalidRequest("A non-empty operation id is required.");
            return false;
        }

        id = new LlmChatOperationId(value);
        error = null;
        return true;
    }
}
