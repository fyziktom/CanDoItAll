using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web;
using CanDoItAll.Web.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectStructureAgentApiTestHost : IAsyncDisposable
{
    private ProjectStructureAgentApiTestHost(string rootPath, WebApplication app, HttpClient client)
    {
        RootPath = rootPath;
        App = app;
        Client = client;
    }

    public string RootPath { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public static async Task<ProjectStructureAgentApiTestHost> CreateAsync()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-api-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = rootPath,
            EnvironmentName = Environments.Development,
            ApplicationName = "CanDoItAll.Tests.Integration"
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = $"Data Source={Path.Combine(rootPath, "candoitall.tests.db")}",
            ["Storage:WorkspaceRoot"] = Path.Combine(rootPath, "workspace"),
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = ".artifacts/codex-manager",
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
        });

        builder.Services.AddLogging();
        builder.Services.AddCanDoItAllInfrastructure(builder.Configuration, builder.Environment, ModuleAssemblies.All);
        builder.Services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
        builder.Services.AddSecurityModule();
        builder.Services.AddWorkspaceModule();
        builder.Services.AddProjectsModule();
        builder.Services.AddWorkbenchModule();
        builder.Services.AddResourcesModule();
        builder.Services.AddPromptsModule();
        builder.Services.AddFactoryModule();
        builder.Services.AddValidationModule();
        builder.Services.AddTestLabModule();
        builder.Services.AddActivityModule();
        builder.Services.AddAutomationModule();

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.MapProjectStructureAgentApi();
        var clientToken = string.Empty;

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await WorkspaceSchemaInitializer.EnsureAsync(dbContext);
            await ProjectsSchemaInitializer.EnsureAsync(dbContext);
            await PromptFactorySchemaInitializer.EnsureAsync(dbContext);
            await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext);
            await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext);

            var administrationService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentAdministrationService>();
            await administrationService.SaveSettingsAsync(
                new ProjectStructureAgentWorkspaceSettingsModel
                {
                    CentralBaseUrl = "http://127.0.0.1",
                    InstallScriptPath = @"tools\Install-CanDoItAllProjectStructureMcp.ps1",
                    SetupReadmePath = @"docs\project-structure-mcp-setup.md"
                },
                CancellationToken.None);

            var profileResult = await administrationService.SaveProfileAsync(
                new ProjectStructureAgentProfileEditorModel
                {
                    Name = "API Test Agent",
                    Description = "Integration host agent profile",
                    CapabilityMask = ProjectStructureAgentCapability.All,
                    GenerateNewToken = true
                },
                CancellationToken.None);
            if (!profileResult.IsSuccess)
            {
                throw new InvalidOperationException(string.Join(" ", profileResult.Errors.Select(error => error.Message)));
            }

            var seededProfile = await administrationService.GetProfileAsync(profileResult.Value, CancellationToken.None);
            clientToken = seededProfile.TokenValue;
        }

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The API test host did not expose any server addresses.");
        var client = new HttpClient
        {
            BaseAddress = new Uri(addresses.Single())
        };
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, "api-test-agent");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, "API Test Agent");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, "api-test-machine");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, "C:/repositories/CanDoItAll");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "tests/project-structure");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentToken, clientToken);

        return new ProjectStructureAgentApiTestHost(rootPath, app, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.StopAsync();
        await App.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(RootPath))
        {
            DeleteDirectoryWithRetry(RootPath);
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }
}
