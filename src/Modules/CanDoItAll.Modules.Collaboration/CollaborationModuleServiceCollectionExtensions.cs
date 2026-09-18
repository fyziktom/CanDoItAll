using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Collaboration;

public static class CollaborationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCollaborationModule(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<CollaborationDbContext>((serviceProvider, optionsBuilder) => {
            var database = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>();
            AppDbContextOptionsConfigurator.Configure(optionsBuilder, database.Profile);
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            CollaborationProjectTransferTargetStateParticipant>());
        services.AddScoped<CollaborationService>();
        return services;
    }
}

public static class CollaborationModuleAssemblyMarker;
