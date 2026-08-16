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
        services.TryAddSingleton(new LlmChatExecutionLeaseOptions());
        services.TryAddSingleton(new LlmChatTransferOptions());
        var streamingOptions = new LlmChatStreamingOptions();
        streamingOptions.Validate();
        services.TryAddSingleton(streamingOptions);
        services.TryAddSingleton<ILlmChatOperationEventSignal, LlmChatOperationEventSignal>();
        services.TryAddSingleton<LlmChatOperationEventRetentionSchedule>();
        services.TryAddSingleton<LlmChatOperationDispatchSignal>();
        services.TryAddSingleton<ILlmChatOperationDispatchSignal>(provider =>
            provider.GetRequiredService<LlmChatOperationDispatchSignal>());
        services.TryAddSingleton<ILlmChatOperationCancellationRegistry, LlmChatOperationCancellationRegistry>();
        services.AddScoped<ILlmChatOperationEvidenceSink, LlmChatOperationEvidenceService>();
        services.AddScoped<LlmChatOperationEventJournal>();
        services.AddScoped<LlmChatOperationEventStreamSessionFactory>();
        services.AddScoped<LlmChatStreamingConsumerState>();
        services.AddScoped<LlmChatStreamingPipeline>();
        services.AddScoped<LlmChatOperationEventRetentionService>();
        services.AddScoped<LlmChatProfileScopeRunner>();
        services.AddScoped<LlmChatOperationAdmissionService>();
        services.AddScoped<LlmChatOperationStateMachine>();
        services.AddScoped<LlmChatExecutionLeaseService>();
        services.AddScoped<LlmChatOperationExecutor>();
        services.AddScoped<LlmChatOperationDispatcher>();
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
