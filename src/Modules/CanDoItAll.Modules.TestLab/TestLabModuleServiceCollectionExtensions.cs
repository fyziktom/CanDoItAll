using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.TestLab;

public static class TestLabModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTestLabModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            TestLabProjectTransferTargetStateParticipant>());
        services.AddScoped<TestLabService>();
        return services;
    }
}

public static class TestLabModuleAssemblyMarker;

