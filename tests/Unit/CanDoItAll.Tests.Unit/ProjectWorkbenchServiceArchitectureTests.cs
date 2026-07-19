using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Composition;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectWorkbenchServiceArchitectureTests
{
    [Fact]
    public void Constructor_uses_extracted_services_for_cross_module_commands()
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
    }

    [Fact]
    public void Command_service_depends_on_the_prompt_gallery_boundary()
    {
        var constructor = Assert.Single(typeof(ProjectWorkbenchCommandService).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(IDbContextFactory<AppDbContext>),
                typeof(IClock),
                typeof(IPromptGalleryService),
                typeof(ProjectStructureAssemblyService)
            ],
            parameterTypes);
        Assert.DoesNotContain(typeof(PromptsService), parameterTypes);
    }

    [Fact]
    public void Runtime_module_assemblies_include_prompts()
    {
        Assert.Contains(typeof(PromptsModuleAssemblyMarker).Assembly, ModuleAssemblies.All);
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
            descriptor => descriptor.ServiceType == typeof(IProjectStructureProjectionContributor) &&
                descriptor.ImplementationType == typeof(PromptGalleryProjectionContributor) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                descriptor.ImplementationType == typeof(ProjectStructureAgentRuntimeToolProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProjectNodeAssignmentPolicyBridge) &&
                descriptor.ImplementationType == typeof(ProjectNodeAssignmentPolicyBridge) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IProjectWorkbenchSeedService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IMemorySourceGatewayAdapter) &&
                descriptor.ImplementationType == typeof(WorkbenchProjectStructureMemorySourceGatewayAdapter) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        AssertScoped<ProjectMemoryIngestionService>(services);
    }

    [Fact]
    public void Workbench_node_details_bridge_wins_regardless_of_module_registration_order()
    {
        Action<IServiceCollection>[] registrations =
        [
            services =>
            {
                services.AddProjectsModule();
                services.AddWorkbenchModule();
            },
            services =>
            {
                services.AddWorkbenchModule();
                services.AddProjectsModule();
            }
        ];

        foreach (var register in registrations)
        {
            var services = new ServiceCollection();

            register(services);

            var descriptor = Assert.Single(
                services.Where(item => item.ServiceType == typeof(IProjectNodeDetailsBridge)));
            Assert.Equal(typeof(ProjectNodeDetailsBridge), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    private static void AssertScoped<TService>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
