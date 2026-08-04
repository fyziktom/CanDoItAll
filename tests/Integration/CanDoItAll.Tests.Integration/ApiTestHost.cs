using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

internal sealed class ApiTestHost : IAsyncDisposable
{
    private ApiTestHost(
        CanDoItAllTestEnvironment testEnvironment,
        TestDatabaseProfile activeProfile,
        WebApplication app,
        HttpClient client)
    {
        TestEnvironment = testEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        App = app;
        Client = client;
    }

    public string RootPath { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public TestDatabaseProfile ActiveProfile { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<ApiTestHost> CreateAsync(
        bool jwtEnabled,
        Action<IServiceCollection>? configureServices = null,
        bool useInMemoryDatabase = false,
        string? environmentName = null,
        IAgentRuntime? agentRuntimeOverride = null)
    {
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = useInMemoryDatabase
            ? testEnvironment.CreateInMemoryProfile("api-host")
            : testEnvironment.CreatePostgreSqlProfile("api-host");
        var configurationOverrides = new Dictionary<string, string?>
        {
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
            ["Api:Enabled"] = "true",
            ["Api:OpenApiEnabled"] = "true",
            ["Api:SwaggerUiEnabled"] = "true",
            ["Api:ServerSentEvents:ReplayCapacity"] = "64",
            ["Api:ServerSentEvents:MaxBatchSize"] = "16",
            ["Api:ServerSentEvents:HeartbeatIntervalSeconds"] = "5",
            ["Api:Authorization:Enabled"] = jwtEnabled.ToString(),
            ["Api:Authorization:Issuer"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:Audience"] = "CanDoItAll.Api.Tests",
            ["Api:Authorization:SigningKey"] = "api-test-signing-key-32-bytes-minimum",
            ["Api:Authorization:DefaultTokenLifetimeMinutes"] = "30",
            ["Api:Authorization:MaxTokenLifetimeMinutes"] = "120"
        };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = environmentName ?? Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });
        builder.Configuration.AddInMemoryCollection(activeProfile.CreateConfigurationValues(configurationOverrides));

        TestApplicationBootstrap.ConfigureDefaultServices(
            builder.Services,
            builder.Configuration,
            builder.Environment,
            registerTestHostApplicationLifetime: false);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddCanDoItAllApi(builder.Configuration);
        configureServices?.Invoke(builder.Services);
        if (agentRuntimeOverride is not null)
        {
            ConfigureAgentRuntimeOverride(
                builder.Services,
                agentRuntimeOverride);
        }

        var app = builder.Build();
        if (agentRuntimeOverride is not null &&
            !ReferenceEquals(
                agentRuntimeOverride,
                app.Services.GetRequiredService<IAgentRuntime>()))
        {
            throw new InvalidOperationException(
                "The API test host did not preserve its explicit agent runtime override.");
        }

        app.Urls.Add("http://127.0.0.1:0");

        var options = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (options.Authorization.Enabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapCanDoItAllApiDocumentation();

        app.MapProjectStructureAgentApi();
        app.MapCanDoItAllApi();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var client = CreateClient(app);
        return new ApiTestHost(testEnvironment, activeProfile, app, client);
    }

    private static void ConfigureAgentRuntimeOverride(
        IServiceCollection services,
        IAgentRuntime runtime)
    {
        services.RemoveAll<IAgentRuntime>();
        services.AddSingleton(runtime);
        services.RemoveAll<IAgentFrameworkWorkspaceService>();
        services.RemoveAll<IAgentPackageService>();
        services.RemoveAll<IProviderDiagnosticsService>();
        services.RemoveAll<IAgentExecutionCheckpointBridge>();
        services.RemoveAll<IAgentExecutionGovernanceBridge>();
        services.RemoveAll<IAgentExecutionEventSink>();
        services.AddScoped<IAgentPackageService>(serviceProvider => new ZipAgentPackageService(
            serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
            ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IProviderDiagnosticsService>(_ => new ProviderDiagnosticsService(runtime));
        services.AddScoped<IAgentExecutionCheckpointBridge>(serviceProvider =>
            new WorkflowBackedAgentExecutionCheckpointBridge(
                serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
                serviceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(),
                ResolveWorkspaceScope(serviceProvider)));
        services.AddScoped<IAgentExecutionGovernanceBridge>(serviceProvider =>
            new DurableAgentExecutionGovernanceBridge(
                serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.AddScoped<IAgentExecutionEventSink, NullAgentExecutionEventSink>();
        services.AddScoped(serviceProvider =>
        {
            var profile = serviceProvider
                .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
                .ResolveCurrentProfile()
                .Profile;
            return new AgentExecutionActivityWorkspaceIdentity(
                profile.Id,
                WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")),
                serviceProvider
                    .GetRequiredService<IAgentExecutionProfileGenerationSource>()
                    .GetGeneration());
        });
        services.AddScoped<IAgentFrameworkWorkspaceService, AgentFrameworkWorkspaceService>();
    }

    private static WorkspaceScopeDescriptor ResolveWorkspaceScope(
        IServiceProvider serviceProvider)
    {
        var profile = serviceProvider
            .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
            .ResolveCurrentProfile();
        return WorkspaceScopeDescriptor.Organization(
            profile.Profile.Id.ToString("N"));
    }

    public HttpClient CreateClient()
    {
        return CreateClient(App);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        await TestEnvironment.DisposeAsync();
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The API test host did not expose any server addresses.");
        return new HttpClient
        {
            BaseAddress = new Uri(addresses.Single()),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
