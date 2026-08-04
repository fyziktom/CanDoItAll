using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Collaboration;

public static class CollaborationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCollaborationModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            CollaborationProjectTransferTargetStateParticipant>());
        services.AddScoped<CollaborationService>();
        return services;
    }
}

public static class CollaborationModuleAssemblyMarker;
