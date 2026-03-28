using System.Data.Common;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Composition;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsSchemaInitializerIntegrationTests
{
    [Fact]
    public async Task EnsureAsync_adds_hierarchy_table_and_indexes_to_existing_sqlite_database()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "candoitall-project-schema-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var databasePath = Path.Combine(rootPath, "candoitall.projects-schema.db");

        try
        {
            await CreateLegacyProjectsSchemaAsync(databasePath);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:ConnectionString"] = $"Data Source={databasePath}",
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

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            await using var scope = provider.CreateAsyncScope();
            var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            await dbContext.Database.EnsureCreatedAsync();
            await ProjectsSchemaInitializer.EnsureAsync(dbContext);

            await using var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            Assert.Contains(
                await ReadObjectNamesAsync(connection, "table"),
                name => string.Equals(name, "Projects_ProjectHierarchyLinks", StringComparison.Ordinal));
            Assert.Contains(
                await ReadObjectNamesAsync(connection, "index"),
                name => string.Equals(name, "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProjectId", StringComparison.Ordinal));
            Assert.Contains(
                await ReadObjectNamesAsync(connection, "index"),
                name => string.Equals(name, "IX_Projects_ProjectHierarchyLinks_ParentProjectId", StringComparison.Ordinal));
            Assert.Contains(
                await ReadObjectNamesAsync(connection, "index"),
                name => string.Equals(name, "IX_Projects_ProjectHierarchyLinks_ChildProjectId", StringComparison.Ordinal));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static async Task CreateLegacyProjectsSchemaAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE "Projects_Projects" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_Projects" PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Slug" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Objective" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "CurrentPhase" TEXT NOT NULL,
                "TargetDateUtc" TEXT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "UpdatedAtUtc" TEXT NOT NULL
            );

            CREATE TABLE "Projects_ProjectPhases" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectPhases" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Goal" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "OrderIndex" INTEGER NOT NULL,
                "StartDateUtc" TEXT NULL,
                "EndDateUtc" TEXT NULL
            );

            CREATE TABLE "Projects_ProjectOptionSelections" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Projects_ProjectOptionSelections" PRIMARY KEY,
                "ProjectId" TEXT NOT NULL,
                "Category" INTEGER NOT NULL,
                "OptionName" TEXT NOT NULL,
                "Notes" TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadObjectNamesAsync(DbConnection connection, string objectType)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "name"
            FROM "sqlite_master"
            WHERE "type" = $type;
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$type";
        parameter.Value = objectType;
        command.Parameters.Add(parameter);

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                names.Add(reader.GetString(0));
            }
        }

        return names;
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "CanDoItAll.Tests.Integration";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
