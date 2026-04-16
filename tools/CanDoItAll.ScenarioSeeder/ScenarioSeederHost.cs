using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal sealed class ScenarioSeederOptions
{
    private const string DefaultManagedProfileRoot = @"C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\fe8c1138e1b541cc97a32dbead3a2394";
    public const string DefaultScenario = "agentframework-integration-simulation";
    public const string AgentShowcaseCalculatorScenario = "agent-showcase-calculator";

    public required string RepositoryRootPath { get; init; }

    public required string ScenarioName { get; init; }

    public required string ProfileRootPath { get; init; }

    public required string DatabasePath { get; init; }

    public required string WorkspaceRootPath { get; init; }

    public required string ManagerArtifactsRootPath { get; init; }

    public required string ConnectionString { get; init; }

    public static ScenarioSeederOptions Parse(string[] args, string currentDirectory)
    {
        var repositoryRootPath = currentDirectory;
        var profileRootPath = DefaultManagedProfileRoot;
        var scenarioName = DefaultScenario;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--repo-root":
                    repositoryRootPath = GetRequiredValue(args, ref index);
                    break;
                case "--profile-root":
                    profileRootPath = GetRequiredValue(args, ref index);
                    break;
                case "--scenario":
                    scenarioName = GetRequiredValue(args, ref index);
                    break;
            }
        }

        var databasePath = Path.Combine(profileRootPath, "db", "candoitall.db");
        var workspaceRootPath = Path.Combine(profileRootPath, "workspace");
        var managerArtifactsRootPath = Path.Combine(profileRootPath, "manager-artifacts");

        return new ScenarioSeederOptions
        {
            RepositoryRootPath = Path.GetFullPath(repositoryRootPath),
            ScenarioName = string.IsNullOrWhiteSpace(scenarioName)
                ? DefaultScenario
                : scenarioName.Trim(),
            ProfileRootPath = Path.GetFullPath(profileRootPath),
            DatabasePath = Path.GetFullPath(databasePath),
            WorkspaceRootPath = Path.GetFullPath(workspaceRootPath),
            ManagerArtifactsRootPath = Path.GetFullPath(managerArtifactsRootPath),
            ConnectionString = $"Data Source={Path.GetFullPath(databasePath)}"
        };
    }

    private static string GetRequiredValue(string[] args, ref int index)
    {
        if (index >= args.Length - 1)
        {
            throw new InvalidOperationException($"Missing value for argument '{args[index]}'.");
        }

        index++;
        return args[index];
    }
}

internal static class ScenarioSeederHost
{
    private static readonly ServiceProviderOptions ServiceProviderOptions = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    public static async Task<ServiceProvider> BuildServiceProviderAsync(
        ScenarioSeederOptions options,
        CancellationToken cancellationToken = default)
    {
        EnsureDirectories(options);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(BuildConfigurationValues(options))
            .Build();

        var services = new ServiceCollection();
        var environment = new SeederHostEnvironment(options.RepositoryRootPath);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(console =>
            {
                console.TimestampFormat = "HH:mm:ss ";
                console.SingleLine = true;
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(options);
        services.AddSingleton<SeederHostApplicationLifetime>();
        services.AddSingleton<IHostApplicationLifetime>(provider => provider.GetRequiredService<SeederHostApplicationLifetime>());
        services.AddCanDoItAllInfrastructure(configuration, environment, CanDoItAll.Web.Composition.ModuleAssemblies.All);
        services.AddCanDoItAllRuntimeDatabaseSwitching();
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
        services.AddAutomationModule();
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        services.AddAgentFrameworkModule();
        services.AddScoped<AgentFrameworkIntegrationSimulationSeeder>();
        services.AddScoped<AgentShowcaseCalculatorSeeder>();

        var serviceProvider = services.BuildServiceProvider(ServiceProviderOptions);
        await using var scope = serviceProvider.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync(cancellationToken);
        return serviceProvider;
    }

    private static Dictionary<string, string?> BuildConfigurationValues(ScenarioSeederOptions options)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = options.ConnectionString,
            ["Storage:WorkspaceRoot"] = options.WorkspaceRootPath,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = options.ManagerArtifactsRootPath,
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = "candoitall.workbench.session",
            ["DevelopmentManager:TuningModeEnabled"] = "true",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
        };
    }

    private static void EnsureDirectories(ScenarioSeederOptions options)
    {
        Directory.CreateDirectory(options.ProfileRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(options.DatabasePath)!);
        Directory.CreateDirectory(options.WorkspaceRootPath);
        Directory.CreateDirectory(Path.Combine(options.WorkspaceRootPath, "managed-files"));
        Directory.CreateDirectory(Path.Combine(options.WorkspaceRootPath, "exports"));
        Directory.CreateDirectory(Path.Combine(options.WorkspaceRootPath, "evidence"));
        Directory.CreateDirectory(options.ManagerArtifactsRootPath);
    }
}

internal sealed class SeederHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "CanDoItAll.ScenarioSeeder";

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
}

internal sealed class SeederHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _started = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationTokenSource _stopped = new();

    public CancellationToken ApplicationStarted => _started.Token;

    public CancellationToken ApplicationStopping => _stopping.Token;

    public CancellationToken ApplicationStopped => _stopped.Token;

    public void StopApplication()
    {
        if (!_stopping.IsCancellationRequested)
        {
            _stopping.Cancel();
        }
    }

    public void Dispose()
    {
        _started.Dispose();
        _stopping.Dispose();
        _stopped.Dispose();
    }
}
