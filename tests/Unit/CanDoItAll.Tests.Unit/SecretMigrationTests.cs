using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class SecretMigrationTests
{
    private const string Sentinel = "migration-sentinel-a04-a91dce";

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Dry_run_verifies_sources_without_writing_destination_or_journal()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            SecretRecord record = await AddLegacyRecordAsync(factory, protector, Sentinel);
            var destination = new RecordingVault();
            var audit = new RecordingAuditSink();
            SecretMigrationCoordinator coordinator = CreateCoordinator(
                factory,
                protector,
                new InMemorySecretVault(),
                destination,
                audit);

            SecretMigrationReport report = await coordinator.RunAsync(new SecretMigrationOptions(
                root,
                SecretMigrationSourceSelection.LegacyDataProtectionRecords,
                DryRun: true));

            Assert.True(report.DryRun);
            Assert.Equal(1, report.CandidateCount);
            Assert.Equal(0, report.CommittedCount);
            Assert.Empty(destination.Keys);
            Assert.False(Directory.Exists(root));
            Assert.Contains(audit.Events, item =>
                item.SecretRecordId == record.Id &&
                item.Stage == SecretMigrationAuditStage.DryRunVerified);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Legacy_data_protection_migration_survives_restart_checkpoint()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            SecretRecord source = await AddLegacyRecordAsync(factory, protector, Sentinel);
            var destination = new RecordingVault();
            var audit = new RecordingAuditSink();
            SecretMigrationOptions options = new(
                root,
                SecretMigrationSourceSelection.LegacyDataProtectionRecords);

            SecretMigrationReport staged = await CreateCoordinator(
                    factory,
                    protector,
                    new InMemorySecretVault(),
                    destination,
                    audit)
                .RunAsync(options);

            Assert.Equal(1, staged.CommittedCount);
            Assert.True(staged.RequiresRestartCheckpoint);
            string destinationKey = await ResolvePersistedVaultKeyAsync(factory, source.Id);
            Assert.Equal(Sentinel, await destination.GetAsync(destinationKey));

            SecretMigrationReport completed = await CreateCoordinator(
                    factory,
                    protector,
                    new InMemorySecretVault(),
                    destination,
                    audit)
                .CompleteRestartCheckpointAsync(options);

            Assert.Equal(1, completed.CleanedCount);
            Assert.False(completed.RequiresRestartCheckpoint);
            var restartedResolver = new SecretRuntimeResolver(factory, destination, protector);
            Assert.Equal(
                Sentinel,
                await restartedResolver.ResolveValueAsync(new SecretRuntimeRequest(
                    source.Id,
                    SecretRuntimePurposes.AgentProviderApiKey)));

            string artifacts = string.Join(
                '\n',
                Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            Assert.DoesNotContain(Sentinel, artifacts, StringComparison.Ordinal);
            Assert.All(audit.Events, item => Assert.Null(item.ErrorCode));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Interrupted_destination_verification_resumes_and_rolls_back_without_orphan()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            var sourceVault = new InMemorySecretVault();
            var durableDestination = new RecordingVault();
            var failingDestination = new FailFirstReadVault(durableDestination);
            VaultRecordFixture source = await AddVaultRecordAsync(factory, sourceVault, Sentinel);
            SecretMigrationOptions options = new(root, SecretMigrationSourceSelection.VaultReferences);

            SecretMigrationException exception = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(factory, protector, sourceVault, failingDestination)
                    .RunAsync(options));
            Assert.Equal("migration-operation-failed", exception.ErrorCode);

            SecretMigrationReport resumed = await CreateCoordinator(
                    factory,
                    protector,
                    sourceVault,
                    durableDestination)
                .RunAsync(options);
            Assert.Equal(1, resumed.CommittedCount);

            SecretMigrationReport rolledBack = await CreateCoordinator(
                    factory,
                    protector,
                    sourceVault,
                    durableDestination)
                .RollbackAsync(options);
            Assert.Equal(0, rolledBack.CommittedCount);
            Assert.Equal(Sentinel, await sourceVault.GetAsync(source.SourceVaultKey));
            Assert.Empty(durableDestination.Keys);

            await using AppDbContext dbContext = await factory.CreateDbContextAsync();
            SecretRecord restored = await dbContext.Set<SecretRecord>().SingleAsync(item => item.Id == source.Record.Id);
            Assert.Equal(source.OriginalPayload, restored.EncryptedPayload);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Rollback_resumes_after_source_reference_restore_interruption()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            var sourceVault = new InMemorySecretVault();
            var destinationVault = new RecordingVault();
            var audit = new RecordingAuditSink();
            VaultRecordFixture source = await AddVaultRecordAsync(factory, sourceVault, Sentinel);
            SecretMigrationOptions options = new(root, SecretMigrationSourceSelection.VaultReferences);

            await CreateCoordinator(factory, protector, sourceVault, destinationVault, audit)
                .RunAsync(options);
            string destinationKey = await ResolvePersistedVaultKeyAsync(factory, source.Record.Id);

            SecretMigrationException interruption = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(
                        factory,
                        protector,
                        sourceVault,
                        destinationVault,
                        audit,
                        new FailAfterSourceReferenceRestoredObserver())
                    .RollbackAsync(options));

            Assert.Equal("rollback-operation-failed", interruption.ErrorCode);
            await using (AppDbContext interruptedContext = await factory.CreateDbContextAsync())
            {
                SecretRecord restored = await interruptedContext.Set<SecretRecord>()
                    .SingleAsync(item => item.Id == source.Record.Id);
                Assert.Equal(source.OriginalPayload, restored.EncryptedPayload);
            }

            Assert.Equal(Sentinel, await sourceVault.GetAsync(source.SourceVaultKey));
            Assert.Equal(Sentinel, await destinationVault.GetAsync(destinationKey));

            SecretMigrationReport rolledBack = await CreateCoordinator(
                    factory,
                    protector,
                    sourceVault,
                    destinationVault,
                    audit)
                .RollbackAsync(options);

            Assert.Equal(0, rolledBack.CommittedCount);
            Assert.Empty(destinationVault.Keys);
            Assert.Contains(audit.Events, item =>
                item.Stage == SecretMigrationAuditStage.Failed &&
                item.ErrorCode == "rollback-operation-failed");
            Assert.Contains(audit.Events, item => item.Stage == SecretMigrationAuditStage.RolledBack);
            Assert.DoesNotContain(Sentinel, JsonSerializer.Serialize(audit.Events), StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Rollback_verifies_legacy_source_before_publishing_restored_reference()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            SecretRecord source = await AddLegacyRecordAsync(factory, protector, Sentinel);
            var destinationVault = new RecordingVault();
            SecretMigrationOptions options = new(
                root,
                SecretMigrationSourceSelection.LegacyDataProtectionRecords);

            await CreateCoordinator(
                    factory,
                    protector,
                    new InMemorySecretVault(),
                    destinationVault)
                .RunAsync(options);
            string destinationKey = await ResolvePersistedVaultKeyAsync(factory, source.Id);

            SecretMigrationException interruption = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(
                        factory,
                        new FailFirstUnprotectProtector(protector),
                        new InMemorySecretVault(),
                        destinationVault)
                    .RollbackAsync(options));

            Assert.Equal("legacy-data-protection-unreadable", interruption.ErrorCode);
            await using (AppDbContext interruptedContext = await factory.CreateDbContextAsync())
            {
                string persistedPayload = await interruptedContext.Set<SecretRecord>()
                    .Where(item => item.Id == source.Id)
                    .Select(item => item.EncryptedPayload)
                    .SingleAsync();
                Assert.True(SecretVaultRecordReference.TryParse(persistedPayload, out string persistedKey));
                Assert.Equal(destinationKey, persistedKey);
            }

            SecretMigrationReport rolledBack = await CreateCoordinator(
                    factory,
                    protector,
                    new InMemorySecretVault(),
                    destinationVault)
                .RollbackAsync(options);

            Assert.Equal(0, rolledBack.CommittedCount);
            Assert.Empty(destinationVault.Keys);
            await using AppDbContext completedContext = await factory.CreateDbContextAsync();
            SecretRecord restored = await completedContext.Set<SecretRecord>()
                .SingleAsync(item => item.Id == source.Id);
            Assert.Equal(source.EncryptedPayload, restored.EncryptedPayload);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Migration_fails_closed_when_source_record_changes_after_prepare()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            SecretRecord source = await AddLegacyRecordAsync(factory, protector, Sentinel);
            var destination = new RecordingVault();
            var failFirstRead = new FailFirstReadVault(destination);
            SecretMigrationOptions options = new(
                root,
                SecretMigrationSourceSelection.LegacyDataProtectionRecords);

            await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(factory, protector, new InMemorySecretVault(), failFirstRead)
                    .RunAsync(options));
            await using (AppDbContext dbContext = await factory.CreateDbContextAsync())
            {
                SecretRecord changed = await dbContext.Set<SecretRecord>().SingleAsync(item => item.Id == source.Id);
                changed.EncryptedPayload = protector.Protect("concurrent-update");
                await dbContext.SaveChangesAsync();
            }

            SecretMigrationException exception = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                CreateCoordinator(factory, protector, new InMemorySecretVault(), destination)
                    .RunAsync(options));

            Assert.Equal("source-record-changed", exception.ErrorCode);
            Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Resume_rejects_changed_source_selection_and_tampered_authority_state()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            await AddLegacyRecordAsync(factory, protector, Sentinel);
            var destination = new RecordingVault();
            SecretMigrationOptions options = new(
                root,
                SecretMigrationSourceSelection.LegacyDataProtectionRecords);
            SecretMigrationCoordinator coordinator = CreateCoordinator(
                factory,
                protector,
                new InMemorySecretVault(),
                destination);

            await coordinator.RunAsync(options);

            await Assert.ThrowsAsync<InvalidDataException>(() => coordinator.RunAsync(
                options with { SourceSelection = SecretMigrationSourceSelection.VaultReferences }));

            string journalPath = Path.Combine(root, "secret-migration.v1.json");
            JsonNode journal = JsonNode.Parse(await File.ReadAllTextAsync(journalPath))!;
            journal["records"]![0]!["destinationVaultKey"] = "migration/tampered";
            await File.WriteAllTextAsync(journalPath, journal.ToJsonString());

            await Assert.ThrowsAsync<InvalidDataException>(() => coordinator.RunAsync(options));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    [Fact]
    [Trait("Category", "SecretMigration")]
    public async Task Restart_checkpoint_preserves_source_when_destination_changed_after_commit()
    {
        string root = CreateTempPath();
        try
        {
            TestDbContextFactory factory = CreateDbContextFactory();
            var protector = new OpaqueTestProtector();
            var sourceVault = new InMemorySecretVault();
            var destinationVault = new RecordingVault();
            VaultRecordFixture source = await AddVaultRecordAsync(factory, sourceVault, Sentinel);
            SecretMigrationOptions options = new(root, SecretMigrationSourceSelection.VaultReferences);
            SecretMigrationCoordinator coordinator = CreateCoordinator(
                factory,
                protector,
                sourceVault,
                destinationVault);

            await coordinator.RunAsync(options);
            string destinationKey = await ResolvePersistedVaultKeyAsync(factory, source.Record.Id);
            await destinationVault.SetAsync(destinationKey, "changed-after-commit");

            SecretMigrationException exception = await Assert.ThrowsAsync<SecretMigrationException>(() =>
                coordinator.CompleteRestartCheckpointAsync(options));

            Assert.Equal("committed-destination-verification-failed", exception.ErrorCode);
            Assert.Equal(Sentinel, await sourceVault.GetAsync(source.SourceVaultKey));
        }
        finally
        {
            DeleteIfExists(root);
        }
    }

    private static SecretMigrationCoordinator CreateCoordinator(
        IDbContextFactory<AppDbContext> factory,
        ISecretProtector protector,
        ISecretVault source,
        ISecretVault destination,
        ISecretMigrationAuditSink? auditSink = null,
        ISecretMigrationInterruptionObserver? interruptionObserver = null)
        => new(
            factory,
            protector,
            source,
            destination,
            new TestControlPlaneContinuityVerifier(2),
            new DurableFileWriter(TestWorkspaceServices.PhysicalPathPolicyFactory),
            new FixedClock(),
            auditSink ?? new NullSecretMigrationAuditSink(),
            interruptionObserver);

    private static async Task<SecretRecord> AddLegacyRecordAsync(
        IDbContextFactory<AppDbContext> factory,
        ISecretProtector protector,
        string value)
    {
        var record = new SecretRecord
        {
            Name = "legacy",
            EncryptedPayload = protector.Protect(value),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();
        dbContext.Set<SecretRecord>().Add(record);
        await dbContext.SaveChangesAsync();
        return record;
    }

    private static async Task<VaultRecordFixture> AddVaultRecordAsync(
        IDbContextFactory<AppDbContext> factory,
        ISecretVault sourceVault,
        string value)
    {
        Guid id = Guid.NewGuid();
        string key = $"legacy-dpapi/{id:N}";
        await sourceVault.SetAsync(key, value);
        string payload = SecretVaultRecordReference.Create(key);
        var record = new SecretRecord
        {
            Id = id,
            Name = "vault-source",
            EncryptedPayload = payload,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();
        dbContext.Set<SecretRecord>().Add(record);
        await dbContext.SaveChangesAsync();
        return new VaultRecordFixture(record, key, payload);
    }

    private static async Task<string> ResolvePersistedVaultKeyAsync(
        IDbContextFactory<AppDbContext> factory,
        Guid secretId)
    {
        await using AppDbContext dbContext = await factory.CreateDbContextAsync();
        string payload = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == secretId)
            .Select(item => item.EncryptedPayload)
            .SingleAsync();
        Assert.True(SecretVaultRecordReference.TryParse(payload, out string key));
        return key;
    }

    private static TestDbContextFactory CreateDbContextFactory()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(SecretRecord).Assembly]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"secret-migration-{Guid.NewGuid():N}")
            .Options;
        return new TestDbContextFactory(options);
    }

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "candoitall-a04-migration-tests", Guid.NewGuid().ToString("N"));

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

    private sealed class OpaqueTestProtector : ISecretProtector
    {
        public string Protect(string plainText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] ^= 0x5A;
            }

            return Convert.ToBase64String(bytes);
        }

        public string Unprotect(string protectedValue)
        {
            byte[] bytes = Convert.FromBase64String(protectedValue);
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] ^= 0x5A;
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }

    private sealed class RecordingVault : ISecretVault
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => values.Keys;

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(values.GetValueOrDefault(key));

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            values.Remove(key);
            return Task.CompletedTask;
        }
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

    private sealed class FailFirstUnprotectProtector(ISecretProtector inner) : ISecretProtector
    {
        private int remainingFailures = 1;

        public string Protect(string plainText) => inner.Protect(plainText);

        public string Unprotect(string protectedValue)
        {
            if (Interlocked.Exchange(ref remainingFailures, 0) == 1)
            {
                throw new InvalidOperationException("Injected source verification interruption.");
            }

            return inner.Unprotect(protectedValue);
        }
    }

    private sealed class FailAfterSourceReferenceRestoredObserver : ISecretMigrationInterruptionObserver
    {
        public ValueTask ObserveAsync(
            Guid secretRecordId,
            SecretMigrationInterruptionPoint point,
            CancellationToken cancellationToken)
            => throw new IOException("Injected post-restore interruption.");
    }

    private sealed class TestControlPlaneContinuityVerifier(int count)
        : IControlPlaneSecretContinuityVerifier
    {
        public Task<ControlPlaneSecretContinuityReport> VerifyAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ControlPlaneSecretContinuityReport(count));
    }

    private sealed class RecordingAuditSink : ISecretMigrationAuditSink
    {
        public List<SecretMigrationAuditEvent> Events { get; } = [];

        public ValueTask RecordAsync(
            SecretMigrationAuditEvent auditEvent,
            CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => new(2026, 8, 9, 16, 0, 0, TimeSpan.Zero);
    }

    private sealed record VaultRecordFixture(
        SecretRecord Record,
        string SourceVaultKey,
        string OriginalPayload);
}
