using Bunit;
using CanDoItAll.ComponentKit.Components;
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
using CanDoItAll.Web.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Components;

internal sealed class ComponentTestHarness : IAsyncDisposable
{
    private ComponentTestHarness(string rootPath, TestContext context)
    {
        RootPath = rootPath;
        Context = context;
    }

    public string RootPath { get; }

    public TestContext Context { get; }

    public static async Task<ComponentTestHarness> CreateAsync()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-component-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:ConnectionString"] = $"Data Source={Path.Combine(rootPath, "candoitall.components.db")}",
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

        context.Services.AddLogging();
        context.Services.AddCanDoItAllInfrastructure(configuration, new TestHostEnvironment(rootPath), ModuleAssemblies.All);
        context.Services.AddScoped<IWorkbenchStateStore, InMemoryWorkbenchStateStore>();
        context.Services.AddScoped<TuningCoordinator>();
        context.Services.AddHttpClient<DevelopmentManagerClient>();
        context.Services.AddSecurityModule();
        context.Services.AddWorkspaceModule();
        context.Services.AddProjectsModule();
        context.Services.AddWorkbenchModule();
        context.Services.AddResourcesModule();
        context.Services.AddPromptsModule();
        context.Services.AddFactoryModule();
        context.Services.AddValidationModule();
        context.Services.AddTestLabModule();
        context.Services.AddActivityModule();
        context.Services.AddAutomationModule();

        var dbContextFactory = context.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Database.EnsureCreatedAsync();

        return new ComponentTestHarness(rootPath, context);
    }

    public ValueTask DisposeAsync()
    {
        Context.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(RootPath))
        {
            DeleteDirectoryWithRetry(RootPath);
        }

        return ValueTask.CompletedTask;
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

        public string ApplicationName { get; set; } = "CanDoItAll.Tests.Components";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
