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
using CanDoItAll.Web.Composition;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

internal sealed class TestApplication : IAsyncDisposable
{
    private TestApplication(string rootPath, ServiceProvider services)
    {
        RootPath = rootPath;
        Services = services;
    }

    public string RootPath { get; }

    public ServiceProvider Services { get; }

    public static async Task<TestApplication> CreateAsync()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
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
                ["DevelopmentManager:TuningModeEnabled"] = "true",
                ["DevelopmentManager:ReviewBeforeSend"] = "true",
                ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCanDoItAllInfrastructure(configuration, new TestHostEnvironment(rootPath), ModuleAssemblies.All);
        services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
        services.AddSecurityModule();
        services.AddWorkspaceModule();
        services.AddProjectsModule();
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddFactoryModule();
        services.AddValidationModule();
        services.AddTestLabModule();
        services.AddActivityModule();
        services.AddAutomationModule();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return new TestApplication(rootPath, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
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

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CanDoItAll.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
