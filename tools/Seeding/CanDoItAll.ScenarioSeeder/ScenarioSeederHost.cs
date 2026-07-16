using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Composition;
using CanDoItAll.Modules.Workbench;
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
    private static readonly string DefaultProfileRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CanDoItAll",
        "control-plane",
        "database-profiles",
        "postgresql-local");
    public const string DefaultScenario = "agentframework-integration-simulation";

    public const string GanttSampleScenario = "gantt-sample-project";

    public required string RepositoryRootPath { get; init; }

    public required string ScenarioName { get; init; }

    public required string ProfileRootPath { get; init; }

    public required string WorkspaceRootPath { get; init; }

    public required string ManagerArtifactsRootPath { get; init; }

    public string ActionName { get; init; } = string.Empty;

    public Guid? RunId { get; init; }

    public int? StepSequence { get; init; }

    public static ScenarioSeederOptions Parse(string[] args, string currentDirectory)
    {
        var repositoryRootPath = currentDirectory;
        var profileRootPath = DefaultProfileRoot;
        var scenarioName = DefaultScenario;
        var actionName = string.Empty;
        Guid? runId = null;
        int? stepSequence = null;

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
                case "--action":
                    actionName = GetRequiredValue(args, ref index);
                    break;
                case "--run-id":
                    runId = Guid.Parse(GetRequiredValue(args, ref index));
                    break;
                case "--step-sequence":
                    stepSequence = int.Parse(GetRequiredValue(args, ref index));
                    break;
            }
        }

        var workspaceRootPath = Path.Combine(profileRootPath, "workspace");
        var managerArtifactsRootPath = Path.Combine(profileRootPath, "manager-artifacts");

        return new ScenarioSeederOptions
        {
            RepositoryRootPath = Path.GetFullPath(repositoryRootPath),
            ScenarioName = string.IsNullOrWhiteSpace(scenarioName)
                ? DefaultScenario
                : scenarioName.Trim(),
            ProfileRootPath = Path.GetFullPath(profileRootPath),
            WorkspaceRootPath = Path.GetFullPath(workspaceRootPath),
            ManagerArtifactsRootPath = Path.GetFullPath(managerArtifactsRootPath),
            ActionName = actionName.Trim(),
            RunId = runId,
            StepSequence = stepSequence
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
            .AddEnvironmentVariables()
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
        services.AddCanDoItAllRuntimeModules(configuration);
        services.AddCanDoItAllRuntimeDatabaseSwitching();
        services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
        services.AddScoped<AgentFrameworkIntegrationSimulationSeeder>();
        services.AddScoped<GanttSampleProjectSeeder>();

        var serviceProvider = services.BuildServiceProvider(ServiceProviderOptions);
        await using var scope = serviceProvider.CreateAsyncScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IDatabaseProfileService>();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await ActivateRequestedProfileAsync(profileService, options, cancellationToken);
        await bootstrapper.EnsureCurrentProfileReadyAsync(cancellationToken);
        return serviceProvider;
    }

    private static Dictionary<string, string?> BuildConfigurationValues(ScenarioSeederOptions options)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
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

    private static async Task ActivateRequestedProfileAsync(
        IDatabaseProfileService profileService,
        ScenarioSeederOptions options,
        CancellationToken cancellationToken)
    {
        var selection = await profileService.GetCurrentSelectionAsync(cancellationToken);
        if (selection.IsRuntimeLocked)
        {
            return;
        }

        var normalizedWorkspaceRoot = Path.GetFullPath(options.WorkspaceRootPath);
        var profiles = await profileService.ListAsync(cancellationToken);
        DatabaseProfileSummary? requestedProfile = null;
        foreach (var profile in profiles)
        {
            var editor = await profileService.GetEditorAsync(profile.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(editor.WorkspaceRoot) &&
                string.Equals(
                    Path.GetFullPath(editor.WorkspaceRoot),
                    normalizedWorkspaceRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                requestedProfile = profile;
                break;
            }
        }

        if (requestedProfile is null)
        {
            throw new InvalidOperationException(
                $"No persisted database profile matched workspace root '{normalizedWorkspaceRoot}'. " +
                "Create or import the managed profile before running the scenario seeder.");
        }

        if (selection.ActiveProfileId == requestedProfile.Id)
        {
            return;
        }

        var activationResult = await profileService.ActivateAsync(requestedProfile.Id, cancellationToken);
        if (activationResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Activating database profile '{requestedProfile.DisplayName}' failed: " +
                string.Join("; ", activationResult.Errors.Select(error => error.Message)));
        }
    }

    private static void EnsureDirectories(ScenarioSeederOptions options)
    {
        Directory.CreateDirectory(options.ProfileRootPath);
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
