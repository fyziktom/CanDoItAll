using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DatabaseSwitchIntegrationTests
{
    [Fact]
    public async Task SwitchAsync_changes_active_data_source_without_restarting_the_process()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-runtime-switch");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var runtimeState = provider.GetRequiredService<IDatabaseRuntimeState>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var switchableFactory = provider.GetRequiredService<ISwitchableAppDbContextFactory>();

        var initialProfile = runtimeAccessor.ResolveCurrentProfile();
        await bootstrapper.EnsureCurrentProfileReadyAsync();

        await using (var initialContext = await dbContextFactory.CreateDbContextAsync())
        {
            initialContext.Set<BackgroundJobRecord>().Add(new BackgroundJobRecord
            {
                JobType = "alpha",
                Description = "alpha profile job",
                CorrelationId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await initialContext.SaveChangesAsync();
        }

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Managed sqlite beta",
            ProviderKind = DatabaseProviderKind.Sqlite,
            SourceKind = DatabaseProfileSourceKind.ManagedSqlite
        });

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        await bootstrapper.EnsureProfileReadyAsync(targetProfile);

        await using (var targetContext = await switchableFactory.CreateDbContextForProfileAsync(targetProfile))
        {
            targetContext.Set<BackgroundJobRecord>().Add(new BackgroundJobRecord
            {
                JobType = "beta",
                Description = "beta profile job",
                CorrelationId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await targetContext.SaveChangesAsync();
        }

        var processIdBeforeSwitch = Environment.ProcessId;
        var firstSwitch = await switchCoordinator.SwitchAsync(targetProfile.Profile.Id);

        Assert.True(firstSwitch.IsSuccess);
        Assert.Equal(processIdBeforeSwitch, firstSwitch.Value!.ProcessId);

        await using (var switchedContext = await dbContextFactory.CreateDbContextAsync())
        {
            var descriptions = await switchedContext.Set<BackgroundJobRecord>()
                .OrderBy(job => job.Description)
                .Select(job => job.Description)
                .ToListAsync();

            Assert.Equal(["beta profile job"], descriptions);
        }

        var switchBack = await switchCoordinator.SwitchAsync(initialProfile.Profile.Id);

        Assert.True(switchBack.IsSuccess);
        Assert.Equal(processIdBeforeSwitch, switchBack.Value!.ProcessId);

        await using (var restoredContext = await dbContextFactory.CreateDbContextAsync())
        {
            var descriptions = await restoredContext.Set<BackgroundJobRecord>()
                .OrderBy(job => job.Description)
                .Select(job => job.Description)
                .ToListAsync();

            Assert.Equal(["alpha profile job"], descriptions);
        }

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(initialProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.True(runtimeSnapshot.Generation >= 2);
    }
}

public sealed class DatabaseDriverBootstrapIntegrationTests
{
    [Fact]
    public async Task PostgreSql_driver_can_create_and_bootstrap_an_empty_database()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-postgres-driver");
        var availability = await PostgresTestAvailability.EnsureAvailableAsync("C:\\repositories\\CanDoItAll");
        Assert.True(availability.IsAvailable, availability.Message);

        var baseBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString);
        var databaseName = $"candoitall_switch_{Guid.NewGuid():N}"[..30];

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var driverRegistry = provider.GetRequiredService<IDatabaseDriverRegistry>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var switchableFactory = provider.GetRequiredService<ISwitchableAppDbContextFactory>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Docker postgres target",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = baseBuilder.Host ?? "127.0.0.1",
            PostgresPort = baseBuilder.Port,
            PostgresDatabaseName = databaseName,
            PostgresUsername = baseBuilder.Username ?? "candoitall",
            PostgresPassword = baseBuilder.Password ?? "candoitall",
            PostgresAdminDatabaseName = baseBuilder.Database,
            WorkspaceRoot = Path.Combine(testEnvironment.RootPath, "postgres-workspace")
        });

        Assert.True(saveResult.IsSuccess);

        var profile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var driver = driverRegistry.Resolve(DatabaseProviderKind.PostgreSql);

        try
        {
            await driver.CreateEmptyAsync(profile);
            await driver.EnsureDatabaseAsync(profile);
            await bootstrapper.EnsureProfileReadyAsync(profile);

            await using var dbContext = await switchableFactory.CreateDbContextForProfileAsync(profile);
            Assert.True(await dbContext.Database.CanConnectAsync());
            Assert.Contains(
                await dbContext.Database.GetAppliedMigrationsAsync(),
                migrationId => migrationId.Contains("InitialCreate", StringComparison.Ordinal));
        }
        finally
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString)
            {
                Database = string.IsNullOrWhiteSpace(baseBuilder.Database) ? "postgres" : baseBuilder.Database
            };

            await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await adminConnection.OpenAsync();
            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }
}

