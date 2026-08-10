using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "UnixPortabilityCore")]
public sealed class SecretPortabilityIntegrationTests
{
    private const string Sentinel = "integration-secret-a04-f3014c";

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task Headless_external_wrapping_key_profile_restarts_on_current_host()
    {
        string root = CreateTempPath();
        byte[] key = RandomNumberGenerator.GetBytes(32);
        string encodedKey = Convert.ToBase64String(key);
        try
        {
            SecretVaultOptions options = CreateExternalOptions(root);
            var first = new ExternalWrappingKeySecretVault(
                options,
                CreateWriter(),
                variable => variable == "A04_INTEGRATION_KEY" ? encodedKey : null);
            Assert.True((await first.ProbeAsync()).IsAvailable);
            await first.SetAsync("restart/provider", Sentinel);

            var restarted = new ExternalWrappingKeySecretVault(
                options,
                CreateWriter(),
                variable => variable == "A04_INTEGRATION_KEY" ? encodedKey : null);
            Assert.Equal(Sentinel, await restarted.GetAsync("restart/provider"));

            string persisted = string.Join(
                '\n',
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            Assert.DoesNotContain(Sentinel, persisted, StringComparison.Ordinal);
            Assert.DoesNotContain(encodedKey, persisted, StringComparison.Ordinal);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(root));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Windows_dpapi_export_reencrypt_checkpoint_and_restart_preserve_value()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTempPath();
        byte[] destinationKey = RandomNumberGenerator.GetBytes(32);
        string encodedDestinationKey = Convert.ToBase64String(destinationKey);
        try
        {
            AppDbContextModelRegistry.ConfigureAssemblies([typeof(SecretRecord).Assembly]);
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"a04-dpapi-migration-{Guid.NewGuid():N}")
                .Options;
            var factory = new TestDbContextFactory(dbOptions);
            var source = new DpapiSecretVault(
                new SecretVaultOptions
                {
                    Provider = SecretVaultProviderKind.Dpapi,
                    VaultPath = Path.Combine(root, "dpapi"),
                    ApplicationName = "CanDoItAll.A04.Integration"
                },
                CreateWriter());
            string sourceKey = $"dpapi/{Guid.NewGuid():N}";
            await source.SetAsync(sourceKey, Sentinel);
            var record = new SecretRecord
            {
                Name = "dpapi-source",
                EncryptedPayload = SecretVaultRecordReference.Create(sourceKey),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            await using (AppDbContext dbContext = await factory.CreateDbContextAsync())
            {
                dbContext.Set<SecretRecord>().Add(record);
                await dbContext.SaveChangesAsync();
            }

            SecretVaultOptions externalOptions = CreateExternalOptions(Path.Combine(root, "portable"));
            ExternalWrappingKeySecretVault CreateDestination() => new(
                externalOptions,
                CreateWriter(),
                variable => variable == "A04_INTEGRATION_KEY" ? encodedDestinationKey : null);
            SecretMigrationOptions migrationOptions = new(
                Path.Combine(root, "migration-rollback"),
                SecretMigrationSourceSelection.VaultReferences);
            SecretMigrationCoordinator CreateCoordinator(
                ISecretVault destination,
                ISecretMigrationInterruptionObserver? interruptionObserver = null) => new(
                factory,
                new UnusedProtector(),
                source,
                destination,
                new EmptyControlPlaneContinuityVerifier(),
                CreateWriter(),
                new SystemClock(),
                new NullSecretMigrationAuditSink(),
                interruptionObserver);

            SecretMigrationReport dryRun = await CreateCoordinator(CreateDestination()).RunAsync(
                migrationOptions with { DryRun = true });
            Assert.True(dryRun.DryRun);
            Assert.Equal(Sentinel, await source.GetAsync(sourceKey));

            ISecretVault rollbackDestination = CreateDestination();
            SecretMigrationReport rollbackCandidate = await CreateCoordinator(rollbackDestination)
                .RunAsync(migrationOptions);
            Assert.Equal(1, rollbackCandidate.CommittedCount);
            SecretMigrationException rollbackInterruption = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(rollbackDestination, new FailAfterSourceReferenceRestoredObserver())
                    .RollbackAsync(migrationOptions));
            Assert.Equal("rollback-operation-failed", rollbackInterruption.ErrorCode);
            Assert.Equal(Sentinel, await source.GetAsync(sourceKey));
            SecretMigrationReport rolledBack = await CreateCoordinator(rollbackDestination)
                .RollbackAsync(migrationOptions);
            Assert.Equal(0, rolledBack.CommittedCount);
            Assert.Equal(Sentinel, await source.GetAsync(sourceKey));

            SecretMigrationOptions resumedOptions = migrationOptions with
            {
                MigrationRoot = Path.Combine(root, "migration-resume")
            };
            ISecretVault durableDestination = CreateDestination();
            await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(new FailFirstReadVault(durableDestination)).RunAsync(resumedOptions));
            SecretMigrationReport committed = await CreateCoordinator(durableDestination)
                .RunAsync(resumedOptions);
            Assert.Equal(1, committed.CommittedCount);
            SecretMigrationReport cleaned = await CreateCoordinator(durableDestination)
                .CompleteRestartCheckpointAsync(resumedOptions);
            Assert.Equal(1, cleaned.CleanedCount);
            Assert.Null(await source.GetAsync(sourceKey));

            await using AppDbContext assertContext = await factory.CreateDbContextAsync();
            string payload = await assertContext.Set<SecretRecord>()
                .Where(item => item.Id == record.Id)
                .Select(item => item.EncryptedPayload)
                .SingleAsync();
            Assert.True(SecretVaultRecordReference.TryParse(payload, out string migratedKey));
            Assert.Equal(Sentinel, await CreateDestination().GetAsync(migratedKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(destinationKey);
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task Linux_secret_service_actual_session_crud_and_restart_when_enabled()
    {
        if (!OperatingSystem.IsLinux() ||
            !string.Equals(
                Environment.GetEnvironmentVariable("CANDOITALL_SECRET_SERVICE_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.LinuxSecretService,
            UsageProfile = SecretVaultUsageProfile.Interactive,
            ApplicationName = $"CanDoItAll.A04.{Guid.NewGuid():N}"
        };
        var first = new LinuxSecretServiceVault(options);
        SecretVaultProbeResult probe = await first.ProbeAsync();
        Assert.True(probe.IsAvailable, $"Secret Service probe failed: {probe.Availability}. {probe.Remediation}");
        string value = Sentinel + "\nmultiline\n";
        await first.SetAsync("integration/key", value);

        var restarted = new LinuxSecretServiceVault(options);
        Assert.Equal(value, await restarted.GetAsync("integration/key"));
        await restarted.DeleteAsync("integration/key");
        Assert.Null(await first.GetAsync("integration/key"));
    }

    [Fact]
    [Trait("Category", "SecretPortability")]
    public async Task MacOs_keychain_actual_session_crud_and_restart_when_enabled()
    {
        if (!OperatingSystem.IsMacOS() ||
            !string.Equals(
                Environment.GetEnvironmentVariable("CANDOITALL_KEYCHAIN_INTEGRATION"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var options = new SecretVaultOptions
        {
            Provider = SecretVaultProviderKind.MacOsKeychain,
            UsageProfile = SecretVaultUsageProfile.Interactive,
            ApplicationName = $"CanDoItAll.A04.{Guid.NewGuid():N}"
        };
        var first = new MacOsKeychainSecretVault(options);
        SecretVaultProbeResult probe = await first.ProbeAsync();
        Assert.True(probe.IsAvailable, $"Keychain probe failed: {probe.Availability}. {probe.Remediation}");
        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(index => Task.Run(() => first.SetAsync("integration/key", $"concurrent-{index}"))));
        await first.SetAsync("integration/key", Sentinel);

        var restarted = new MacOsKeychainSecretVault(options);
        Assert.Equal(Sentinel, await restarted.GetAsync("integration/key"));
        await restarted.DeleteAsync("integration/key");
        Assert.Null(await first.GetAsync("integration/key"));
    }

    private static SecretVaultOptions CreateExternalOptions(string root)
        => new()
        {
            Provider = SecretVaultProviderKind.ExternalWrappingKeyFile,
            UsageProfile = SecretVaultUsageProfile.Headless,
            VaultPath = root,
            WrappingKeyId = "integration-generation",
            WrappingKeyEnvironmentVariable = "A04_INTEGRATION_KEY"
        };

    private static DurableFileWriter CreateWriter()
        => new(new PhysicalFileSystemPathPolicyFactory());

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "candoitall-a04-integration", Guid.NewGuid().ToString("N"));

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppDbContext(options));
    }

    private sealed class UnusedProtector : ISecretProtector
    {
        public string Protect(string plainText) => throw new NotSupportedException();

        public string Unprotect(string protectedValue) => throw new NotSupportedException();
    }

    private sealed class EmptyControlPlaneContinuityVerifier : IControlPlaneSecretContinuityVerifier
    {
        public Task<ControlPlaneSecretContinuityReport> VerifyAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ControlPlaneSecretContinuityReport(0));
    }

    private sealed class FailFirstReadVault(ISecretVault inner) : ISecretVault
    {
        private int remainingFailures = 1;

        public Task SetAsync(string key, string value, CancellationToken ct = default)
            => inner.SetAsync(key, value, ct);

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref remainingFailures, 0) == 1)
            {
                throw new IOException("Injected destination read interruption.");
            }

            return inner.GetAsync(key, ct);
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => inner.DeleteAsync(key, ct);
    }

    private sealed class FailAfterSourceReferenceRestoredObserver : ISecretMigrationInterruptionObserver
    {
        public ValueTask ObserveAsync(
            Guid secretRecordId,
            SecretMigrationInterruptionPoint point,
            CancellationToken cancellationToken)
            => throw new IOException("Injected post-restore interruption.");
    }
}
