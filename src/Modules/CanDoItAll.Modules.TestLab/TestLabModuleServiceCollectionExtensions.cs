using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.TestLab;

public static class TestLabModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTestLabModule(this IServiceCollection services)
    {
        services.AddPooledDbContextFactory<TestLabDbContext>((serviceProvider, optionsBuilder) => {
            var database = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>();
            AppDbContextOptionsConfigurator.Configure(optionsBuilder, database.Profile);
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            TestLabProjectTransferTargetStateParticipant>());
        services.AddScoped<TestLabService>();
        return services;
    }
}

public static class TestLabModuleAssemblyMarker;

