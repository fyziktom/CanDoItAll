using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.LlmChats.Ui;

public static class LlmChatsUiServiceCollectionExtensions
{
    public static IServiceCollection AddLlmChatsUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ILlmChatUiAuthorizationFacade, LlmChatUiAuthorizationFacade>();
        services.TryAddScoped<ILlmChatDefinitionUiGateway, LlmChatDefinitionUiGateway>();
        services.TryAddScoped<ILlmChatConversationUiGateway, LlmChatConversationUiGateway>();
        services.TryAddScoped<ILlmChatOperationUiGateway, LlmChatOperationUiGateway>();
        services.TryAddScoped<ILlmChatProviderUiGateway, LlmChatProviderUiGateway>();
        services.TryAddScoped<ILlmChatUiEventSessionGateway, LlmChatUiEventSessionGateway>();
        services.TryAddSingleton<ILlmChatOperationProjectionReducer, LlmChatOperationProjectionReducer>();
        return services;
    }
}
