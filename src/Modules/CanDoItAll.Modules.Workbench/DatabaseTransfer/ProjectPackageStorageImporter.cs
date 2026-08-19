using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Workbench.ProjectPackageArchive;

using static CanDoItAll.Modules.Workbench.ProjectPackageStorageBindingPolicy;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectPackageStorageImporter(
    IStorageDriverRegistry storageDrivers,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IClock clock,
    ILogger<StoragePlacementService> placementLogger,
    ILogger<ProjectPackageService> logger)
{
    internal async Task<ProjectPackageStorageImportPreflight> PreflightImportAsync(
        string extractionRoot,
        ProjectPackageManifest manifest,
        ProjectTransferDataSet dataSet,
        CancellationToken cancellationToken)
    {
        var bindings = ResolvePackageBindings(dataSet);
        await ValidateStorageManifestAsync(
            extractionRoot,
            manifest,
            bindings,
            cancellationToken);
        return new ProjectPackageStorageImportPreflight(bindings);
    }

    internal List<StagedStorageWrite> CreateStagingJournal() => [];

    private async Task ValidateStorageManifestAsync(
        string extractionRoot,
        ProjectPackageManifest manifest,
        IReadOnlyList<PackageBinding> bindings,
        CancellationToken cancellationToken)
    {
        var mutableGroups = bindings
            .Where(binding => binding.Reference.ProviderKind is
                StorageProviderKind.FileSystem or StorageProviderKind.Ftp)
            .GroupBy(binding => binding.Key)
            .ToDictionary(group => group.Key);
        var immutableGroups = bindings
            .Where(binding => binding.Reference.ProviderKind == StorageProviderKind.Ipfs)
            .GroupBy(binding => binding.Key)
            .ToDictionary(group => group.Key);

        var mutableManifestByKey = new Dictionary<
            ProjectManagedStorageObjectKey,

            ProjectPackageStorageFileManifest>();
        var packagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.StorageFiles)
        {
            ValidateStorageFileManifest(file);
            var key = CreateStorageKey(
                file.SourceStorageId,
                file.ProviderKind,
                file.LocatorKind,
                file.Locator);
            if (!mutableManifestByKey.TryAdd(key, file))
            {
                throw new InvalidDataException(
                    "The project package contains duplicate mutable storage identities.");
            }

            var canonicalPackagePath = NormalizePackageRelativePath(
                file.PackagePath,

                isDirectory: false);
            if (!canonicalPackagePath.StartsWith("storage/", StringComparison.Ordinal) ||
                !packagePaths.Add(canonicalPackagePath))
            {
                throw new InvalidDataException(
                    "The project package contains a duplicate or invalid storage payload path.");
            }

            var sourcePath = ResolvePackageFilePath(
                extractionRoot,
                canonicalPackagePath,
                physicalPathPolicyFactory);
            if (!File.Exists(sourcePath))
            {
                throw new InvalidDataException(
                    $"The packaged storage file '{canonicalPackagePath}' is missing.");
            }

            await VerifyFileIntegrityAsync(
                sourcePath,
                file.Length,
                file.Sha256,
                ProjectStructureAssetUploadLimits.MaximumFileBytes,
                cancellationToken);
        }

        if (mutableManifestByKey.Count != mutableGroups.Count ||
            mutableManifestByKey.Keys.Any(key => !mutableGroups.ContainsKey(key)))
        {
            throw new InvalidDataException(
                "The project package mutable storage manifest does not match its project bindings.");
        }

        var immutableManifestKeys = new HashSet<ProjectManagedStorageObjectKey>();
        foreach (var immutableReference in manifest.ImmutableStorageReferences)
        {
            if (immutableReference.ProviderKind != StorageProviderKind.Ipfs ||
                immutableReference.LocatorKind != StorageLocatorKind.ContentAddress ||
                immutableReference.SourceStorageId == Guid.Empty ||
                immutableReference.Length < 0 ||
                immutableReference.Length > ProjectStructureAssetUploadLimits.MaximumFileBytes ||
                !IsSha256(immutableReference.Sha256) ||
                string.IsNullOrWhiteSpace(immutableReference.ContentType) ||
                string.IsNullOrWhiteSpace(immutableReference.OriginalFileName) ||
                !string.Equals(
                    Path.GetFileName(immutableReference.OriginalFileName),
                    immutableReference.OriginalFileName,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The project package contains an invalid immutable storage reference.");
            }

            var key = CreateStorageKey(
                immutableReference.SourceStorageId,
                immutableReference.ProviderKind,
                immutableReference.LocatorKind,
                immutableReference.Locator);
            if (!immutableManifestKeys.Add(key))
            {
                throw new InvalidDataException(
                    "The project package contains duplicate immutable storage identities.");
            }
        }

        if (immutableManifestKeys.Count != immutableGroups.Count ||
            immutableManifestKeys.Any(key => !immutableGroups.ContainsKey(key)))
        {
            throw new InvalidDataException(
                "The project package immutable storage manifest does not match its project bindings.");
        }
    }

    private static void ValidateStorageFileManifest(
        ProjectPackageStorageFileManifest file)
    {
        if (file.ProviderKind is not StorageProviderKind.FileSystem and
            not StorageProviderKind.Ftp ||
            file.SourceStorageId == Guid.Empty ||
            file.Length < 0 ||
            file.Length > ProjectStructureAssetUploadLimits.MaximumFileBytes ||
            string.IsNullOrWhiteSpace(file.ContentType) ||
            file.ContentType.Length > 160 ||
            string.IsNullOrWhiteSpace(file.OriginalFileName) ||
            file.OriginalFileName.Length > 260 ||
            !string.Equals(
                Path.GetFileName(file.OriginalFileName),
                file.OriginalFileName,
                StringComparison.Ordinal) ||
            !IsSha256(file.Sha256))
        {
            throw new InvalidDataException(
                "The project package contains an invalid mutable storage manifest entry.");
        }

        var key = CreateStorageKey(
            file.SourceStorageId,
            file.ProviderKind,
            file.LocatorKind,
            file.Locator);
        var normalizedRelativePath = ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(
            file.RelativePath);
        if (!ProjectManagedStorageObjectKey.LocatorEquals(
                file.ProviderKind,
                key.Locator,
                normalizedRelativePath))
        {
            throw new InvalidDataException(
                "The project package mutable storage path does not match its source locator.");
        }
    }

    internal async Task<int> RewriteStorageBindingsAsync(
        string extractionRoot,
        ProjectPackageManifest manifest,
        ProjectTransferDataSet dataSet,
        ProjectPackageStorageImportPreflight preflight,
        TargetStoragePlan storagePlan,
        ICollection<StagedStorageWrite> stagedWrites,
        CancellationToken cancellationToken)
    {
        var bindings = preflight.Bindings;
        var objectProjectIds = dataSet.Objects.ToDictionary(
            item => item.Id,
            item => item.ProjectId);
        var manifestByKey = manifest.StorageFiles.ToDictionary(
            file => CreateStorageKey(
                file.SourceStorageId,
                file.ProviderKind,
                file.LocatorKind,
                file.Locator));
        var placementCatalog = new ProjectPackageStorageCatalogSnapshot(
            storagePlan.PlacementStorages,
            storagePlan.Rules);
        var placementService = new StoragePlacementService(
            placementCatalog,
            new DefaultStorageRoutingService(placementCatalog),
            storageDrivers,
            placementLogger);
        var imported = 0;

        foreach (var group in bindings
                     .Where(binding => binding.Reference.ProviderKind is
                         StorageProviderKind.FileSystem or StorageProviderKind.Ftp)
                     .GroupBy(binding => binding.Key)
                     .OrderBy(group => ToStableStorageKey(group.Key), StringComparer.Ordinal))
        {
            var first = group.OrderBy(binding => binding.Binding.Id).First();
            var file = manifestByKey[group.Key];
            var sourcePath = ResolvePackageFilePath(
                extractionRoot,
                file.PackagePath,
                physicalPathPolicyFactory);
            var content = await ReadBoundedFileAsync(
                sourcePath,
                ProjectStructureAssetUploadLimits.MaximumFileBytes,
                cancellationToken);
            var projectId = objectProjectIds[first.Binding.ProjectObjectId];
            var requestedPath = CreateImportedManagedPath(
                manifest.PackageId,
                projectId,
                file.OriginalFileName);
            var contentKind = StorageContentClassifier.Resolve(
                file.ContentType,
                file.OriginalFileName);
            var placement = await placementService.PlaceAsync(
                new StoragePlacementRequest(
                    file.OriginalFileName,
                    file.ContentType,
                    content,
                    StorageUsagePurpose.ProjectAsset,
                    contentKind,
                    projectId,
                    RelativePathHint: requestedPath,
                    PreviewRequired: StorageContentClassifier.SupportsInlinePreview(contentKind)),
                cancellationToken);
            stagedWrites.Add(new StagedStorageWrite(
                placement.Storage,
                placement.WriteResult.Reference));
            ValidatePlacedReference(placement, requestedPath);
            await VerifyPlacedContentAsync(
                placement.Storage,
                placement.WriteResult.Reference,
                file.Length,
                file.Sha256,
                cancellationToken);

            var stampedReference = ProjectManagedStorageProvenancePolicy.Stamp(
                placement.WriteResult.Reference,
                requestedPath,
                placement.Storage,
                physicalIdentityPolicy);
            if (!ProjectManagedStorageProvenancePolicy.TryValidate(
                    stampedReference,
                    placement.RelativePath,
                    out var error))
            {
                throw new InvalidDataException(
                    $"Imported project storage binding could not be restamped: {error}");
            }

            foreach (var packageBinding in group)
            {
                RewriteBinding(
                    packageBinding.Binding,
                    placement.RelativePath,
                    placement.Route,
                    file.ContentType,
                    file.OriginalFileName,
                    stampedReference);
            }

            imported++;
        }

        await RewriteImmutableBindingsAsync(
            manifest,
            bindings,
            objectProjectIds,
            storagePlan,
            cancellationToken);
        return imported;
    }

    private async Task RewriteImmutableBindingsAsync(
        ProjectPackageManifest manifest,
        IReadOnlyList<PackageBinding> bindings,
        IReadOnlyDictionary<Guid, Guid> objectProjectIds,
        TargetStoragePlan storagePlan,
        CancellationToken cancellationToken)
    {
        var immutableBindings = bindings
            .Where(binding => binding.Reference.ProviderKind == StorageProviderKind.Ipfs)
            .GroupBy(binding => binding.Key)
            .OrderBy(group => ToStableStorageKey(group.Key), StringComparer.Ordinal)
            .ToList();
        if (immutableBindings.Count == 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var targetStorage = storagePlan.Storages
            .Where(storage =>
                storage.ProviderKind == StorageProviderKind.Ipfs &&
                storage.IsEnabled &&
                storage.CapabilityMask.HasFlag(StorageCapability.Read) &&
                storage.HealthStatus != StorageHealthStatus.Unavailable)
            .OrderByDescending(storage => storage.IsSystemDefault)
            .ThenBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Id)
            .FirstOrDefault()
            ?? throw new InvalidDataException(
                "The package contains immutable IPFS assets, but the target profile has no readable IPFS storage catalog entry. Configure the target provider before importing.");
        var targetDriver = ResolveReadableDriver(storageDrivers, StorageProviderKind.Ipfs);
        var immutableManifestByKey = manifest.ImmutableStorageReferences.ToDictionary(
            item => CreateStorageKey(
                item.SourceStorageId,
                item.ProviderKind,
                item.LocatorKind,
                item.Locator));

        foreach (var group in immutableBindings)
        {
            var first = group.OrderBy(binding => binding.Binding.Id).First();
            var immutableManifest = immutableManifestByKey[group.Key];
            var projectId = objectProjectIds[first.Binding.ProjectObjectId];
            var requestedPath = CreateImportedManagedPath(
                manifest.PackageId,
                projectId,
                immutableManifest.OriginalFileName);
            var remappedReference = first.Reference with
            {
                StorageId = targetStorage.Id,
                ContentLength = immutableManifest.Length,
                Route = string.Empty,
                MetadataJson = "{}"
            };
            if (!string.Equals(
                    remappedReference.Locator,
                    immutableManifest.Locator,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "An immutable content address changed while preparing the package import.");
            }

            await using (var targetStream = await targetDriver.OpenReadAsync(
                             targetStorage,
                             remappedReference,
                             cancellationToken))
            {
                var targetIntegrity = await ComputeStreamIntegrityAsync(
                    targetStream,
                    ProjectStructureAssetUploadLimits.MaximumFileBytes,
                    cancellationToken);
                if (targetIntegrity.Length != immutableManifest.Length ||
                    !string.Equals(
                        targetIntegrity.Sha256,
                        immutableManifest.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "The target IPFS provider resolved different bytes for the packaged immutable content address.");
                }
            }

            var stampedReference = ProjectManagedStorageProvenancePolicy.Stamp(
                remappedReference,
                requestedPath,
                targetStorage,
                physicalIdentityPolicy);
            if (!ProjectManagedStorageProvenancePolicy.TryValidate(
                    stampedReference,
                    mediaRelativePath: null,
                    out var error))
            {
                throw new InvalidDataException(
                    $"Imported immutable project storage binding could not be restamped: {error}");
            }

            foreach (var packageBinding in group)
            {
                RewriteBinding(
                    packageBinding.Binding,
                    string.Empty,
                    StorageJson.BuildPreviewUrl(stampedReference),
                    immutableManifest.ContentType,
                    immutableManifest.OriginalFileName,
                    stampedReference);
            }
        }
    }

    private static void RewriteBinding(
        ProjectNodeBindingRecord binding,
        string mediaRelativePath,
        string route,
        string contentType,
        string originalFileName,
        StorageObjectReference reference)
    {
        binding.MediaRelativePath = mediaRelativePath;
        binding.Route = route;
        binding.MediaContentType = contentType;
        binding.MediaOriginalFileName = originalFileName;
        binding.StorageObjectReferenceJson = StorageJson.SerializeReference(reference);
    }

    private static void ValidatePlacedReference(
        StoragePlacementResult placement,
        string requestedPath)
    {
        var reference = placement.WriteResult.Reference;
        if (reference.StorageId != placement.Storage.Id ||
            reference.ProviderKind != placement.Storage.ProviderKind)
        {
            throw new InvalidDataException(
                "The target storage driver returned a reference for a different storage catalog entry.");
        }

        var key = ProjectManagedStorageObjectKey.FromReference(reference);
        if (reference.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp &&
            !ProjectManagedStorageObjectKey.LocatorEquals(
                reference.ProviderKind,
                key.Locator,
                requestedPath))
        {
            throw new InvalidDataException(
                "The target mutable storage driver did not honor the unique copy-on-write generation path.");
        }
    }

    private async Task VerifyPlacedContentAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        long expectedLength,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        var driver = ResolveReadableDriver(storageDrivers, reference.ProviderKind);
        await using var stream = await driver.OpenReadAsync(
            storage,
            reference,
            cancellationToken);
        var integrity = await ComputeStreamIntegrityAsync(
            stream,
            ProjectStructureAssetUploadLimits.MaximumFileBytes,
            cancellationToken);
        if (integrity.Length != expectedLength ||
            !string.Equals(integrity.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The target storage provider did not reproduce the packaged asset bytes.");
        }
    }

    private static string CreateImportedManagedPath(
        Guid packageId,
        Guid projectId,
        string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (extension.Length > 20 || extension.Any(character =>

                !char.IsLetterOrDigit(character) && character != '.'))
        {
            extension = ".bin";
        }

        var stem = SanitizeFileStem(Path.GetFileNameWithoutExtension(originalFileName));
        return $"managed-files/project-media/imports/{packageId:N}/{projectId:N}/{stem}-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    private static string SanitizeFileStem(string value)
    {
        var stem = new string(value
            .Trim()

            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (stem.Contains("--", StringComparison.Ordinal))
        {
            stem = stem.Replace("--", "-", StringComparison.Ordinal);
        }

        stem = stem.Trim('-');
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "asset";
        }

        return stem.Length <= 80 ? stem : stem[..80];
    }

    internal async Task<TargetStoragePlan> BuildTargetStoragePlanAsync(
        AppDbContext dbContext,
        ResolvedDatabaseProfile targetProfile,
        CancellationToken cancellationToken)
    {
        var storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Id)
            .ToListAsync(cancellationToken);
        var rules = await dbContext.Set<StorageRoutingRule>()
            .AsNoTracking()
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        var catalogFingerprint = ComputeStorageCatalogFingerprint(storages, rules);

        StorageCatalogRecord? pendingStorage = null;
        StorageRoutingRule? pendingRule = null;
        if (storages.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(targetProfile.Profile.Storage.WorkspaceRoot))
            {
                throw new InvalidDataException(
                    "The inactive target profile has no storage catalog and no workspace root for a bootstrap storage.");
            }

            var now = clock.GetUtcNow();
            var workspaceRoot = Path.GetFullPath(
                targetProfile.Profile.Storage.WorkspaceRoot);
            pendingStorage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Workspace file system",
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
                LastHealthMessage = "Bootstrap workspace storage created by project package import",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            StorageCatalogHostBindingPolicy.BindCurrent(
                pendingStorage,
                workspaceRoot,
                now);
            pendingRule = new StorageRoutingRule
            {
                Id = Guid.NewGuid(),
                Name = "Workspace editable fallback",
                IsEnabled = true,
                Priority = 1000,
                ScopeKind = StorageRoutingScopeKind.Workspace,
                UsagePurpose = StorageUsagePurpose.Unknown,
                ContentKind = StorageContentKind.Unknown,
                RequiredCapabilities = StorageCapability.Write,
                PreferredStorageId = pendingStorage.Id,
                Reason = "Bootstrap filesystem fallback for imported project assets.",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            storages.Add(pendingStorage);
            rules.Add(pendingRule);
        }

        foreach (var storage in storages.Where(storage =>
                     storage.ProviderKind == StorageProviderKind.FileSystem &&
                     storage.IsEnabled &&
                     !storage.IsReadOnly))
        {
            if (string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
            {
                throw new InvalidDataException(
                    $"Target filesystem storage '{storage.Name}' does not have an explicit root for inactive-profile import.");
            }

            _ = physicalIdentityPolicy.ResolveReparseSafeFullPath(
                Path.GetFullPath(storage.EndpointOrRoot));
        }

        var placementStorages = storages
            .Where(IsUsablePlacementStorage)
            .ToList();
        if (placementStorages.Count == 0)
        {
            throw new InvalidDataException(
                "The inactive target profile has no enabled storage that can write and verify imported project assets.");
        }

        return new TargetStoragePlan(
            storages,
            placementStorages,
            rules,
            pendingStorage,
            pendingRule,
            catalogFingerprint);
    }

    private bool IsUsablePlacementStorage(StorageCatalogRecord storage)
    {
        const StorageCapability required =
            StorageCapability.Read |
            StorageCapability.Write |
            StorageCapability.Delete;
        return storage.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp &&
               storage.IsEnabled &&
               !storage.IsReadOnly &&
               storage.HealthStatus != StorageHealthStatus.Unavailable &&
               (storage.CapabilityMask & required) == required &&
               storageDrivers.TryResolve(storage.ProviderKind, out var driver) &&
               (driver.SupportedCapabilities & required) == required;
    }

    internal static async Task PersistPendingStorageCatalogAsync(
        AppDbContext dbContext,
        TargetStoragePlan storagePlan,
        CancellationToken cancellationToken)
    {
        if (storagePlan.PendingStorage is null)
        {
            return;
        }

        await dbContext.Set<StorageCatalogRecord>().AddAsync(
            storagePlan.PendingStorage,
            cancellationToken);
        if (storagePlan.PendingRule is not null)
        {
            await dbContext.Set<StorageRoutingRule>().AddAsync(
                storagePlan.PendingRule,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static async Task ValidateTargetStoragePlanStillCurrentAsync(
        AppDbContext dbContext,
        TargetStoragePlan storagePlan,
        CancellationToken cancellationToken)
    {
        var storages = await dbContext.Set<StorageCatalogRecord>()
            .AsNoTracking()
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Id)
            .ToListAsync(cancellationToken);
        var rules = await dbContext.Set<StorageRoutingRule>()
            .AsNoTracking()
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        var currentFingerprint = ComputeStorageCatalogFingerprint(storages, rules);
        if (!string.Equals(
                currentFingerprint,
                storagePlan.CatalogFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The inactive target storage catalog changed while project assets were being staged. Import was stopped before project data changed.");
        }
    }

    private static string ComputeStorageCatalogFingerprint(
        IReadOnlyList<StorageCatalogRecord> storages,
        IReadOnlyList<StorageRoutingRule> rules)
    {
        var snapshot = new
        {
            Storages = storages
                .OrderBy(storage => storage.Id)
                .Select(storage => new
                {
                    storage.Id,
                    storage.Name,
                    storage.ProviderKind,
                    storage.IsEnabled,
                    storage.IsSystemDefault,
                    storage.IsReadOnly,
                    storage.DisplayOrder,
                    storage.EndpointOrRoot,
                    storage.ConfigJson,
                    storage.CapabilityMask,
                    storage.HealthStatus,
                    storage.CredentialSecretId
                }),
            Rules = rules
                .OrderBy(rule => rule.Id)
                .Select(rule => new
                {
                    rule.Id,
                    rule.IsEnabled,
                    rule.Priority,
                    rule.ScopeKind,
                    rule.ProjectId,
                    rule.NodeKey,
                    rule.UsagePurpose,
                    rule.ContentKind,
                    rule.MimePattern,
                    rule.MinimumContentLength,
                    rule.MaximumContentLength,
                    rule.EditIntent,
                    rule.PreviewRequired,
                    rule.PublishIntent,
                    rule.RequiredCapabilities,
                    rule.PreferredStorageId,
                    rule.AlternativeStorageIdsJson
                })
        };
        var json = JsonSerializer.Serialize(snapshot);
        return Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json)));
    }

    internal async Task CleanupStagedWritesAsync(
        IReadOnlyCollection<StagedStorageWrite> stagedWrites)
    {
        const int maximumAttempts = 3;
        var failures = new List<ProjectPackageCompensationFailure>();
        foreach (var stagedWrite in stagedWrites.Reverse())
        {
            if (!storageDrivers.TryResolve(
                    stagedWrite.Reference.ProviderKind,
                    out var driver) ||
                !driver.SupportedCapabilities.HasFlag(StorageCapability.Delete))
            {
                logger.LogWarning(
                    "Could not compensate a staged project package object because its delete-capable driver disappeared. StorageId={StorageId}. Provider={ProviderKind}. LocatorKind={LocatorKind}. LocatorFingerprint={LocatorFingerprint}.",
                    stagedWrite.Storage.Id,
                    stagedWrite.Reference.ProviderKind,
                    stagedWrite.Reference.LocatorKind,
                    ProjectPackageCompensationFailure.CreateLocatorFingerprint(
                        stagedWrite.Reference));
                failures.Add(ProjectPackageCompensationFailure.From(stagedWrite));
                continue;
            }

            Exception? lastFailure = null;
            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                using var attemptTimeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(10));
                try
                {
                    await driver.DeleteAsync(
                            stagedWrite.Storage,
                            stagedWrite.Reference,
                            attemptTimeout.Token)
                        .WaitAsync(attemptTimeout.Token);
                    lastFailure = null;
                    break;
                }
                catch (Exception exception)
                {
                    lastFailure = exception;
                }
            }

            if (lastFailure is not null)
            {
                logger.LogWarning(
                    "Could not clean up staged project package object. StorageId={StorageId}. Provider={ProviderKind}. LocatorKind={LocatorKind}. LocatorFingerprint={LocatorFingerprint}. FailureType={FailureType}.",
                    stagedWrite.Storage.Id,
                    stagedWrite.Reference.ProviderKind,
                    stagedWrite.Reference.LocatorKind,
                    ProjectPackageCompensationFailure.CreateLocatorFingerprint(
                        stagedWrite.Reference),
                    lastFailure.GetType().Name);
                failures.Add(ProjectPackageCompensationFailure.From(stagedWrite));
            }
        }

        if (failures.Count > 0)
        {
            throw new ProjectPackageCompensationException(failures);
        }
    }

}