public sealed class DatabaseSnapshotIntegrationTests
{
    [Fact]
    public async Task CloneAsync_creates_snapshot_backed_profile_with_copied_data_and_files()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-snapshot-clone");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var snapshotService = provider.GetRequiredService<IDatabaseSnapshotService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        var sourceProfile = runtimeAccessor.ResolveCurrentProfile();

        TestProfileSeedResult sourceSeed;
        await using (var sourceScope = provider.CreateAsyncScope())
        {
            sourceSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                sourceScope.ServiceProvider,
                "Alpha");
        }

        var cloneResult = await snapshotService.CloneAsync(new DatabaseCloneRequest
        {
            SourceProfileId = sourceProfile.Profile.Id,
            DisplayName = "Alpha clone"
        });

        Assert.True(cloneResult.IsSuccess, DescribeErrors(cloneResult.Errors));

        var cloneProfile = runtimeAccessor.ResolveProfile(cloneResult.Value!.ProfileId);
        Assert.Equal(DatabaseProfileSourceKind.SnapshotCache, cloneProfile.Profile.SourceKind);
        Assert.Equal(sourceProfile.Profile.Id, cloneProfile.Profile.Clone.OriginProfileId);
        Assert.Equal(cloneResult.Value.Manifest.SnapshotId, cloneProfile.Profile.Clone.OriginSnapshotId);
        Assert.StartsWith(testEnvironment.ControlPlaneRootPath, cloneProfile.Profile.Storage.WorkspaceRoot, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(cloneResult.Value.PackagePath));

        var switchToClone = await switchCoordinator.SwitchAsync(cloneProfile.Profile.Id);
        Assert.True(switchToClone.IsSuccess, DescribeErrors(switchToClone.Errors));

        TestProfileSeedResult cloneSeed;
        await using (var cloneScope = provider.CreateAsyncScope())
        {
            var cloneProjects = await cloneScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
            Assert.Contains(cloneProjects, project => project.Name == sourceSeed.ProjectName);

            var clonedFilePath = Path.Combine(cloneProfile.Profile.Storage.WorkspaceRoot, sourceSeed.ManagedFileRelativePath);
            Assert.Equal(sourceSeed.ManagedFileContent, await File.ReadAllTextAsync(clonedFilePath));

            cloneSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                cloneScope.ServiceProvider,
                "Clone");
        }

        var switchBack = await switchCoordinator.SwitchAsync(sourceProfile.Profile.Id);
        Assert.True(switchBack.IsSuccess, DescribeErrors(switchBack.Errors));

        TestProfileSeedResult sourceOnlySeed;
        await using (var sourceVerifyScope = provider.CreateAsyncScope())
        {
            var sourceProjects = await sourceVerifyScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
            Assert.Contains(sourceProjects, project => project.Name == sourceSeed.ProjectName);
            Assert.DoesNotContain(sourceProjects, project => project.Name == cloneSeed.ProjectName);
            Assert.False(File.Exists(Path.Combine(sourceProfile.Profile.Storage.WorkspaceRoot, cloneSeed.ManagedFileRelativePath)));

            sourceOnlySeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                sourceVerifyScope.ServiceProvider,
                "Source");
        }

        var switchAgain = await switchCoordinator.SwitchAsync(cloneProfile.Profile.Id);
        Assert.True(switchAgain.IsSuccess, DescribeErrors(switchAgain.Errors));

        await using (var cloneVerifyScope = provider.CreateAsyncScope())
        {
            var cloneProjects = await cloneVerifyScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
            Assert.Contains(cloneProjects, project => project.Name == sourceSeed.ProjectName);
            Assert.Contains(cloneProjects, project => project.Name == cloneSeed.ProjectName);
            Assert.DoesNotContain(cloneProjects, project => project.Name == sourceOnlySeed.ProjectName);
            Assert.False(File.Exists(Path.Combine(cloneProfile.Profile.Storage.WorkspaceRoot, sourceOnlySeed.ManagedFileRelativePath)));
        }
    }

    [Fact]
    public async Task Local_snapshot_package_can_materialize_into_a_new_profile()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-snapshot-local");
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var snapshotService = provider.GetRequiredService<IDatabaseSnapshotService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        var sourceProfile = runtimeAccessor.ResolveCurrentProfile();

        TestProfileSeedResult sourceSeed;
        await using (var sourceScope = provider.CreateAsyncScope())
        {
            sourceSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                sourceScope.ServiceProvider,
                "Local");
        }

        var snapshotResult = await snapshotService.CreateSnapshotAsync(
            sourceProfile.Profile.Id,
            DatabaseSnapshotTransportKind.Local);
        Assert.True(snapshotResult.IsSuccess, DescribeErrors(snapshotResult.Errors));
        Assert.True(File.Exists(snapshotResult.Value!.PackagePath));

        var materializeResult = await snapshotService.MaterializeSnapshotAsync(new DatabaseSnapshotMaterializationRequest
        {
            DisplayName = "Local snapshot restore",
            PackagePath = snapshotResult.Value.PackagePath
        });
        Assert.True(materializeResult.IsSuccess, DescribeErrors(materializeResult.Errors));

        var restoredProfile = runtimeAccessor.ResolveProfile(materializeResult.Value!.ProfileId);
        Assert.Equal(DatabaseProfileSourceKind.SnapshotCache, restoredProfile.Profile.SourceKind);

        var switchResult = await switchCoordinator.SwitchAsync(restoredProfile.Profile.Id);
        Assert.True(switchResult.IsSuccess, DescribeErrors(switchResult.Errors));

        await using var restoredScope = provider.CreateAsyncScope();
        var restoredProjects = await restoredScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
        Assert.Contains(restoredProjects, project => project.Name == sourceSeed.ProjectName);
        Assert.Equal(
            sourceSeed.ManagedFileContent,
            await File.ReadAllTextAsync(Path.Combine(restoredProfile.Profile.Storage.WorkspaceRoot, sourceSeed.ManagedFileRelativePath)));
    }

    [Fact]
    public async Task Ipfs_snapshot_transport_round_trips_through_the_fake_server()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-snapshot-ipfs");
        await using var server = await FakeIpfsTestServer.StartAsync();
        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(
            testEnvironment,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ControlPlane:IpfsApiBaseUrl"] = server.ApiBaseUri.ToString()
            });

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var snapshotService = provider.GetRequiredService<IDatabaseSnapshotService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();

        await bootstrapper.EnsureCurrentProfileReadyAsync();
        var sourceProfile = runtimeAccessor.ResolveCurrentProfile();

        TestProfileSeedResult sourceSeed;
        await using (var sourceScope = provider.CreateAsyncScope())
        {
            sourceSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                sourceScope.ServiceProvider,
                "Ipfs");
        }

        var snapshotResult = await snapshotService.CreateSnapshotAsync(
            sourceProfile.Profile.Id,
            DatabaseSnapshotTransportKind.Ipfs);
        Assert.True(snapshotResult.IsSuccess, DescribeErrors(snapshotResult.Errors));
        Assert.False(string.IsNullOrWhiteSpace(snapshotResult.Value!.IpfsCid));
        Assert.Contains(snapshotResult.Value.IpfsCid!, server.StoredCids);
        Assert.Contains(snapshotResult.Value.IpfsCid!, server.PinnedCids);

        var materializeResult = await snapshotService.MaterializeSnapshotAsync(new DatabaseSnapshotMaterializationRequest
        {
            DisplayName = "IPFS snapshot restore",
            SnapshotCid = snapshotResult.Value.IpfsCid
        });
        Assert.True(materializeResult.IsSuccess, DescribeErrors(materializeResult.Errors));

        var restoredProfile = runtimeAccessor.ResolveProfile(materializeResult.Value!.ProfileId);
        Assert.Equal(DatabaseProfileSourceKind.IpfsSnapshot, restoredProfile.Profile.SourceKind);

        var switchResult = await switchCoordinator.SwitchAsync(restoredProfile.Profile.Id);
        Assert.True(switchResult.IsSuccess, DescribeErrors(switchResult.Errors));

        await using var restoredScope = provider.CreateAsyncScope();
        var restoredProjects = await restoredScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
        Assert.Contains(restoredProjects, project => project.Name == sourceSeed.ProjectName);
        Assert.Equal(
            sourceSeed.ManagedFileContent,
            await File.ReadAllTextAsync(Path.Combine(restoredProfile.Profile.Storage.WorkspaceRoot, sourceSeed.ManagedFileRelativePath)));
    }

    [Fact]
    public async Task PostgreSql_profile_can_be_cloned_into_a_snapshot_backed_sqlite_profile()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-snapshot-postgres");
        var availability = await PostgresTestAvailability.EnsureAvailableAsync("C:\\repositories\\CanDoItAll");
        Assert.True(availability.IsAvailable, availability.Message);

        var baseBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString);
        var databaseName = $"candoitall_clone_{Guid.NewGuid():N}"[..29];

        await using var provider = DatabaseProfileControlPlaneIntegrationHost.BuildServiceProvider(testEnvironment);
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var driverRegistry = provider.GetRequiredService<IDatabaseDriverRegistry>();
        var bootstrapper = provider.GetRequiredService<IAppDatabaseBootstrapper>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var snapshotService = provider.GetRequiredService<IDatabaseSnapshotService>();

        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = "Snapshot postgres source",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = baseBuilder.Host ?? "127.0.0.1",
            PostgresPort = baseBuilder.Port,
            PostgresDatabaseName = databaseName,
            PostgresUsername = baseBuilder.Username ?? "candoitall",
            PostgresPassword = baseBuilder.Password ?? "candoitall",
            PostgresAdminDatabaseName = baseBuilder.Database,
            WorkspaceRoot = Path.Combine(testEnvironment.RootPath, "postgres-workspace")
        });

        Assert.True(saveResult.IsSuccess, DescribeErrors(saveResult.Errors));

        var postgresProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var driver = driverRegistry.Resolve(DatabaseProviderKind.PostgreSql);

        try
        {
            await driver.CreateEmptyAsync(postgresProfile);
            await bootstrapper.EnsureProfileReadyAsync(postgresProfile);

            var switchToPostgres = await switchCoordinator.SwitchAsync(postgresProfile.Profile.Id);
            Assert.True(switchToPostgres.IsSuccess, DescribeErrors(switchToPostgres.Errors));

            TestProfileSeedResult postgresSeed;
            await using (var postgresScope = provider.CreateAsyncScope())
            {
                postgresSeed = await TestProfileSeedHelper.SeedDistinctProjectAndManagedFileAsync(
                    postgresScope.ServiceProvider,
                    "Postgres");
            }

            var cloneResult = await snapshotService.CloneAsync(new DatabaseCloneRequest
            {
                SourceProfileId = postgresProfile.Profile.Id,
                DisplayName = "Postgres clone"
            });
            Assert.True(cloneResult.IsSuccess, DescribeErrors(cloneResult.Errors));

            var cloneProfile = runtimeAccessor.ResolveProfile(cloneResult.Value!.ProfileId);
            Assert.Equal(DatabaseProviderKind.Sqlite, cloneProfile.Profile.ProviderKind);
            Assert.Equal(DatabaseProfileSourceKind.SnapshotCache, cloneProfile.Profile.SourceKind);

            var switchToClone = await switchCoordinator.SwitchAsync(cloneProfile.Profile.Id);
            Assert.True(switchToClone.IsSuccess, DescribeErrors(switchToClone.Errors));

            await using var cloneScope = provider.CreateAsyncScope();
            var cloneProjects = await cloneScope.ServiceProvider.GetRequiredService<ProjectsService>().ListAsync();
            Assert.Contains(cloneProjects, project => project.Name == postgresSeed.ProjectName);
            Assert.Equal(
                postgresSeed.ManagedFileContent,
                await File.ReadAllTextAsync(Path.Combine(cloneProfile.Profile.Storage.WorkspaceRoot, postgresSeed.ManagedFileRelativePath)));
        }
        finally
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(availability.ConnectionString)
            {
                Database = string.IsNullOrWhiteSpace(baseBuilder.Database) ? "postgres" : baseBuilder.Database
            };

            await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
            await adminConnection.OpenAsync();
            await using var dropCommand = adminConnection.CreateCommand();
            dropCommand.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
            await dropCommand.ExecuteNonQueryAsync();
        }
    }

    private static string DescribeErrors(IReadOnlyList<CanDoItAll.SharedKernel.Error> errors)
    {
        return string.Join(" ", errors.Select(error => error.Message));
    }
}
