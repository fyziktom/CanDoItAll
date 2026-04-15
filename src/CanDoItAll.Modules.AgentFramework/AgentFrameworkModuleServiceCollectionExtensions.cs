using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workspace;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public static class AgentFrameworkModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkModule(this IServiceCollection services)
    {
        services.AddSingleton<IProviderProfileService, ProviderProfileService>();
        services.AddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.AddSingleton<IAgentProviderCredentialResolver, SecretStoreAgentProviderCredentialResolver>();
        services.AddScoped<ISandboxWorkspaceStore>(serviceProvider =>
        {
            var workspaceRoot = serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var profile = serviceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
            var scope = WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N"));
            return new FileSandboxWorkspaceStore(workspaceRoot, scope);
        });
        services.AddScoped<IProviderProfileRegistry, WorkspaceBackedAgentProviderProfileRegistry>();
        services.AddScoped<ICanDoItAllAgentWorkspaceFactory, CanDoItAllAgentWorkspaceFactory>();
        services.AddScoped<CanDoItAllAgentWorkspaceFactory>(serviceProvider =>
            (CanDoItAllAgentWorkspaceFactory)serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>());
        services.AddScoped<IAgentFrameworkWorkspaceService>(serviceProvider =>
            serviceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>().GetOrganizationWorkspaceService());
        services.AddScoped<ScenarioHarnessService>();
        services.AddScoped<IProviderRuntimeGateway, AgentFrameworkProviderRuntimeGateway>();
        services.AddScoped<IAiTechnicalAgentBridge, AgentFrameworkAiTechnicalAgentBridge>();
        return services;
    }
}

public static class AgentFrameworkModuleAssemblyMarker;
