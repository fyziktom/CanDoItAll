using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageCatalogService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkspacePathResolver workspacePathResolver,
    IClock clock) : IStorageCatalogService
{
    private const string BootstrapStorageName = "Workspace file system";
    private const string BootstrapRoutingRuleName = "Workspace editable fallback";
    private static readonly Guid BootstrapRoutingRuleId = Guid.Parse("fbb91e1a-f1fc-4261-8baf-76c2de2730b9");

    public async Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<StorageCatalogRecord>()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapFileSystemStorageAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<StorageCatalogRecord>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingStorage = await dbContext.Set<StorageCatalogRecord>()
            .OrderBy(item => item.DisplayOrder)
            .FirstOrDefaultAsync(item => item.IsSystemDefault, cancellationToken);
        var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        if (existingStorage is not null)
        {
            RefreshBootstrapStorage(existingStorage, workspaceRoot);
            await dbContext.SaveChangesAsync(cancellationToken);
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

        await EnsureBootstrapRuleAsync(dbContext, storage, cancellationToken);
        return storage;
    }

    public async Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

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
        var storage = await dbContext.Set<StorageCatalogRecord>()
            .OrderBy(item => item.DisplayOrder)
            .FirstOrDefaultAsync(
                item => item.IsSystemDefault || item.Name == BootstrapStorageName,
                cancellationToken);
        if (storage is null)
        {
            return null;
        }

        RefreshBootstrapStorage(storage, workspaceRoot);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapRuleAsync(dbContext, storage, cancellationToken);
        return storage;
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
        storage.CapabilityMask = capabilityMask;
        storage.HealthStatus = StorageHealthStatus.Healthy;
        storage.LastHealthMessage = "Bootstrap workspace storage";
        storage.UpdatedAtUtc = clock.GetUtcNow();
    }
}
