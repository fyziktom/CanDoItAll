using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Composition;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eServiceHost : IAsyncDisposable
{
    private static readonly ServiceProviderOptions ServiceProviderOptions = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    private const string HostBindingEnvironmentVariable = "CANDOITALL_HOST_BINDING_ID";
    private const string ApiTrustRealmPrefix = "CanDoItAll.SharedProviders.E2E";

    private readonly string? previousHostBindingId;
    private bool disposed;

    private E2eServiceHost(
        ServiceProvider services,
        string? previousHostBindingId)
    {
        Services = services;
        this.previousHostBindingId = previousHostBindingId;
    }

    public ServiceProvider Services { get; }

    public static async Task<E2eServiceHost> CreateAsync(
        E2eOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var previousBinding = Environment.GetEnvironmentVariable(HostBindingEnvironmentVariable);
        Environment.SetEnvironmentVariable(HostBindingEnvironmentVariable, options.HostBindingId);

        ServiceProvider? serviceProvider = null;
        var initializationStage = E2eServiceHostInitializationStage.InstanceDirectories;
        try
        {
            EnsureInstanceDirectories(options);
            initializationStage = E2eServiceHostInitializationStage.SecretFiles;
            var connectionString = await E2eSecretFile.ReadRequiredAsync(
                options.DatabaseConnectionStringFilePath,
                "database connection string",
                cancellationToken);
            var signingKey = await E2eSecretFile.ReadRequiredAsync(
                options.ApiSigningKeyFilePath,
                "API signing key",
                cancellationToken);

            initializationStage = E2eServiceHostInitializationStage.Configuration;
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(BuildConfiguration(options, connectionString, signingKey))
                .Build();
            var services = new ServiceCollection();
            var environment = new E2eHostEnvironment(options.InstanceRootPath);

            initializationStage = E2eServiceHostInitializationStage.ServiceRegistration;
            services.AddSingleton<IWebHostEnvironment>(environment);
            services.AddCanDoItAllInteractiveServer(detailedErrors: false);
            services.AddCanDoItAllBaseLib();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.None);
            });
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton(options);
            services.AddSingleton<E2eHostApplicationLifetime>();
            services.AddSingleton<IHostApplicationLifetime>(provider =>
                provider.GetRequiredService<E2eHostApplicationLifetime>());
            services.AddCanDoItAllInfrastructure(
                configuration,
                environment,
                CanDoItAll.Web.Composition.ModuleAssemblies.All);
            services.AddCanDoItAllRuntimeModules(configuration, environment);
            services.AddCanDoItAllRuntimeDatabaseSwitching();
            services.AddCanDoItAllApi(configuration);
            services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
            services.AddSingleton<E2eArtifactStore>();
            services.AddScoped<E2eSnapshotService>();
            services.AddScoped<E2eOrchestrator>();

            initializationStage = E2eServiceHostInitializationStage.ServiceProvider;
            serviceProvider = services.BuildServiceProvider(ServiceProviderOptions);
            await using var scope = serviceProvider.CreateAsyncScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
            initializationStage = E2eServiceHostInitializationStage.DatabaseBootstrap;
            await bootstrapper.EnsureCurrentProfileReadyAsync(cancellationToken);
            return new E2eServiceHost(serviceProvider, previousBinding);
        }
        catch (E2eSafeException)
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }

            Environment.SetEnvironmentVariable(HostBindingEnvironmentVariable, previousBinding);
            throw;
        }
        catch (Exception exception)
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }

            Environment.SetEnvironmentVariable(HostBindingEnvironmentVariable, previousBinding);
            var failureType = exception.GetBaseException().GetType().Name;
            throw new E2eSafeException(
                $"The role-bound E2E service host could not be initialized. " +
                $"Stage={initializationStage}; FailureType={failureType}.",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            await Services.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                HostBindingEnvironmentVariable,
                previousHostBindingId);
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildConfiguration(
        E2eOptions options,
        string connectionString,
        string signingKey)
    {
        var workspaceRoot = Path.Combine(options.InstanceRootPath, "workspace");
        var apiTrustRealm = ResolveApiTrustRealm(options.Role);
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Provider"] = "PostgreSql",
            ["Database:ConnectionString"] = connectionString,
            ["Database:EnableEntityFrameworkConsoleLogging"] = "false",
            ["ControlPlane:RootPath"] = Path.Combine(options.InstanceRootPath, "control-plane"),
            ["ControlPlane:StateRootPath"] = Path.Combine(options.InstanceRootPath, "state"),
            ["ControlPlane:LogsRootPath"] = Path.Combine(options.InstanceRootPath, "logs"),
            ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(options.InstanceRootPath, "runtime"),
            ["Storage:WorkspaceRoot"] = workspaceRoot,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = Path.Combine(options.InstanceRootPath, "manager-artifacts"),
            ["SecretVault:Provider"] = "DataProtectionFile",
            ["SecretVault:UsageProfile"] = "Headless",
            ["SecretVault:VaultPath"] = Path.Combine(options.InstanceRootPath, "secrets"),
            ["SecretVault:AllowInsecureDevelopmentProviders"] = "true",
            ["DataProtection:KeyProtection:Provider"] = "UnprotectedDevelopment",
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = $"candoitall.shared-providers.e2e.{options.HostBindingId}",
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407",
            ["AgentFramework:ProcessMockAgents:Enabled"] = "false",
            ["Workflows:ExampleSeed:Enabled"] = "false",
            ["Workflows:ExampleSeed:SeedSampleWorkspaceFiles"] = "false",
            ["Memory:BackgroundWorkers:Enabled"] = "false",
            ["Automation:Runtime:Mqtt:Enabled"] = "false",
            ["Processes:Runtime:RecoverActiveRunsOnStartup"] = "false",
            ["Processes:Runtime:ResumePersistedAutomationDispatchesOnStartup"] = "false",
            ["Api:Enabled"] = "true",
            ["Api:OpenApiEnabled"] = "false",
            ["Api:SwaggerUiEnabled"] = "false",
            ["Api:ServerSentEvents:ReplayCapacity"] = "64",
            ["Api:ServerSentEvents:MaxBatchSize"] = "16",
            ["Api:ServerSentEvents:HeartbeatIntervalSeconds"] = "5",
            ["Api:Authorization:Enabled"] = "true",
            ["Api:Authorization:Issuer"] = apiTrustRealm,
            ["Api:Authorization:Audience"] = apiTrustRealm,
            ["Api:Authorization:SigningKey"] = signingKey,
            ["Api:Authorization:DefaultTokenLifetimeMinutes"] = "1440",
            ["Api:Authorization:MaxTokenLifetimeMinutes"] = "10080"
        };
    }

    private static string ResolveApiTrustRealm(E2eRole role) => role switch
    {
        E2eRole.Central => $"{ApiTrustRealmPrefix}.Central",
        E2eRole.ClientA => $"{ApiTrustRealmPrefix}.ClientA",
        E2eRole.ClientB => $"{ApiTrustRealmPrefix}.ClientB",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static void EnsureInstanceDirectories(E2eOptions options)
    {
        var workspaceRoot = Path.Combine(options.InstanceRootPath, "workspace");
        Directory.CreateDirectory(options.InstanceRootPath);
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "managed-files"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "exports"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "evidence"));
        Directory.CreateDirectory(Path.Combine(options.InstanceRootPath, "manager-artifacts"));
    }

    private enum E2eServiceHostInitializationStage
    {
        InstanceDirectories,
        SecretFiles,
        Configuration,
        ServiceRegistration,
        ServiceProvider,
        DatabaseBootstrap
    }
}

internal sealed class E2eHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "CanDoItAll.SharedProviders.E2E";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } =
        new PhysicalFileProvider(contentRootPath);

    public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class E2eHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource started = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly CancellationTokenSource stopped = new();

    public CancellationToken ApplicationStarted => started.Token;

    public CancellationToken ApplicationStopping => stopping.Token;

    public CancellationToken ApplicationStopped => stopped.Token;

    public void StopApplication()
    {
        if (!stopping.IsCancellationRequested)
        {
            stopping.Cancel();
        }
    }

    public void Dispose()
    {
        started.Dispose();
        stopping.Dispose();
        stopped.Dispose();
    }
}
