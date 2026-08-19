using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageCatalogService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkspacePathResolver workspacePathResolver,
    IClock clock) : IStorageCatalogService, IStorageCatalogPathMigrationService
{
    private const string BootstrapStorageName = "Workspace file system";
    private const string BootstrapRoutingRuleName = "Workspace editable fallback";
    private static readonly Guid BootstrapRoutingRuleId = Guid.Parse("fbb91e1a-f1fc-4261-8baf-76c2de2730b9");
    private static readonly DurableFileWriter MigrationFileWriter =
        new(new PhysicalFileSystemPathPolicyFactory());
    private static readonly JsonSerializerOptions MigrationSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<StorageCatalogRecord> storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return storages;
    }

    public async Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<StorageCatalogRecord> systemDefaults = await dbContext.Set<StorageCatalogRecord>()
            .Where(item => item.IsSystemDefault)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        StorageCatalogRecord? existingStorage = ResolveTrustedBootstrapStorage(systemDefaults);
        var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        if (existingStorage is not null)
        {
            EnsureAuthoritativeBootstrapStorage(systemDefaults, existingStorage, workspaceRoot);
            RefreshBootstrapStorage(existingStorage, workspaceRoot);
            await dbContext.SaveChangesAsync(cancellationToken);
            await MigrateLegacyRootBindingsAsync(dbContext, workspaceRoot, cancellationToken);
            await EnsureBootstrapRuleAsync(dbContext, existingStorage, cancellationToken);
            return existingStorage;
        }

        var storage = new StorageCatalogRecord
        {
            Name = BootstrapStorageName,
            ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = true,
            IsSystemDefault = true,
            ConnectionMode = StorageConnectionMode.Local,
            EndpointOrRoot = workspaceRoot,
            CapabilityMask =
                StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.Download |
                StorageCapability.InlinePreview |
                StorageCapability.OpenLocally |
                StorageCapability.MutableUpdate |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            HealthStatus = StorageHealthStatus.Healthy,
            LastHealthMessage = "Bootstrap workspace storage",
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };
        StorageCatalogHostBindingPolicy.BindCurrent(storage, workspaceRoot, clock.GetUtcNow());

        await dbContext.Set<StorageCatalogRecord>().AddAsync(storage, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var recoveredStorage = await TryRecoverBootstrapStorageAfterConcurrentInsertAsync(workspaceRoot, cancellationToken);
            if (recoveredStorage is not null)
            {
                return recoveredStorage;
            }

            throw;
        }

        await MigrateLegacyRootBindingsAsync(dbContext, workspaceRoot, cancellationToken);
        EnsureAuthoritativeBootstrapStorage([storage], storage, workspaceRoot);
        await EnsureBootstrapRuleAsync(dbContext, storage, cancellationToken);
        return storage;
    }

    public async Task<StorageCatalogPathMigrationReport> DryRunAsync(
        CancellationToken cancellationToken = default)
    {
        string workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<StorageCatalogRecord> legacy = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .Where(storage =>
                storage.ProviderKind == StorageProviderKind.FileSystem &&
                storage.RootBindingFormatVersion == 0)
            .OrderBy(storage => storage.Id)
            .ToListAsync(cancellationToken);
        return CreateMigrationReport(
            isDryRun: true,
            legacy.Count == 0 ? StorageCatalogPathMigrationState.NoChanges : StorageCatalogPathMigrationState.Discovered,
            legacy,
            backupSha256: string.Empty,
            targetSha256: string.Empty);
    }

    public async Task<StorageCatalogPathMigrationReport> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        string workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await MigrateLegacyRootBindingsAsync(dbContext, workspaceRoot, cancellationToken);
    }

    public async Task<StorageCatalogPathMigrationReport> RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        string workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        string migrationRoot = ResolveMigrationRoot(workspaceRoot);
        string backupPath = Path.Combine(migrationRoot, "storage-catalog.v1.backup.json");
        if (!File.Exists(backupPath))
        {
            throw new InvalidOperationException("No storage-catalog host-binding migration backup is available.");
        }

        string backupJson = await MigrationBackupIntegrity.ReadVerifiedAsync(
            backupPath,
            cancellationToken);
        StorageCatalogMigrationBackup backup = JsonSerializer.Deserialize<StorageCatalogMigrationBackup>(
            backupJson,
            MigrationSerializerOptions)
            ?? throw new InvalidOperationException("The storage-catalog migration backup is empty.");
        if (backup.FormatVersion != 1)
        {
            throw new InvalidOperationException("The storage-catalog migration backup format is unsupported.");
        }

        string commitPath = Path.Combine(migrationRoot, "commit.json");
        if (File.Exists(commitPath))
        {
            StorageCatalogMigrationCommit commit = DeserializeCommit(
                await File.ReadAllTextAsync(commitPath, cancellationToken));
            if (!string.Equals(commit.BackupSha256, ComputeSha256(backupJson), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The storage-catalog migration backup checksum is invalid.");
            }
        }

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        List<StorageCatalogRecord> restored = [];
        foreach (StorageCatalogMigrationBackupItem item in backup.Records)
        {
            StorageCatalogRecord storage = await dbContext.Set<StorageCatalogRecord>()
                .SingleAsync(record => record.Id == item.Id, cancellationToken);
            item.Restore(storage);
            restored.Add(storage);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        string rollbackJson = JsonSerializer.Serialize(new
        {
            formatVersion = 1,
            state = StorageCatalogPathMigrationState.RolledBack,
            recordCount = restored.Count,
            rolledBackAtUtc = clock.GetUtcNow()
        }, MigrationSerializerOptions);
        await MigrationFileWriter.WriteTextAsync(
            workspaceRoot,
            Path.Combine(migrationRoot, "rollback.commit.json"),
            rollbackJson,
            DurableFileWriteOptions.Private,
            cancellationToken: cancellationToken);
        return CreateMigrationReport(
            isDryRun: false,
            StorageCatalogPathMigrationState.RolledBack,
            restored,
            ComputeSha256(backupJson),
            string.Empty);
    }

    public async Task<StorageCatalogRecord> RebindRootAsync(
        Guid storageId,
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        string resolvedRoot = ControlPlane.ControlPlanePathDefaults.ResolveConfiguredPath(
            Directory.GetCurrentDirectory(),
            rootPath);
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        StorageCatalogRecord storage = await dbContext.Set<StorageCatalogRecord>()
            .SingleOrDefaultAsync(item => item.Id == storageId, cancellationToken)
            ?? throw new InvalidOperationException("The storage catalog entry to rebind was not found.");
        if (storage.ProviderKind != StorageProviderKind.FileSystem)
        {
            throw new InvalidOperationException("Only filesystem storage entries own host-bound roots.");
        }

        if (storage.IsSystemDefault)
        {
            string workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
            IPhysicalFileSystemPathPolicy workspacePolicy = new PhysicalFileSystemPathPolicyFactory()
                .Create(workspaceRoot);
            if (!workspacePolicy.PathComparer.Equals(resolvedRoot, workspacePolicy.RootPath))
            {
                throw new InvalidOperationException(
                    "The system-default filesystem storage can only be rebound to the current workspace root.");
            }
        }

        StorageCatalogHostBindingPolicy.BindCurrent(storage, resolvedRoot, clock.GetUtcNow());
        storage.IsEnabled = true;
        storage.HealthStatus = StorageHealthStatus.Unknown;
        storage.LastHealthMessage = "Storage root was explicitly rebound for the current host.";
        storage.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return storage;
    }

    public async Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        StorageJson.ParseProviderConfiguration(record.ConfigJson);

        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<StorageCatalogRecord>()
            .FirstOrDefaultAsync(item => item.Id == record.Id, cancellationToken);

        if (entity is null)
        {
            entity = new StorageCatalogRecord
            {
                CreatedAtUtc = clock.GetUtcNow()
            };
            await dbContext.Set<StorageCatalogRecord>().AddAsync(entity, cancellationToken);
        }

        entity.Name = string.IsNullOrWhiteSpace(record.Name) ? $"Storage {record.ProviderKind}" : record.Name.Trim();
        entity.ProviderKind = record.ProviderKind;
        entity.IsEnabled = record.IsEnabled;
        entity.IsSystemDefault = record.IsSystemDefault;
        entity.IsReadOnly = record.IsReadOnly;
        entity.DisplayOrder = record.DisplayOrder;
        entity.ConnectionMode = record.ConnectionMode;
        entity.EndpointOrRoot = record.EndpointOrRoot?.Trim() ?? string.Empty;
        entity.ConfigJson = string.IsNullOrWhiteSpace(record.ConfigJson) ? "{}" : record.ConfigJson;
        entity.CapabilityMask = record.CapabilityMask;
        entity.HealthStatus = record.HealthStatus;
        entity.LastTestedAtUtc = record.LastTestedAtUtc;
        entity.LastHealthMessage = record.LastHealthMessage?.Trim() ?? string.Empty;
        entity.CredentialSecretId = record.CredentialSecretId;
        entity.UpdatedAtUtc = clock.GetUtcNow();
        if (entity.ProviderKind == StorageProviderKind.FileSystem)
        {
            string configuredRoot = string.IsNullOrWhiteSpace(entity.EndpointOrRoot)
                ? workspacePathResolver.ResolveWorkspaceRoot()
                : ControlPlane.ControlPlanePathDefaults.ResolveConfiguredPath(
                    Directory.GetCurrentDirectory(),
                    entity.EndpointOrRoot);
            StorageCatalogHostBindingPolicy.BindCurrent(entity, configuredRoot, clock.GetUtcNow());
        }
        else
        {
            ClearRootBinding(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storage = await dbContext.Set<StorageCatalogRecord>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (storage is null || storage.IsSystemDefault)
        {
            return;
        }

        var rules = await dbContext.Set<StorageRoutingRule>()
            .Where(item => item.PreferredStorageId == id)
            .ToListAsync(cancellationToken);
        if (rules.Count > 0)
        {
            dbContext.RemoveRange(rules);
        }

        dbContext.Remove(storage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<StorageRoutingRule>()
            .AsNoTracking()
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<StorageRoutingRule>()
            .FirstOrDefaultAsync(item => item.Id == rule.Id, cancellationToken);
        if (entity is null)
        {
            entity = new StorageRoutingRule
            {
                CreatedAtUtc = clock.GetUtcNow()
            };
            await dbContext.Set<StorageRoutingRule>().AddAsync(entity, cancellationToken);
        }

        entity.Name = string.IsNullOrWhiteSpace(rule.Name) ? "Storage routing rule" : rule.Name.Trim();
        entity.IsEnabled = rule.IsEnabled;
        entity.Priority = rule.Priority;
        entity.ScopeKind = rule.ScopeKind;
        entity.ProjectId = rule.ProjectId;
        entity.NodeKey = rule.NodeKey?.Trim() ?? string.Empty;
        entity.UsagePurpose = rule.UsagePurpose;
        entity.ContentKind = rule.ContentKind;
        entity.MimePattern = rule.MimePattern?.Trim() ?? string.Empty;
        entity.MinimumContentLength = rule.MinimumContentLength;
        entity.MaximumContentLength = rule.MaximumContentLength;
        entity.EditIntent = rule.EditIntent;
        entity.PreviewRequired = rule.PreviewRequired;
        entity.PublishIntent = rule.PublishIntent;
        entity.RequiredCapabilities = rule.RequiredCapabilities;
        entity.PreferredStorageId = rule.PreferredStorageId;
        entity.AlternativeStorageIdsJson = string.IsNullOrWhiteSpace(rule.AlternativeStorageIdsJson)
            ? "[]"
            : rule.AlternativeStorageIdsJson;
        entity.Reason = rule.Reason?.Trim() ?? string.Empty;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private async Task EnsureBootstrapRuleAsync(
        AppDbContext dbContext,
        StorageCatalogRecord storage,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.Set<StorageRoutingRule>()
            .FirstOrDefaultAsync(item =>
                item.Id == BootstrapRoutingRuleId ||
                (item.ScopeKind == StorageRoutingScopeKind.Workspace &&
                 item.PreferredStorageId == storage.Id &&
                 item.Name == BootstrapRoutingRuleName),
                cancellationToken);
        if (rule is not null)
        {
            return;
        }

        rule = new StorageRoutingRule
        {
            Id = BootstrapRoutingRuleId,
            Name = BootstrapRoutingRuleName,
            ScopeKind = StorageRoutingScopeKind.Workspace,
            UsagePurpose = StorageUsagePurpose.Unknown,
            ContentKind = StorageContentKind.Unknown,
            Priority = 1000,
            PreferredStorageId = storage.Id,
            Reason = "Bootstrap filesystem fallback for editable-first content.",
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await dbContext.Set<StorageRoutingRule>().AddAsync(rule, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(rule).State = EntityState.Detached;
            if (await dbContext.Set<StorageRoutingRule>().AnyAsync(item => item.Id == BootstrapRoutingRuleId, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    private async Task<StorageCatalogRecord?> TryRecoverBootstrapStorageAfterConcurrentInsertAsync(
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<StorageCatalogRecord> systemDefaults = await dbContext.Set<StorageCatalogRecord>()
            .Where(item => item.IsSystemDefault)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        StorageCatalogRecord? storage = ResolveTrustedBootstrapStorage(systemDefaults);
        if (storage is null)
        {
            return null;
        }

        EnsureAuthoritativeBootstrapStorage(systemDefaults, storage, workspaceRoot);
        RefreshBootstrapStorage(storage, workspaceRoot);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapRuleAsync(dbContext, storage, cancellationToken);
        return storage;
    }

    private static StorageCatalogRecord? ResolveTrustedBootstrapStorage(
        IReadOnlyCollection<StorageCatalogRecord> systemDefaults)
    {
        if (systemDefaults.Count == 0)
        {
            return null;
        }

        if (systemDefaults.Count != 1)
        {
            throw new InvalidOperationException(
                "The storage catalog contains multiple system-default entries. Resolve the catalog conflict before bootstrap.");
        }

        StorageCatalogRecord storage = systemDefaults.Single();
        if (storage.ProviderKind != StorageProviderKind.FileSystem ||
            !string.Equals(storage.Name, BootstrapStorageName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The system-default storage is not the trusted workspace filesystem bootstrap entry. It was left unchanged.");
        }

        return storage;
    }

    private static void EnsureAuthoritativeBootstrapStorage(
        IReadOnlyCollection<StorageCatalogRecord> systemDefaults,
        StorageCatalogRecord expected,
        string workspaceRoot)
    {
        StorageCatalogRecord? authoritative = StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
            systemDefaults,
            workspaceRoot);
        if (authoritative?.Id != expected.Id)
        {
            throw new InvalidOperationException(
                "The trusted workspace filesystem bootstrap entry is not authoritative for the current workspace root.");
        }
    }

    private void RefreshBootstrapStorage(StorageCatalogRecord storage, string workspaceRoot)
    {
        var capabilityMask =
            StorageCapability.Read |
            StorageCapability.Write |
            StorageCapability.Delete |
            StorageCapability.Download |
            StorageCapability.InlinePreview |
            StorageCapability.OpenLocally |
            StorageCapability.MutableUpdate |
            StorageCapability.BatchFolderUpload |
            StorageCapability.BatchTransfer |
            StorageCapability.ConnectionTest;

        storage.Name = BootstrapStorageName;
        storage.ProviderKind = StorageProviderKind.FileSystem;
        storage.IsEnabled = true;
        storage.IsSystemDefault = true;
        storage.ConnectionMode = StorageConnectionMode.Local;
        storage.EndpointOrRoot = workspaceRoot;
        StorageCatalogHostBindingPolicy.BindCurrent(storage, workspaceRoot, clock.GetUtcNow());
        storage.CapabilityMask = capabilityMask;
        storage.HealthStatus = StorageHealthStatus.Healthy;
        storage.LastHealthMessage = "Bootstrap workspace storage";
        storage.UpdatedAtUtc = clock.GetUtcNow();
    }

    private static void ClearRootBinding(StorageCatalogRecord storage)
    {
        storage.RootBindingFormatVersion = 0;
        storage.RootPlatformFamily = HostPlatformFamily.Unknown;
        storage.RootPathSyntax = default;
        storage.RootHostBindingId = string.Empty;
        storage.RootPathState = HostBoundPathState.NeedsRebind;
        storage.RootLastValidatedAtUtc = null;
    }

    private async Task<StorageCatalogPathMigrationReport> MigrateLegacyRootBindingsAsync(
        AppDbContext dbContext,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        string migrationRoot = ResolveMigrationRoot(workspaceRoot);
        string backupPath = Path.Combine(migrationRoot, "storage-catalog.v1.backup.json");
        string stagedPath = Path.Combine(migrationRoot, "storage-catalog.v2.staged.json");
        string commitPath = Path.Combine(migrationRoot, "commit.json");
        List<StorageCatalogRecord> legacy = await dbContext.Set<StorageCatalogRecord>()
            .Where(storage =>
                storage.ProviderKind == StorageProviderKind.FileSystem &&
                storage.RootBindingFormatVersion == 0)
            .OrderBy(storage => storage.Id)
            .ToListAsync(cancellationToken);
        if (legacy.Count == 0)
        {
            StorageCatalogPathMigrationReport? repaired = await TryRepairCommitMarkerAsync(
                dbContext,
                workspaceRoot,
                backupPath,
                stagedPath,
                commitPath,
                cancellationToken);
            if (repaired is not null)
            {
                return repaired;
            }

            return CreateMigrationReport(
                isDryRun: false,
                StorageCatalogPathMigrationState.NoChanges,
                legacy,
                string.Empty,
                string.Empty);
        }

        MigrationFileWriter.EnsureDirectory(workspaceRoot, migrationRoot, requirePrivateUnixMode: true);
        if (File.Exists(backupPath) && File.Exists(commitPath))
        {
            throw new InvalidOperationException(
                "New legacy storage roots were discovered after a completed migration. Run an explicit operator migration generation.");
        }

        var backup = new StorageCatalogMigrationBackup
        {
            Records = legacy.Select(StorageCatalogMigrationBackupItem.Create).ToList()
        };
        string sourceBackupJson = JsonSerializer.Serialize(backup, MigrationSerializerOptions);
        string backupJson = await MigrationBackupIntegrity.CreateOrVerifyAsync(
            MigrationFileWriter,
            workspaceRoot,
            backupPath,
            sourceBackupJson,
            cancellationToken);

        foreach (StorageCatalogRecord storage in legacy)
        {
            StorageCatalogHostBindingPolicy.ImportLegacy(
                storage,
                workspaceRoot);
        }

        string targetJson = JsonSerializer.Serialize(
            legacy.Select(StorageCatalogMigrationBackupItem.Create).ToList(),
            MigrationSerializerOptions);
        foreach (StorageCatalogRecord storage in legacy.Where(item => item.RootPathState == HostBoundPathState.Active))
        {
            StorageCatalogHostBindingPolicy.ResolveRequired(storage, workspaceRoot);
        }

        await MigrationFileWriter.WriteTextAsync(
            workspaceRoot,
            stagedPath,
            targetJson,
            DurableFileWriteOptions.Private,
            cancellationToken: cancellationToken);
        await using IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        string backupSha256 = ComputeSha256(backupJson);
        string targetSha256 = ComputeSha256(targetJson);
        await WriteCommitMarkerAsync(
            workspaceRoot,
            commitPath,
            legacy.Count,
            backupSha256,
            targetSha256,
            cancellationToken);
        return CreateMigrationReport(
            isDryRun: false,
            StorageCatalogPathMigrationState.PointerCommitted,
            legacy,
            backupSha256,
            targetSha256);
    }

    private async Task<StorageCatalogPathMigrationReport?> TryRepairCommitMarkerAsync(
        AppDbContext dbContext,
        string workspaceRoot,
        string backupPath,
        string stagedPath,
        string commitPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(backupPath) || !File.Exists(stagedPath) || File.Exists(commitPath))
        {
            return null;
        }

        string backupJson = await MigrationBackupIntegrity.ReadVerifiedAsync(
            backupPath,
            cancellationToken);
        string targetJson = await File.ReadAllTextAsync(stagedPath, cancellationToken);
        List<StorageCatalogMigrationBackupItem> staged = JsonSerializer.Deserialize<List<StorageCatalogMigrationBackupItem>>(
            targetJson,
            MigrationSerializerOptions)
            ?? throw new InvalidOperationException("The staged storage-catalog migration payload is empty.");
        var committed = new List<StorageCatalogRecord>(staged.Count);
        foreach (StorageCatalogMigrationBackupItem stagedItem in staged)
        {
            StorageCatalogRecord storage = await dbContext.Set<StorageCatalogRecord>()
                .AsNoTracking()
                .SingleAsync(record => record.Id == stagedItem.Id, cancellationToken);
            if (!stagedItem.Matches(storage))
            {
                throw new InvalidOperationException(
                    "The staged storage-catalog migration does not match the committed catalog state.");
            }

            committed.Add(storage);
        }

        string backupSha256 = ComputeSha256(backupJson);
        string targetSha256 = ComputeSha256(targetJson);
        await WriteCommitMarkerAsync(
            workspaceRoot,
            commitPath,
            committed.Count,
            backupSha256,
            targetSha256,
            cancellationToken);
        return CreateMigrationReport(
            isDryRun: false,
            StorageCatalogPathMigrationState.PointerCommitted,
            committed,
            backupSha256,
            targetSha256);
    }

    private async Task WriteCommitMarkerAsync(
        string workspaceRoot,
        string commitPath,
        int recordCount,
        string backupSha256,
        string targetSha256,
        CancellationToken cancellationToken)
    {
        string commitJson = JsonSerializer.Serialize(new StorageCatalogMigrationCommit
        {
            RecordCount = recordCount,
            BackupSha256 = backupSha256,
            TargetSha256 = targetSha256,
            CommittedAtUtc = clock.GetUtcNow()
        }, MigrationSerializerOptions);
        await MigrationFileWriter.WriteTextAsync(
            workspaceRoot,
            commitPath,
            commitJson,
            DurableFileWriteOptions.Private,
            cancellationToken: cancellationToken);
    }

    private static StorageCatalogPathMigrationReport CreateMigrationReport(
        bool isDryRun,
        StorageCatalogPathMigrationState state,
        IReadOnlyCollection<StorageCatalogRecord> records,
        string backupSha256,
        string targetSha256)
    {
        return new StorageCatalogPathMigrationReport(
            isDryRun,
            state,
            records.Count,
            records.Select(record => record.Id).Order().ToArray(),
            backupSha256,
            targetSha256);
    }

    private static string ResolveMigrationRoot(string workspaceRoot)
        => Path.Combine(
            workspaceRoot,
            ".candoitall",
            "migrations",
            "storage-catalog-host-binding-v1");

    private static string ComputeSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static StorageCatalogMigrationCommit DeserializeCommit(string json)
    {
        StorageCatalogMigrationCommit commit = JsonSerializer.Deserialize<StorageCatalogMigrationCommit>(
            json,
            MigrationSerializerOptions)
            ?? throw new InvalidOperationException("The storage-catalog migration commit marker is empty.");
        if (commit.FormatVersion != 1 || commit.State != StorageCatalogPathMigrationState.PointerCommitted)
        {
            throw new InvalidOperationException("The storage-catalog migration commit marker is invalid.");
        }

        return commit;
    }

    private sealed class StorageCatalogMigrationBackup
    {
        public int FormatVersion { get; set; } = 1;

        public List<StorageCatalogMigrationBackupItem> Records { get; set; } = [];
    }

    private sealed class StorageCatalogMigrationCommit
    {
        public int FormatVersion { get; set; } = 1;

        public StorageCatalogPathMigrationState State { get; set; } = StorageCatalogPathMigrationState.PointerCommitted;

        public int RecordCount { get; set; }

        public string BackupSha256 { get; set; } = string.Empty;

        public string TargetSha256 { get; set; } = string.Empty;

        public DateTimeOffset CommittedAtUtc { get; set; }
    }

    private sealed class StorageCatalogMigrationBackupItem
    {
        public Guid Id { get; set; }

        public string EndpointOrRoot { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public StorageHealthStatus HealthStatus { get; set; }

        public string LastHealthMessage { get; set; } = string.Empty;

        public int RootBindingFormatVersion { get; set; }

        public HostPlatformFamily RootPlatformFamily { get; set; }

        public PhysicalPathSyntax RootPathSyntax { get; set; }

        public string RootHostBindingId { get; set; } = string.Empty;

        public HostBoundPathState RootPathState { get; set; }

        public DateTimeOffset? RootLastValidatedAtUtc { get; set; }

        public static StorageCatalogMigrationBackupItem Create(StorageCatalogRecord storage)
        {
            return new StorageCatalogMigrationBackupItem
            {
                Id = storage.Id,
                EndpointOrRoot = storage.EndpointOrRoot,
                IsEnabled = storage.IsEnabled,
                HealthStatus = storage.HealthStatus,
                LastHealthMessage = storage.LastHealthMessage,
                RootBindingFormatVersion = storage.RootBindingFormatVersion,
                RootPlatformFamily = storage.RootPlatformFamily,
                RootPathSyntax = storage.RootPathSyntax,
                RootHostBindingId = storage.RootHostBindingId,
                RootPathState = storage.RootPathState,
                RootLastValidatedAtUtc = storage.RootLastValidatedAtUtc
            };
        }

        public void Restore(StorageCatalogRecord storage)
        {
            storage.EndpointOrRoot = EndpointOrRoot;
            storage.IsEnabled = IsEnabled;
            storage.HealthStatus = HealthStatus;
            storage.LastHealthMessage = LastHealthMessage;
            storage.RootBindingFormatVersion = RootBindingFormatVersion;
            storage.RootPlatformFamily = RootPlatformFamily;
            storage.RootPathSyntax = RootPathSyntax;
            storage.RootHostBindingId = RootHostBindingId;
            storage.RootPathState = RootPathState;
            storage.RootLastValidatedAtUtc = RootLastValidatedAtUtc;
        }

        public bool Matches(StorageCatalogRecord storage)
        {
            return Id == storage.Id &&
                   string.Equals(EndpointOrRoot, storage.EndpointOrRoot, StringComparison.Ordinal) &&
                   IsEnabled == storage.IsEnabled &&
                   HealthStatus == storage.HealthStatus &&
                   string.Equals(LastHealthMessage, storage.LastHealthMessage, StringComparison.Ordinal) &&
                   RootBindingFormatVersion == storage.RootBindingFormatVersion &&
                   RootPlatformFamily == storage.RootPlatformFamily &&
                   RootPathSyntax == storage.RootPathSyntax &&
                   string.Equals(RootHostBindingId, storage.RootHostBindingId, StringComparison.Ordinal) &&
                   RootPathState == storage.RootPathState &&
                   RootLastValidatedAtUtc == storage.RootLastValidatedAtUtc;
        }
    }
}
