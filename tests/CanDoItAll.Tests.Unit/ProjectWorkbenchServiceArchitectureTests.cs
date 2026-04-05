using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectWorkbenchServiceArchitectureTests
{
    [Fact]
    public void Constructor_uses_extracted_services_and_drops_direct_prompt_factory_dependency()
    {
        var constructor = Assert.Single(typeof(ProjectWorkbenchService).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(IDbContextFactory<AppDbContext>),
                typeof(IClock),
                typeof(IStoragePlacementService),
                typeof(ProjectStructureAssemblyService),
                typeof(ProjectWorkbenchRelationService),
                typeof(ProjectWorkbenchLifecycleService),
                typeof(ProjectWorkbenchCommandService),
                typeof(ProjectWorkbenchCrossModuleMutationService)
            ],
            parameterTypes);
        Assert.DoesNotContain(typeof(PromptFactoryService), parameterTypes);
    }

    [Fact]
    public void AddWorkbenchModule_registers_extracted_workbench_services_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddWorkbenchModule();

        AssertScoped<ProjectWorkbenchRelationService>(services);
        AssertScoped<ProjectWorkbenchLifecycleService>(services);
        AssertScoped<ProjectWorkbenchCommandService>(services);
        AssertScoped<ProjectWorkbenchCrossModuleMutationService>(services);
        AssertScoped<ProjectCrossModuleMutationProcessor>(services);
        AssertScoped<ProjectStructureAssemblyService>(services);
        AssertScoped<ProjectWorkbenchService>(services);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProjectNodeAssignmentPolicyBridge) &&
                descriptor.ImplementationType == typeof(ProjectNodeAssignmentPolicyBridge) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProjectWorkbenchSeedService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static void AssertScoped<TService>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
