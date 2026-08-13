using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class MigrationBootstrapIntegrationTests
{
    [Fact]
    public async Task Bootstrap_migrates_a_new_postgresql_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migrations");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-primary");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var resolver = scope.ServiceProvider.GetRequiredService<IActiveDatabaseProfileResolver>();

        var profile = resolver.ResolveCurrentProfile();

        Assert.Equal(DatabaseProviderKind.PostgreSql, profile.Profile.ProviderKind);
        Assert.Equal(activeProfile.ConnectionString, profile.ConnectionString);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
        await AssertCurrentMigrationChainAsync(dbContext);
    }

    [Fact]
    public async Task Bootstrap_is_idempotent_for_postgresql_profiles()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-migration-idempotent");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-idempotent");
        await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Integration",
            TestSchemaBootstrapModules.Full);

        await using var scope = provider.CreateAsyncScope();
        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Database.CanConnectAsync());
        await AssertCurrentMigrationChainAsync(dbContext);
    }

    [Fact]
    public async Task Bootstrap_adopts_existing_postgresql_schema_without_migration_history()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-existing-schema");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-existing-schema");

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var existingSchemaContext = await dbContextFactory.CreateDbContextAsync())
        {
            var migrator = existingSchemaContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PostgreSqlMigrationBaseline.CurrentMigrationId);
            await existingSchemaContext.Database.ExecuteSqlRawAsync(
                """DELETE FROM "__EFMigrationsHistory";""");
            Assert.Empty(await existingSchemaContext.Database.GetAppliedMigrationsAsync());
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await AssertCurrentMigrationChainAsync(dbContext);
        Assert.True(await dbContext.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Bootstrap_reconciles_the_complete_legacy_history_and_restores_owned_custom_indexes()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-squashed-history");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-squashed-history");
        string[] predecessorMigrationIds =
        [
            "20260401094848_InitialCreate",
            "20260520190312_AddCognitiveMemoryStatementAggregateClaimMaps"
        ];

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var legacyHistoryContext = await dbContextFactory.CreateDbContextAsync())
        {
            var migrator = legacyHistoryContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync(PostgreSqlMigrationBaseline.CurrentMigrationId);
            foreach (var legacyMigrationId in PostgreSqlMigrationBaseline.LegacyMigrationIds.Concat(predecessorMigrationIds))
            {
                await legacyHistoryContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                     SELECT {legacyMigrationId}, "ProductVersion"
                     FROM "__EFMigrationsHistory"
                     WHERE "MigrationId" = {PostgreSqlMigrationBaseline.CurrentMigrationId};
                     """);
            }

            await legacyHistoryContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 DELETE FROM "__EFMigrationsHistory"
                 WHERE "MigrationId" = {PostgreSqlMigrationBaseline.CurrentMigrationId};
                 """);
            await legacyHistoryContext.Database.ExecuteSqlRawAsync(
                """DROP INDEX "IX_Workspace_ConnectorCommands_PendingClaimOrder";""");
            Assert.Equal(
                PostgreSqlMigrationBaseline.LegacyMigrationIds
                    .Concat(predecessorMigrationIds)
                    .Order(StringComparer.Ordinal),
                (await legacyHistoryContext.Database.GetAppliedMigrationsAsync()).Order(StringComparer.Ordinal));
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await AssertCurrentMigrationChainAsync(dbContext);
    }

    [Fact]
    public async Task Bootstrap_migrates_legacy_process_strategy_result_receipts()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-process-result-hashes");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-process-result-hashes");

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var runId = Guid.NewGuid();
        var stepInstanceId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        const string legacyHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string legacySummary = @"Workflow output is stored at C:\legacy\result.txt";

        await using (var legacyContext = await dbContextFactory.CreateDbContextAsync())
        {
            var migrator = legacyContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260813012618_CorrectProcessPlanHashClassification");
            await legacyContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO process_runtime_states
                     ("RunId", "RootRunId", "PlanId", "PlanHash", "Status", "UpdatedAtUtc", "ConcurrencyToken",
                      "BlockedRecoveryActionsJson")
                 VALUES
                     ({runId}, {runId}, {Guid.NewGuid()}, {"sha256:" + new string('a', 64)}, {"Active"}, {DateTimeOffset.UtcNow},
                      {Guid.NewGuid()}, {"[]"});
                 """);
            await legacyContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO process_strategy_result_receipts
                     ("RunId", "StepInstanceId", "StrategyId", "IdempotencyKey", "Outcome", "AppliedStepStatus",
                      "ResultHash", "DiagnosticsJson", "ProducedArtifactsJson", "UserSafeSummary", "AppliedSequence")
                 VALUES
                     ({runId}, {stepInstanceId}, {"strategy.adapter.workflow.execute"}, {idempotencyKey}, {"Succeeded"},
                      {"Completed"}, {legacyHash}, {"[]"}, {"[]"}, {legacySummary}, {1L});
                 """);
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedReceipt = await dbContext.Set<ProcessStrategyResultReceiptEntity>()
            .Where(receipt => receipt.RunId == runId && receipt.IdempotencyKey == idempotencyKey)
            .SingleAsync();

        Assert.Equal("sha256:" + legacyHash, persistedReceipt.ResultHash);
        Assert.Equal(
            ProcessStrategyResultReceiptContractVersion.LegacyV1,
            persistedReceipt.ContractVersion);
        var processOptions = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseNpgsql(activeProfile.ConnectionString)
            .Options;
        await using var processContext = new ProcessPersistenceDbContext(processOptions);
        var loaded = await new EfProcessRuntimeUnitOfWork(processContext)
            .LoadAsync(new ProcessRunId(runId));
        Assert.NotNull(loaded);
        var loadedReceipt = Assert.Single(loaded.AppliedResults);
        Assert.True(ProcessPublicReceiptTextPolicy.IsSafe(
            loadedReceipt.UserSafeSummary,
            ProcessStrategyResultLimits.MaximumUserSafeSummaryLength));
        await AssertCurrentMigrationChainAsync(dbContext);
    }

    [Fact]
    public async Task Bootstrap_refuses_to_adopt_schema_with_retired_prompt_factory_tables()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-factory-adoption");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("migration-factory-adoption");

        var services = new ServiceCollection();
        var environment = new TestHostEnvironment(activeProfile.EnvironmentRootPath, "CanDoItAll.Tests.Integration");
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile);
        TestApplicationBootstrap.ConfigureDefaultServices(services, configuration, environment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await using var scope = provider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var existingSchemaContext = await dbContextFactory.CreateDbContextAsync())
        {
            await existingSchemaContext.Database.EnsureCreatedAsync();
            await existingSchemaContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "Factory_PromptBlocks" (
                    "Id" uuid NOT NULL,
                    CONSTRAINT "PK_Factory_PromptBlocks" PRIMARY KEY ("Id")
                );
                """);
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IAppDatabaseBootstrapper>();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bootstrapper.EnsureCurrentProfileReadyAsync());

        Assert.Contains("retired table Factory_PromptBlocks", exception.Message, StringComparison.Ordinal);
    }

    private static async Task AssertCurrentMigrationChainAsync(AppDbContext dbContext)
    {
        var knownMigrations = dbContext.Database.GetMigrations().ToArray();
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(knownMigrations);
        Assert.Equal(PostgreSqlMigrationBaseline.CurrentMigrationId, knownMigrations[0]);
        Assert.DoesNotContain(knownMigrations, PostgreSqlMigrationBaseline.LegacyMigrationIds.Contains);
        Assert.Equal(knownMigrations, appliedMigrations);
        await AssertProcessRuntimeStepHostCapabilitiesColumnAsync(dbContext);
        await AssertCustomBaselineIndexesAsync(dbContext);
    }

    private static async Task AssertProcessRuntimeStepHostCapabilitiesColumnAsync(AppDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'process_runtime_steps'
              AND column_name = 'RequiredHostCapabilitiesJson';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("NO", reader.GetString(0));
        Assert.Contains("[]", reader.GetString(1), StringComparison.Ordinal);
        Assert.False(await reader.ReadAsync());
    }

    private static async Task AssertCustomBaselineIndexesAsync(AppDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var actualIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = current_schema()
              AND indexname IN (
                  'IX_Workspace_ConnectorCommands_PendingClaimOrder',
                  'IX_Prompts_PromptArtifacts_SearchText_Trgm',
                  'IX_Prompts_PromptTags_NameKey_Trgm'
              );
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actualIndexNames.Add(reader.GetString(0));
        }

        Assert.Equal(
            PostgreSqlMigrationBaseline.CustomIndexNames.Order(StringComparer.Ordinal),
            actualIndexNames.Order(StringComparer.Ordinal));
    }
}
