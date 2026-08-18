namespace CanDoItAll.Web.Api;

internal static class LlmChatsApi
{
    public static RouteGroupBuilder MapLlmChatsApi(this RouteGroupBuilder api)
    {
        api.MapLlmChatDefinitionEndpoints();
        api.MapLlmChatConversationEndpoints();
        return api;
    }
}
