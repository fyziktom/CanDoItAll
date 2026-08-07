using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace;

public sealed partial class WorkspaceService
{
    public StorageCatalogEditorModel CreateStorageDraft(StorageProviderKind providerKind)
    {
        return NewStorage(providerKind);
    }

    public async Task<IReadOnlyList<StorageCatalogSummary>> ListStorageCatalogAsync(CancellationToken cancellationToken = default)
    {
        var storages = await storageCatalogService.ListAsync(cancellationToken);
        return storages
            .Select(storage => new StorageCatalogSummary(
                storage.Id,
                storage.Name,
                storage.ProviderKind,
                storage.ConnectionMode,
                storage.EndpointOrRoot,
                storage.DisplayOrder,
                storage.IsEnabled,
                storage.IsSystemDefault,
                storage.IsReadOnly,
                storage.CapabilityMask,
                storage.HealthStatus,
                storage.LastTestedAtUtc,
                storage.LastHealthMessage))
            .ToList();
    }

    public async Task<StorageCatalogEditorModel> GetStorageAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return NewStorage(StorageProviderKind.FileSystem);
        }

        var storage = await storageCatalogService.GetAsync(id.Value, cancellationToken);
        if (storage is null)
        {
            return NewStorage(StorageProviderKind.FileSystem);
        }

        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        var rules = await storageCatalogService.ListRulesAsync(cancellationToken);
        return new StorageCatalogEditorModel
        {
            Id = storage.Id,
            Name = storage.Name,
            ProviderKind = storage.ProviderKind,
            ConnectionMode = storage.ConnectionMode,
            EndpointOrRoot = storage.EndpointOrRoot,
            CredentialSecretId = storage.CredentialSecretId,
            IsEnabled = storage.IsEnabled,
            IsSystemDefault = storage.IsSystemDefault,
            IsReadOnly = storage.IsReadOnly,
            DisplayOrder = storage.DisplayOrder,
            CapabilityMask = storage.CapabilityMask,
            HealthStatus = storage.HealthStatus,
            LastTestedAtUtc = storage.LastTestedAtUtc,
            LastHealthMessage = storage.LastHealthMessage,
            GatewayBaseUrl = configuration.GatewayBaseUrl,
            Port = configuration.Port,
            PinOnUpload = configuration.PinOnUpload,
            Username = configuration.Username,
            BasePath = configuration.BasePath,
            UseSsl = configuration.UseSsl,
            UsePassiveMode = configuration.UsePassiveMode,
            DefaultPurposes = rules
                .Where(rule =>
                    rule.IsEnabled &&
                    rule.ScopeKind == StorageRoutingScopeKind.Workspace &&
                    rule.PreferredStorageId == storage.Id &&
                    WorkspaceStorageDefaults.TrackedPurposes.Contains(rule.UsagePurpose))
                .Select(rule => rule.UsagePurpose)
                .Distinct()
                .OrderBy(WorkspaceStorageDefaults.ResolveTrackedPurposeOrder)
                .ToList()
        };
    }

    public async Task<Result<Guid>> SaveStorageAsync(StorageCatalogEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Storage name is required."));
        }

        if (model.ProviderKind == StorageProviderKind.FileSystem && string.IsNullOrWhiteSpace(model.EndpointOrRoot))
        {
            return Result<Guid>.Failure(Error.Validation("File system storage requires a root path."));
        }

        if (model.ProviderKind != StorageProviderKind.FileSystem && string.IsNullOrWhiteSpace(model.EndpointOrRoot))
        {
            return Result<Guid>.Failure(Error.Validation("Remote storage requires an endpoint or host."));
        }

        if (model.IsSystemDefault && model.Id.HasValue)
        {
            var existing = await storageCatalogService.GetAsync(model.Id.Value, cancellationToken);
            if (existing is { IsSystemDefault: true })
            {
                return Result<Guid>.Failure(Error.Validation("The bootstrap workspace storage is system-managed."));
            }
        }

        var capabilityMask = ResolveCapabilityMask(model.ProviderKind, model.IsReadOnly);
        var record = new StorageCatalogRecord
        {
            Id = model.Id ?? Guid.NewGuid(),
            Name = model.Name.Trim(),
            ProviderKind = model.ProviderKind,
            ConnectionMode = ResolveConnectionMode(model.ProviderKind, model.ConnectionMode),
            EndpointOrRoot = model.EndpointOrRoot.Trim(),
            CredentialSecretId = model.CredentialSecretId,
            IsEnabled = model.IsEnabled,
            IsSystemDefault = model.IsSystemDefault,
            IsReadOnly = model.IsReadOnly,
            DisplayOrder = model.DisplayOrder,
            CapabilityMask = capabilityMask,
            HealthStatus = model.HealthStatus,
            LastTestedAtUtc = model.LastTestedAtUtc,
            LastHealthMessage = model.LastHealthMessage,
            ConfigJson = StorageJson.SerializeProviderConfiguration(new StorageProviderConfiguration
            {
                GatewayBaseUrl = model.GatewayBaseUrl.Trim(),
                Port = model.Port,
                PinOnUpload = model.PinOnUpload,
                Username = model.Username.Trim(),
                BasePath = model.BasePath.Trim(),
                UseSsl = model.UseSsl,
                UsePassiveMode = model.UsePassiveMode
            })
        };

        var saved = await storageCatalogService.SaveAsync(record, cancellationToken);
        await ApplyDefaultPurposesAsync(saved.Id, model.DefaultPurposes, cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "storage",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} storage catalog entry",
            $"{saved.Name} ({saved.ProviderKind})",
            ArtifactKind: "storage-catalog",
            ArtifactId: saved.Id,
            Route: "/settings?tab=storage"), cancellationToken);
        return Result<Guid>.Success(saved.Id);
    }

    public async Task DeleteStorageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await storageCatalogService.GetAsync(id, cancellationToken);
        if (existing is null || existing.IsSystemDefault)
        {
            return;
        }

        await storageCatalogService.DeleteAsync(id, cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "storage",
            "delete",
            "Deleted storage catalog entry",
            existing.Name,
            ArtifactKind: "storage-catalog",
            ArtifactId: existing.Id,
            Route: "/settings?tab=storage"), cancellationToken);
    }

    public async Task<StorageCatalogTestResult> TestStorageAsync(StorageCatalogEditorModel model, CancellationToken cancellationToken = default)
    {
        var record = new StorageCatalogRecord
        {
            Id = model.Id ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(model.Name) ? $"Storage {model.ProviderKind}" : model.Name.Trim(),
            ProviderKind = model.ProviderKind,
            ConnectionMode = ResolveConnectionMode(model.ProviderKind, model.ConnectionMode),
            EndpointOrRoot = model.EndpointOrRoot.Trim(),
            CredentialSecretId = model.CredentialSecretId,
            IsEnabled = model.IsEnabled,
            IsSystemDefault = model.IsSystemDefault,
            IsReadOnly = model.IsReadOnly,
            DisplayOrder = model.DisplayOrder,
            CapabilityMask = ResolveCapabilityMask(model.ProviderKind, model.IsReadOnly),
            ConfigJson = StorageJson.SerializeProviderConfiguration(new StorageProviderConfiguration
            {
                GatewayBaseUrl = model.GatewayBaseUrl.Trim(),
                Port = model.Port,
                PinOnUpload = model.PinOnUpload,
                Username = model.Username.Trim(),
                BasePath = model.BasePath.Trim(),
                UseSsl = model.UseSsl,
                UsePassiveMode = model.UsePassiveMode
            })
        };

        if (!storageDriverRegistry.TryResolve(record.ProviderKind, out var driver))
        {
            return new StorageCatalogTestResult(
                false,
                $"No storage driver is registered for {record.ProviderKind}.",
                StorageHealthStatus.Unavailable,
                StorageCapability.None,
                clock.GetUtcNow());
        }

        var secretValue = await ResolveStorageCredentialAsync(record, cancellationToken);
        var result = await driver.TestConnectionAsync(record, secretValue, cancellationToken);
        model.CapabilityMask = result.CapabilityMask;
        model.HealthStatus = result.HealthStatus;
        model.LastTestedAtUtc = result.TestedAtUtc;
        model.LastHealthMessage = result.Message;

        if (model.Id.HasValue)
        {
            await storageCatalogService.SaveAsync(new StorageCatalogRecord
            {
                Id = model.Id.Value,
                Name = record.Name,
                ProviderKind = record.ProviderKind,
                ConnectionMode = record.ConnectionMode,
                EndpointOrRoot = record.EndpointOrRoot,
                CredentialSecretId = record.CredentialSecretId,
                IsEnabled = record.IsEnabled,
                IsSystemDefault = record.IsSystemDefault,
                IsReadOnly = record.IsReadOnly,
                DisplayOrder = record.DisplayOrder,
                CapabilityMask = result.CapabilityMask,
                HealthStatus = result.HealthStatus,
                LastTestedAtUtc = result.TestedAtUtc,
                LastHealthMessage = result.Message,
                ConfigJson = record.ConfigJson
            }, cancellationToken);
        }

        await activityStream.RecordAsync(new ActivityWriteRequest(
            "storage",
            "health-check",
            $"Checked storage connection for {record.Name}",
            result.Message,
            ArtifactKind: "storage-catalog",
            ArtifactId: model.Id,
            Route: "/settings?tab=storage"), cancellationToken);

        return new StorageCatalogTestResult(
            result.IsSuccess,
            result.Message,
            result.HealthStatus,
            result.CapabilityMask,
            result.TestedAtUtc);
    }

    private async Task<string?> ResolveStorageCredentialAsync(
        StorageCatalogRecord record,
        CancellationToken cancellationToken)
    {
        if (record.CredentialSecretId is not { } secretId)
        {
            return null;
        }

        return await secretRuntimeResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                secretId,
                SecretRuntimePurposes.StorageCredential,
                [secretId],
                ConsumerType: SecretRuntimeConsumerTypes.StorageCredential,
                ConsumerId: SecretRuntimeConsumerIds.StorageCatalog(record.Id)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<StorageRoutingPreferenceSummary>> ListStorageRoutingDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var storages = await storageCatalogService.ListAsync(cancellationToken);
        var rules = await storageCatalogService.ListRulesAsync(cancellationToken);
        return WorkspaceStorageDefaults.TrackedPurposes
            .Select(purpose =>
            {
                var rule = rules.FirstOrDefault(candidate =>
                    candidate.ScopeKind == StorageRoutingScopeKind.Workspace &&
                    candidate.UsagePurpose == purpose &&
                    candidate.IsEnabled);
                var storageName = rule is null
                    ? string.Empty
                    : storages.FirstOrDefault(storage => storage.Id == rule.PreferredStorageId)?.Name ?? string.Empty;
                return new StorageRoutingPreferenceSummary(
                    purpose,
                    rule?.PreferredStorageId,
                    storageName,
                    rule?.IsEnabled == true,
                    rule?.Reason ?? string.Empty);
            })
            .ToList();
    }

    private async Task ApplyDefaultPurposesAsync(
        Guid storageId,
        IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
        CancellationToken cancellationToken)
    {
        var trackedPurposes = WorkspaceStorageDefaults.TrackedPurposes;
        var rules = await storageCatalogService.ListRulesAsync(cancellationToken);

        foreach (var purpose in trackedPurposes)
        {
            var existing = rules.FirstOrDefault(rule =>
                rule.ScopeKind == StorageRoutingScopeKind.Workspace &&
                rule.UsagePurpose == purpose);
            var selected = defaultPurposes.Contains(purpose);

            if (!selected && existing?.PreferredStorageId != storageId)
            {
                continue;
            }

            if (!selected)
            {
                if (existing is null)
                {
                    continue;
                }

                existing.IsEnabled = false;
                await storageCatalogService.SaveRuleAsync(existing, cancellationToken);
                continue;
            }

            var previewRequired = RequiresPreviewCapability(purpose);
            var rule = existing ?? new StorageRoutingRule();
            rule.Name = $"{WorkspaceStorageDefaults.DescribePurpose(purpose)} default";
            rule.IsEnabled = true;
            rule.Priority = ResolvePriority(purpose);
            rule.ScopeKind = StorageRoutingScopeKind.Workspace;
            rule.ProjectId = null;
            rule.NodeKey = string.Empty;
            rule.UsagePurpose = purpose;
            rule.ContentKind = StorageContentKind.Unknown;
            rule.MimePattern = string.Empty;
            rule.EditIntent = purpose is StorageUsagePurpose.ProjectAsset or StorageUsagePurpose.PromptExport;
            rule.PreviewRequired = previewRequired;
            rule.PublishIntent = purpose is StorageUsagePurpose.ReleasePackage or StorageUsagePurpose.DeploymentMirror;
            rule.RequiredCapabilities = StorageCapability.Write |
                (previewRequired ? StorageCapability.InlinePreview : StorageCapability.None);
            rule.PreferredStorageId = storageId;
            rule.AlternativeStorageIdsJson = "[]";
            rule.Reason = BuildRoutingReason(purpose);
            await storageCatalogService.SaveRuleAsync(rule, cancellationToken);
        }
    }

    private static StorageCatalogEditorModel NewStorage(StorageProviderKind providerKind)
    {
        return new StorageCatalogEditorModel
        {
            ProviderKind = providerKind,
            ConnectionMode = providerKind == StorageProviderKind.FileSystem ? StorageConnectionMode.Local : StorageConnectionMode.Remote,
            IsEnabled = true,
            UseSsl = providerKind == StorageProviderKind.Ftp,
            UsePassiveMode = true,
            PinOnUpload = providerKind == StorageProviderKind.Ipfs,
            CapabilityMask = ResolveCapabilityMask(providerKind, isReadOnly: false)
        };
    }

    private static StorageConnectionMode ResolveConnectionMode(StorageProviderKind providerKind, StorageConnectionMode requestedMode)
    {
        return providerKind == StorageProviderKind.FileSystem
            ? StorageConnectionMode.Local
            : requestedMode == StorageConnectionMode.Local
                ? StorageConnectionMode.Remote
                : requestedMode;
    }

    private static StorageCapability ResolveCapabilityMask(StorageProviderKind providerKind, bool isReadOnly)
    {
        var capabilityMask = providerKind switch
        {
            StorageProviderKind.FileSystem => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.Download |
                StorageCapability.InlinePreview |
                StorageCapability.OpenLocally |
                StorageCapability.MutableUpdate |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            StorageProviderKind.Ipfs => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Download |
                StorageCapability.InlinePreview |
                StorageCapability.DirectUrl |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            StorageProviderKind.Ftp => StorageCapability.Read |
                StorageCapability.Write |
                StorageCapability.Delete |
                StorageCapability.Download |
                StorageCapability.BatchFolderUpload |
                StorageCapability.BatchTransfer |
                StorageCapability.ConnectionTest,
            _ => StorageCapability.None
        };

        if (!isReadOnly)
        {
            return capabilityMask;
        }

        return capabilityMask &
            ~StorageCapability.Write &
            ~StorageCapability.Delete &
            ~StorageCapability.MutableUpdate &
            ~StorageCapability.BatchFolderUpload;
    }

    private static bool RequiresPreviewCapability(StorageUsagePurpose purpose)
    {
        return purpose is StorageUsagePurpose.ProjectAsset or
            StorageUsagePurpose.PromptAttachment or
            StorageUsagePurpose.Evidence or
            StorageUsagePurpose.RecordingMedia;
    }

    private static int ResolvePriority(StorageUsagePurpose purpose)
    {
        return purpose switch
        {
            StorageUsagePurpose.ProjectAsset => 100,
            StorageUsagePurpose.PromptAttachment => 110,
            StorageUsagePurpose.PromptExport => 120,
            StorageUsagePurpose.Evidence => 130,
            StorageUsagePurpose.RecordingMedia => 140,
            StorageUsagePurpose.SnapshotPackage => 150,
            StorageUsagePurpose.ReleasePackage => 160,
            StorageUsagePurpose.DeploymentMirror => 170,
            _ => 500
        };
    }

    private static string BuildRoutingReason(StorageUsagePurpose purpose)
    {
        return purpose switch
        {
            StorageUsagePurpose.ProjectAsset => "Workspace default for editable project assets.",
            StorageUsagePurpose.PromptAttachment => "Workspace default for prompt attachments.",
            StorageUsagePurpose.PromptExport => "Workspace default for generated prompt exports.",
            StorageUsagePurpose.Evidence => "Workspace default for shareable evidence artifacts.",
            StorageUsagePurpose.RecordingMedia => "Workspace default for recordings and captured media.",
            StorageUsagePurpose.SnapshotPackage => "Workspace default for snapshot packages.",
            StorageUsagePurpose.ReleasePackage => "Workspace default for publish-ready release packages.",
            StorageUsagePurpose.DeploymentMirror => "Workspace default for deployment mirror targets.",
            _ => "Workspace default storage route."
        };
    }
}
