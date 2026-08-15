using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.LlmChats;

public static class LlmChatsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddLlmChatsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILlmChatOperationCancellationRegistry, LlmChatOperationCancellationRegistry>();
        services.AddScoped<ILlmChatOperationEvidenceSink, LlmChatOperationEvidenceService>();
        services.AddScoped<LlmChatOperationAdmissionService>();
        services.AddScoped<LlmChatOperationStateMachine>();
        services.AddScoped<LlmChatOperationDetailsReader>();
        services.AddScoped<ILlmChatDefinitionApplicationService, LlmChatDefinitionApplicationService>();
        services.AddScoped<ILlmChatConversationApplicationService, LlmChatConversationApplicationService>();
        services.AddScoped<ILlmChatOperationApplicationService, LlmChatOperationApplicationService>();
        return services;
    }
}
