using System.Reflection;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Support;

public static class TestApplicationBootstrap
{
    private static readonly ServiceProviderOptions DefaultServiceProviderOptions = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    public static IReadOnlyList<Assembly> ModuleAssemblies => CanDoItAll.Web.Composition.ModuleAssemblies.All;

    public static IConfiguration BuildConfiguration(
        TestDatabaseProfile activeProfile,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(activeProfile.CreateConfigurationValues(overrides))
            .Build();
    }

    public static void ConfigureDefaultServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddSingleton<TestHostApplicationLifetime>();
        services.AddSingleton<IHostApplicationLifetime>(serviceProvider => serviceProvider.GetRequiredService<TestHostApplicationLifetime>());
        services.TryAddSingleton<IJSRuntime, UnsupportedJsRuntime>();
        services.AddCanDoItAllBaseLib();
        services.AddCanDoItAllInfrastructure(configuration, environment, ModuleAssemblies);
        services.AddCanDoItAllRuntimeDatabaseSwitching();
        services.AddMermaidJS();
        services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
        services.AddSecurityModule();
        services.AddWorkspaceModule();
        services.AddProjectsModule();
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddFactoryModule();
        services.AddProcessesModule();
        services.AddValidationModule();
        services.AddTestLabModule();
        services.AddActivityModule();
        services.AddAgentFrameworkModule();
        services.AddAutomationModule();
        services.AddCollaborationModule();
        services.AddCrmHrModule();
    }

    public static async Task<ServiceProvider> BuildServiceProviderAsync(
        TestDatabaseProfile activeProfile,
        string applicationName,
        TestSchemaBootstrapModules schemaModules,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, applicationName);
        var configuration = BuildConfiguration(activeProfile, configurationOverrides);

        ConfigureDefaultServices(services, configuration, environment);
        configureServices?.Invoke(services);

        var serviceProvider = services.BuildServiceProvider(DefaultServiceProviderOptions);
        await InitializeSchemaAsync(serviceProvider, schemaModules, cancellationToken);
        return serviceProvider;
    }

    public static async Task InitializeSchemaAsync(
        IServiceProvider serviceProvider,
        TestSchemaBootstrapModules schemaModules,
        CancellationToken cancellationToken = default)
    {
        _ = schemaModules;

        await using var scope = serviceProvider.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync(cancellationToken);
    }

    private sealed class UnsupportedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            throw new NotSupportedException($"JavaScript interop '{identifier}' is not available in this test harness.");
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            throw new NotSupportedException($"JavaScript interop '{identifier}' is not available in this test harness.");
        }
    }
}
