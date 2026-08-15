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
        services.AddScoped<LlmChatProfileScopeRunner>();
        services.AddScoped<LlmChatOperationAdmissionService>();
        services.AddScoped<LlmChatOperationStateMachine>();
        services.AddScoped<LlmChatOperationDetailsReader>();
        services.AddScoped<LlmChatDefinitionApplicationService>();
        services.AddScoped<LlmChatConversationApplicationService>();
        services.AddScoped<LlmChatOperationApplicationService>();
        services.AddScoped<ILlmChatDefinitionApplicationService, ProfileScopedLlmChatDefinitionApplicationService>();
        services.AddScoped<ILlmChatConversationApplicationService, ProfileScopedLlmChatConversationApplicationService>();
        services.AddScoped<ILlmChatOperationApplicationService, ProfileScopedLlmChatOperationApplicationService>();
        return services;
    }
}
