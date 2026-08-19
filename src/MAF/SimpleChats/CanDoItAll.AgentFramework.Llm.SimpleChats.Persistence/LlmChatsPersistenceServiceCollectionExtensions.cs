using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.DatabaseTransfer;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Usage;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

public static class LlmChatsPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddLlmChatsPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ILlmChatRuntimeLeaseFactory, DatabaseProfileLlmChatRuntimeLeaseFactory>();
        services.AddScoped<ILlmChatDefinitionRepository, EfLlmChatDefinitionRepository>();
        services.AddScoped<ILlmChatDefinitionReadStore, EfLlmChatDefinitionReadStore>();
        services.AddScoped<ILlmChatConversationRepository, EfLlmChatConversationRepository>();
        services.AddScoped<ILlmChatConversationReadStore, EfLlmChatConversationReadStore>();
        services.AddScoped<ILlmChatOperationRepository, EfLlmChatOperationRepository>();
        services.AddScoped<ILlmChatOperationReadStore, EfLlmChatOperationReadStore>();
        services.AddScoped<ILlmChatProjectStructureReportStore, EfLlmChatProjectStructureReportStore>();
        services.AddScoped<ILlmChatTurnStateRepository, EfLlmChatTurnStateRepository>();
        services.AddScoped<ILlmChatInvocationRecordRepository, EfLlmChatInvocationRecordRepository>();
        services.AddScoped<ILlmChatOperationEventRepository, EfLlmChatOperationEventRepository>();
        services.AddScoped<ILlmChatCommitFence, DatabaseProfileLlmChatCommitFence>();
        services.AddSingleton<ILlmChatExecutionLeaseHeartbeatStore,
            DatabaseProfileLlmChatExecutionLeaseHeartbeatStore>();
        services.AddScoped<ILlmChatUnitOfWork, EfLlmChatUnitOfWork>();
        services.AddScoped<ILlmChatRuntimePersistenceBoundary, DatabaseLlmChatRuntimePersistenceBoundary>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProviderUsageProjectionSource,
            SimpleChatProviderUsageProjectionSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IDatabaseTransferHandler, LlmChatsDatabaseTransferHandler>());
        return services;
    }

}
