using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.DatabaseTransfer;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Modules.LlmChats.Persistence.ReadModels;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public static class LlmChatsPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddLlmChatsPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProviderBackedLlmInvocationPort();
        services.TryAddSingleton<IProviderModelCapabilityResolver, ProviderModelCapabilityResolver>();
        services.TryAddSingleton<ILlmConversationContextWindowPolicy, RecencyBoundedContextWindowPolicy>();
        services.TryAddSingleton<ILlmChatRuntimeLeaseFactory, DatabaseProfileLlmChatRuntimeLeaseFactory>();
        services.TryAddSingleton<ILlmChatOperationScopeAccessor, LlmChatOperationScopeAccessor>();
        services.TryAddSingleton<CanonicalLlmChatProviderResolver>();
        services.TryAddSingleton<ILlmChatProviderResolver>(serviceProvider =>
            serviceProvider.GetRequiredService<CanonicalLlmChatProviderResolver>());
        services.AddScoped<ILlmChatDefinitionRepository, EfLlmChatDefinitionRepository>();
        services.AddScoped<ILlmChatDefinitionReadStore, EfLlmChatDefinitionReadStore>();
        services.AddScoped<ILlmChatConversationRepository, EfLlmChatConversationRepository>();
        services.AddScoped<ILlmChatConversationReadStore, EfLlmChatConversationReadStore>();
        services.AddScoped<ILlmChatOperationRepository, EfLlmChatOperationRepository>();
        services.AddScoped<ILlmChatOperationReadStore, EfLlmChatOperationReadStore>();
        services.AddScoped<ILlmChatTurnStateRepository, EfLlmChatTurnStateRepository>();
        services.AddScoped<ILlmChatInvocationRecordRepository, EfLlmChatInvocationRecordRepository>();
        services.AddScoped<ILlmChatCommitFence, DatabaseProfileLlmChatCommitFence>();
        services.AddSingleton<ILlmChatExecutionLeaseHeartbeatStore,
            DatabaseProfileLlmChatExecutionLeaseHeartbeatStore>();
        services.AddScoped<ILlmChatUnitOfWork, EfLlmChatUnitOfWork>();
        services.AddScoped<ILlmChatConversationEngine>(CreateConversationEngine);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDatabaseTransferHandler, LlmChatsDatabaseTransferHandler>());
        return services;
    }

    private static LlmChatConversationEngine CreateConversationEngine(IServiceProvider serviceProvider)
    {
        var operationScope = serviceProvider.GetRequiredService<ILlmChatOperationScopeAccessor>();
        var runtimeState = serviceProvider.GetRequiredService<IDatabaseRuntimeState>();
        var evidenceSink = serviceProvider.GetRequiredService<ILlmChatOperationEvidenceSink>();
        ILlmInvocationPort invocationPort = new AuditedLlmChatInvocationPort(
            serviceProvider.GetRequiredService<ILlmInvocationPort>(),
            evidenceSink,
            serviceProvider.GetRequiredService<IProviderModelCapabilityResolver>(),
            operationScope,
            serviceProvider.GetRequiredService<TimeProvider>());
        invocationPort = new ProfileFencedLlmChatInvocationPort(
            invocationPort,
            runtimeState,
            operationScope);
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        var conversationStore = new ProfileFencedLlmConversationStore(
            new EfLlmConversationStore(dbContext),
            new EfLlmConversationTurnStore(dbContext),
            runtimeState,
            operationScope);
        var conversationService = new LlmConversationService(
            invocationPort,
            conversationStore,
            conversationStore,
            serviceProvider.GetRequiredService<ILlmConversationContextWindowPolicy>(),
            serviceProvider.GetRequiredService<TimeProvider>());
        return new LlmChatConversationEngine(
            conversationService,
            invocationPort,
            serviceProvider.GetRequiredService<ILlmChatConversationReadStore>(),
            serviceProvider.GetRequiredService<CanonicalLlmChatProviderResolver>(),
            serviceProvider.GetRequiredService<ILlmChatRuntimeLeaseFactory>(),
            operationScope);
    }
}
