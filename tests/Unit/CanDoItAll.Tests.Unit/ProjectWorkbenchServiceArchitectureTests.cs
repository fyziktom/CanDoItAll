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

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectWorkbenchServiceArchitectureTests
{
    [Fact]
    public void Constructors_preserve_the_storage_boundary_and_enable_extracted_composition()
    {
        var constructors = typeof(ProjectWorkbenchService).GetConstructors();
        var extractedParameterTypes = Assert.Single(
                constructors,
                constructor => constructor.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(ProjectAssetStorageService)))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(IDbContextFactory<AppDbContext>),
                typeof(IClock),
                typeof(ProjectAssetStorageService),
                typeof(ProjectStructureAssemblyService),
                typeof(ProjectWorkbenchRelationService),
                typeof(ProjectWorkbenchLifecycleService),
                typeof(ProjectWorkbenchCommandService),
                typeof(ProjectWorkbenchCrossModuleMutationService),
                typeof(ProjectStructureRuntimeNodeMetadataBoundary)
            ],
            extractedParameterTypes);

        var compatibilityParameterTypes = Assert.Single(
                constructors,
                constructor => constructor.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(IStoragePlacementService)))
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(typeof(IStoragePlacementService), compatibilityParameterTypes[2]);
    }

    [Fact]
    public void Asset_storage_service_owns_the_storage_placement_dependency()
    {
        var constructor = Assert.Single(typeof(ProjectAssetStorageService).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(
            [
                typeof(IStoragePlacementService),
                typeof(ProjectAssetCreationService),
                typeof(ProjectManagedStoragePhysicalIdentityPolicy)
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

    [Theory]
    [InlineData(typeof(IProjectStructureRuntimeLauncher))]
    [InlineData(typeof(ProjectStructureRuntimeLauncher))]
    public void Raw_runtime_resolution_requires_an_explicit_path_authority_mode(Type launcherType)
    {
        var rawResolve = Assert.Single(
            launcherType.GetMethods(),
            method => method.Name == nameof(IProjectStructureRuntimeLauncher.Resolve) &&
                method.GetParameters().Length == 5);

        Assert.False(rawResolve.GetParameters()[^1].HasDefaultValue);
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
        AssertScoped<ProjectAssetStorageService>(services);
        AssertScoped<ProjectWorkbenchService>(services);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(ProjectWorkbenchService) &&
                descriptor.ImplementationFactory is not null &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
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
                services,
                item => item.ServiceType == typeof(IProjectNodeDetailsBridge));
            Assert.Equal(typeof(ProjectNodeDetailsBridge), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void Workbench_assignment_mutation_bridge_wins_regardless_of_module_registration_order()
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
                services,
                item =>
                    item.ServiceType ==
                        typeof(IProjectWorkItemAssignmentMutationBridge));
            Assert.NotNull(descriptor.ImplementationFactory);
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
