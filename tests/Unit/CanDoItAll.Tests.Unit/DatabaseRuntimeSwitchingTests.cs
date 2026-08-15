using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Unit.Infrastructure.DatabaseRuntimeSwitchingTestProfiles;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class DatabaseRuntimeStateTests
{
    [Fact]
    public async Task MarkCurrentProfile_is_concurrently_idempotent_only_for_the_same_identity()
    {
        var first = CreateResolvedProfile("first");
        var second = CreateResolvedProfile("second");
        var runtimeState = new DatabaseRuntimeState(
            new DatabaseSwitchNotificationService());
        runtimeState.MarkCurrentProfile(first);

        const int updateCount = 20_000;
        const int readerCount = 4;
        var writer = Task.Run(() =>
        {
            for (var index = 0; index < updateCount; index++)
            {
                runtimeState.MarkCurrentProfile(first);
            }
        });
        var readers = Enumerable.Range(0, readerCount)
            .Select(_ => Task.Run(() =>
            {
                for (var index = 0; index < updateCount; index++)
                {
                    var observed = runtimeState.GetSnapshot();
                    Assert.Equal(first.Profile.Id, observed.ActiveProfileId);
                    Assert.Equal(
                        first.Profile.Runtime.Fingerprint,
                        observed.ActiveFingerprint);
                    Assert.Equal(0, observed.Generation);
                }
            }))
            .ToArray();

        await Task.WhenAll(readers.Append(writer));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            runtimeState.MarkCurrentProfile(second));
        Assert.Contains(
            "already initialized",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.Profile.Id, runtimeState.GetSnapshot().ActiveProfileId);
    }

    [Fact]
    public void PublishRestartObserved_isolates_subscribers_after_advancing_runtime_generation()
    {
        var first = CreateResolvedProfile("first");
        var second = CreateResolvedProfile("second");
        var notifications = new DatabaseSwitchNotificationService();
        var runtimeState = new DatabaseRuntimeState(notifications);
        runtimeState.MarkCurrentProfile(first);
        var previous = runtimeState.GetSnapshot();
        var throwingSubscriberCalls = 0;
        var laterSubscriberCalls = 0;
        DatabaseProfileChangedNotification? observedNotification = null;

        notifications.Changed += (_, _) =>
        {
            throwingSubscriberCalls++;
            throw new InvalidOperationException("Expected subscriber failure.");
        };
        notifications.Changed += (_, notification) =>
        {
            laterSubscriberCalls++;
            observedNotification = notification;
        };

        var exception = Assert.Throws<AggregateException>(() =>
            runtimeState.PublishRestartObserved(previous, second));

        Assert.Single(exception.InnerExceptions);
        Assert.IsType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.Equal(1, throwingSubscriberCalls);
        Assert.Equal(1, laterSubscriberCalls);

        var current = runtimeState.GetSnapshot();
        Assert.Equal(second.Profile.Id, current.ActiveProfileId);
        Assert.Equal(second.Profile.Runtime.Fingerprint, current.ActiveFingerprint);
        Assert.Equal(previous.Generation + 1, current.Generation);
        Assert.NotNull(observedNotification);
        Assert.Equal(current.Generation, observedNotification.Generation);
        Assert.Equal(second.Profile.Id, observedNotification.CurrentProfileId);
        Assert.Throws<InvalidOperationException>(() =>
            runtimeState.MarkCurrentProfile(first));
    }

    [Fact]
    public async Task Write_fence_totally_orders_commit_and_rejects_the_old_generation_after_switch()
    {
        var first = CreateResolvedProfile("first");
        var second = CreateResolvedProfile("second");
        var runtimeState = new DatabaseRuntimeState(new DatabaseSwitchNotificationService());
        runtimeState.MarkCurrentProfile(first);
        var expected = runtimeState.GetSnapshot();
        var writeEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWrite = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var write = runtimeState.ExecuteAsync(expected, async _ =>
        {
            writeEntered.TrySetResult(true);
            await releaseWrite.Task.ConfigureAwait(false);
            return 42;
        });
        await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var switchProfile = Task.Run(() => runtimeState.PublishRestartObserved(expected, second));
        await Task.Yield();
        Assert.False(switchProfile.IsCompleted);

        releaseWrite.TrySetResult(true);
        Assert.Equal(42, await write);
        await switchProfile;

        var staleWriteExecuted = false;
        await Assert.ThrowsAsync<DatabaseRuntimeProfileChangedException>(() => runtimeState.ExecuteAsync(
            expected,
            _ =>
            {
                staleWriteExecuted = true;
                return Task.FromResult(0);
            }));
        Assert.False(staleWriteExecuted);
    }

    private static ResolvedDatabaseProfile CreateResolvedProfile(string name)
    {
        return new ResolvedDatabaseProfile(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = name,
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory,
                InMemory = new InMemoryDatabaseProfileConnection
                {
                    DatabaseName = name
                },
                Storage = new DatabaseProfileStorageDescriptor
                {
                    Mode = DatabaseProfileStorageMode.Ephemeral
                },
                Runtime = new DatabaseProfileRuntimeMetadata
                {
                    Fingerprint = $"fingerprint-{name}"
                }
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            $"in-memory:{name}");
    }
}

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class DatabaseSwitchCoordinatorTests
{
    [Fact]
    public async Task SwitchAsync_returns_failure_when_RuntimeOverride_is_active()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-override-switch");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: true);

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();

        var saveResult = await profileService.SaveAsync(CreatePostgreSqlEditorForDatabase(
            "Switch target",
            "runtime_override_switch",
            Path.Combine(testEnvironment.RootPath, "switch-target-workspace")));

        Assert.True(saveResult.IsSuccess);

        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value);

        Assert.True(switchResult.IsFailure);
        Assert.Contains(
            switchResult.Errors,
            error => error.Message.Contains("Runtime override", StringComparison.OrdinalIgnoreCase));
    }
}

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AppDbContextRuntimeSwitchTests
{
    [Fact]
    public async Task CreateDbContextAsync_keeps_canonical_profile_until_restart_after_activation()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        await using var testEnvironment = CanDoItAllTestEnvironment.Create("runtime-context-switch");
        await using var provider = DatabaseProfileControlPlaneTestHost.BuildServiceProvider(
            testEnvironment,
            includeDatabaseOverride: false,
            dataProtectionProvider: new EphemeralDataProtectionProvider(),
            secretVault: new InMemorySecretVault());

        var runtimeAccessor = provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var switchCoordinator = provider.GetRequiredService<IDatabaseSwitchCoordinator>();
        var runtimeState = provider.GetRequiredService<IDatabaseRuntimeState>();
        var dbContextFactory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var initialProfile = runtimeAccessor.ResolveCurrentProfile();
        await using var targetDatabase = PostgresTestDatabaseLease.Create("runtime-context-switch");
        var saveResult = await profileService.SaveAsync(CreatePostgreSqlEditor(
            "PostgreSQL switch target",
            targetDatabase.ConnectionString,
            Path.Combine(testEnvironment.RootPath, "switch-target-workspace")));

        Assert.True(saveResult.IsSuccess);

        var targetProfile = runtimeAccessor.ResolveProfile(saveResult.Value);
        var switchResult = await switchCoordinator.SwitchAsync(saveResult.Value);

        Assert.True(switchResult.IsSuccess, string.Join("; ", switchResult.Errors.Select(error => error.Message)));
        Assert.NotEqual(initialProfile.Profile.Id, targetProfile.Profile.Id);
        Assert.True(switchResult.Value!.RequiresRestart);
        Assert.False(switchResult.Value.RuntimeChangedInProcess);
        Assert.Equal(initialProfile.Profile.Id, switchResult.Value.RuntimeProfileId);
        Assert.Equal(targetProfile.Profile.Id, switchResult.Value.PendingRestartProfileId);
        Assert.Contains("Restart", switchResult.Value.Message, StringComparison.OrdinalIgnoreCase);

        await using var switchedContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(initialProfile.ConnectionString, switchedContext.Database.GetConnectionString());
        Assert.NotEqual(targetProfile.ConnectionString, switchedContext.Database.GetConnectionString());

        var runtimeSnapshot = runtimeState.GetSnapshot();
        Assert.Equal(initialProfile.Profile.Id, runtimeSnapshot.ActiveProfileId);
        Assert.Equal(switchResult.Value!.Generation, runtimeSnapshot.Generation);

        var persistedSelection = await profileService.GetCurrentSelectionAsync();
        Assert.Equal(targetProfile.Profile.Id, persistedSelection.ActiveProfileId);
    }
}

internal static class DatabaseRuntimeSwitchingTestProfiles
{
    public static DatabaseProfileEditorModel CreatePostgreSqlEditorForDatabase(
        string displayName,
        string databaseName,
        string workspaceRoot)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 5432,
            Database = databaseName,
            Username = "postgres",
            Password = "postgres"
        };

        return CreatePostgreSqlEditor(displayName, builder.ConnectionString, workspaceRoot);
    }

    public static DatabaseProfileEditorModel CreatePostgreSqlEditor(
        string displayName,
        string connectionString,
        string workspaceRoot)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new DatabaseProfileEditorModel
        {
            DisplayName = displayName,
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = builder.Host ?? "127.0.0.1",
            PostgresPort = builder.Port,
            PostgresDatabaseName = builder.Database ?? "candoitall",
            PostgresUsername = builder.Username ?? "postgres",
            PostgresPassword = builder.Password ?? string.Empty,
            PostgresAdminDatabaseName = builder.Database,
            WorkspaceRoot = workspaceRoot
        };
    }
}
