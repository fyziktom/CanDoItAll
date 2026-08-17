using CanDoItAll.Conversations.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public static class SimpleChatsComponentsServiceCollectionExtensions
{
    public static IServiceCollection AddSimpleChatsComponents(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddConversationShell();
        services.TryAddScoped<ILlmChatUiAuthorizationFacade, LlmChatUiAuthorizationFacade>();
        services.TryAddScoped<ILlmChatDefinitionUiGateway, LlmChatDefinitionUiGateway>();
        services.TryAddScoped<ILlmChatConversationUiGateway, LlmChatConversationUiGateway>();
        services.TryAddScoped<ILlmChatOperationUiGateway, LlmChatOperationUiGateway>();
        services.TryAddScoped<ILlmChatProviderUiGateway, LlmChatProviderUiGateway>();
        services.TryAddScoped<ILlmChatUiEventSessionGateway, LlmChatUiEventSessionGateway>();
        services.TryAddSingleton<ILlmChatOperationProjectionReducer, LlmChatOperationProjectionReducer>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IConversationShellContributor, LlmChatConversationShellContributor>());
        return services;
    }
}