internal sealed record ProjectPackageCompensationFailure(
    Guid StorageId,
    StorageProviderKind ProviderKind,
    StorageLocatorKind LocatorKind,
    string LocatorFingerprint)
{
    internal static ProjectPackageCompensationFailure From(
        StagedStorageWrite stagedWrite)
        => new(
            stagedWrite.Storage.Id,
            stagedWrite.Reference.ProviderKind,
            stagedWrite.Reference.LocatorKind,
            CreateLocatorFingerprint(stagedWrite.Reference));

    internal static string CreateLocatorFingerprint(StorageObjectReference reference)
    {
        var identity = $"{reference.ProviderKind:D}\0{reference.LocatorKind:D}\0{reference.Locator}";
        return Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(identity)));
    }
}

internal sealed class ProjectPackageCompensationException : IOException
{
    internal ProjectPackageCompensationException(
        IReadOnlyCollection<ProjectPackageCompensationFailure> failures)
        : base(BuildMessage(failures))
    {
        Failures = failures;
    }

    internal IReadOnlyCollection<ProjectPackageCompensationFailure> Failures { get; }

    private static string BuildMessage(
        IReadOnlyCollection<ProjectPackageCompensationFailure> failures)
    {
        var identities = string.Join(
            ", ",
            failures
                .Distinct()
                .OrderBy(failure => failure.ProviderKind)
                .ThenBy(failure => failure.StorageId)
                .ThenBy(failure => failure.LocatorKind)
                .ThenBy(failure => failure.LocatorFingerprint, StringComparer.Ordinal)
                .Select(failure =>
                    $"Provider={failure.ProviderKind}, StorageId={failure.StorageId:D}, " +
                    $"LocatorKind={failure.LocatorKind}, LocatorFingerprint={failure.LocatorFingerprint}"));
        return "Project package import did not change target project data, but staged storage cleanup is incomplete. " +
               $"Remove or reconcile the unbound objects for: {identities}.";
    }
}
