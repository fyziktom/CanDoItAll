using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
using CanDoItAll.Web.Infrastructure;
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
    private readonly bool ownsTestEnvironment;

    private ApiTestHost(
        CanDoItAllTestEnvironment testEnvironment,
        TestDatabaseProfile activeProfile,
        WebApplication app,
        HttpClient client,
        bool ownsTestEnvironment)
    {
        TestEnvironment = testEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        App = app;
        Client = client;
        this.ownsTestEnvironment = ownsTestEnvironment;
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
        IFakeAgentRuntime? agentRuntimeOverride = null,
        CanDoItAllTestEnvironment? sharedTestEnvironment = null,
        TestDatabaseProfile? sharedActiveProfile = null,
        Action<WebApplication>? configureApplication = null)
    {
        if ((sharedTestEnvironment is null) != (sharedActiveProfile is null))
        {
            throw new ArgumentException(
                "A shared API test environment and active profile must be supplied together.");
        }

        bool ownsTestEnvironment = sharedTestEnvironment is null;
        var testEnvironment = sharedTestEnvironment ??
            CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var activeProfile = sharedActiveProfile ?? (useInMemoryDatabase
            ? testEnvironment.CreateInMemoryProfile("api-host")
            : testEnvironment.CreatePostgreSqlProfile("api-host"));
        if (!ownsTestEnvironment &&
            useInMemoryDatabase != (activeProfile.Provider == TestDatabaseProviderKind.InMemory))
        {
            throw new ArgumentException(
                "The shared API test profile provider does not match the requested database mode.");
        }

        var configurationOverrides = new Dictionary<string, string?>
        {
            ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
            ["ControlPlane:StateRootPath"] = Path.Combine(testEnvironment.RootPath, "state"),
            ["ControlPlane:LogsRootPath"] = Path.Combine(testEnvironment.RootPath, "logs"),
            ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(testEnvironment.RootPath, "runtime"),
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
        string resolvedEnvironmentName = environmentName ?? Environments.Development;
        if (!string.Equals(resolvedEnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            string certificatePath = CreateDataProtectionCertificate(testEnvironment.RootPath);
            configurationOverrides["SecretVault:Provider"] = "Auto";
            configurationOverrides["SecretVault:AllowInsecureDevelopmentProviders"] = "false";
            configurationOverrides["DataProtection:KeyProtection:Provider"] = "Certificate";
            configurationOverrides["DataProtection:KeyProtection:CertificatePath"] = certificatePath;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = testEnvironment.RootPath,
            EnvironmentName = resolvedEnvironmentName,
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
                app.Services.GetRequiredService<IFakeAgentRuntime>()))
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

        configureApplication?.Invoke(app);
        app.UseMiddleware<AccessContextReferenceMiddleware>();

        app.MapCanDoItAllApiDocumentation();

        app.MapCanDoItAllManagedFiles();
        app.MapProjectStructureAgentApi();
        app.MapCanDoItAllApi();

        await TestApplicationBootstrap.InitializeSchemaAsync(app.Services, TestSchemaBootstrapModules.Full);
        await app.StartAsync();

        var client = CreateClient(app);
        return new ApiTestHost(
            testEnvironment,
            activeProfile,
            app,
            client,
            ownsTestEnvironment);
    }

    private static string CreateDataProtectionCertificate(string rootPath)
    {
        string certificatePath = Path.Combine(rootPath, "data-protection-test.pfx");
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=CanDoItAll API integration test",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                certificatePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return certificatePath;
    }

    private static void ConfigureAgentRuntimeOverride(
        IServiceCollection services,
        IFakeAgentRuntime runtime)
    {
        services.RemoveAll<IFakeAgentRuntime>();
        services.RouteRuntimePortsThroughAgentRuntime();
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
        services.AddScoped<IProviderDiagnosticsService>(_ =>
        {
            var portFacade = new FakeAgentRuntimePortAdapter(runtime);
            return new ProviderDiagnosticsService(portFacade, portFacade);
        });
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
        if (ownsTestEnvironment)
        {
            await TestEnvironment.DisposeAsync();
        }
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
