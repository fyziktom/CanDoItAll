using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Unit;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public async Task AddCanDoItAllInfrastructure_UsesInMemoryProvider_WhenConfigured()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-inmemory-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:ConnectionString"] = "rpi3-validation",
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
            services.AddCanDoItAllInfrastructure(configuration, new TestHostEnvironment(rootPath), []);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            await using var scope = provider.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();

            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", dbContext.Database.ProviderName);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void AppDbContextFactory_UsesInMemoryProvider_WhenConfiguredViaEnvironment()
    {
        const string providerVariable = "CANDOITALL_DATABASE_PROVIDER";
        const string connectionVariable = "CANDOITALL_DATABASE_CONNECTION";
        var originalProvider = Environment.GetEnvironmentVariable(providerVariable);
        var originalConnection = Environment.GetEnvironmentVariable(connectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, "InMemory");
            Environment.SetEnvironmentVariable(connectionVariable, "factory-validation");

            using var context = new AppDbContextFactory().CreateDbContext([]);
            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, originalProvider);
            Environment.SetEnvironmentVariable(connectionVariable, originalConnection);
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CanDoItAll.Tests.Unit";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
