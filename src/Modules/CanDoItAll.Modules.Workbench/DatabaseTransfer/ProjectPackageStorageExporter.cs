using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Modules.Workbench.ProjectPackageArchive;

using static CanDoItAll.Modules.Workbench.ProjectPackageStorageBindingPolicy;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectPackageStorageExporter(
    IStorageDriverRegistry storageDrivers,
    ProjectManagedStoragePhysicalIdentityPolicy physicalIdentityPolicy)
{
    internal async Task CopyReferencedStorageAsync(
        ResolvedDatabaseProfile sourceProfile,
        string workingRoot,
        ProjectTransferDataSet dataSet,
        IReadOnlyList<StorageCatalogRecord> storages,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        var bindings = ResolvePackageBindings(dataSet);
        if (bindings.Count == 0)
        {
            return;
        }

        var storagesById = storages.ToDictionary(storage => storage.Id);
        var bootstrapStorage = string.IsNullOrWhiteSpace(sourceProfile.Profile.Storage.WorkspaceRoot)
            ? null
            : StorageBootstrapCatalogPolicy.ResolveAuthoritativeFileSystemStorage(
                storages,
                sourceProfile.Profile.Storage.WorkspaceRoot);
        var mutableIndex = 0;

        foreach (var group in bindings
                     .GroupBy(binding => binding.Key)
                     .OrderBy(group => ToStableStorageKey(group.Key), StringComparer.Ordinal))
        {
            var first = group.OrderBy(binding => binding.Binding.Id).First();
            var reference = first.Reference;
            var storage = ResolveSourceStorage(
                reference,
                storagesById,
                bootstrapStorage,
                first.Binding.Id);
            ValidateReadableStorage(storage, reference, first.Binding.Id);
            if (ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference) &&
                !ProjectManagedStorageProvenancePolicy.TryValidateCurrentStorage(
                    reference,
                    storage,
                    physicalIdentityPolicy,
                    out var provenanceError))
            {
                throw new InvalidDataException(
                    $"Project media binding '{first.Binding.Id:D}' cannot be exported because {provenanceError}");
            }

            var driver = ResolveReadableDriver(storageDrivers, reference.ProviderKind);
            await using var sourceStream = await driver.OpenReadAsync(
                storage,
                reference,
                cancellationToken);
            if (reference.ProviderKind == StorageProviderKind.Ipfs)
            {
                var immutableIntegrity = await ComputeStreamIntegrityAsync(
                    sourceStream,
                    ProjectStructureAssetUploadLimits.MaximumFileBytes,
                    cancellationToken);
                if (reference.ContentLength.HasValue &&
                    reference.ContentLength.Value != immutableIntegrity.Length)
                {
                    throw new InvalidDataException(
                        $"Project media binding '{first.Binding.Id:D}' content length does not match its immutable storage reference.");
                }

                manifest.ImmutableStorageReferences.Add(
                    new ProjectPackageImmutableStorageReferenceManifest
                    {
                        SourceStorageId = reference.StorageId,
                        ProviderKind = reference.ProviderKind,
                        LocatorKind = reference.LocatorKind,
                        Locator = first.Key.Locator,
                        ContentType = ResolveContentType(first.Binding, reference),
                        OriginalFileName = ResolveOriginalFileName(first.Binding, reference),
                        Length = immutableIntegrity.Length,
                        Sha256 = immutableIntegrity.Sha256
                    });
                continue;
            }

            var packageRelativePath = $"storage/{mutableIndex++:D8}.payload";
            var packagePath = ResolvePackageFilePath(workingRoot, packageRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

            var integrity = await CopyToNewFileWithIntegrityAsync(
                sourceStream,
                packagePath,
                ProjectStructureAssetUploadLimits.MaximumFileBytes,
                cancellationToken);
            if (reference.ContentLength.HasValue &&
                reference.ContentLength.Value != integrity.Length)
            {
                throw new InvalidDataException(
                    $"Project media binding '{first.Binding.Id:D}' content length does not match its storage reference.");
            }

            manifest.StorageFiles.Add(new ProjectPackageStorageFileManifest
            {
                SourceStorageId = reference.StorageId,
                ProviderKind = reference.ProviderKind,
                LocatorKind = reference.LocatorKind,
                Locator = first.Key.Locator,
                RelativePath = ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(
                    first.Binding.MediaRelativePath),
                PackagePath = packageRelativePath,
                ContentType = ResolveContentType(first.Binding, reference),
                OriginalFileName = ResolveOriginalFileName(first.Binding, reference),
                Length = integrity.Length,
                Sha256 = integrity.Sha256
            });
        }
    }

    private static StorageCatalogRecord ResolveSourceStorage(
        StorageObjectReference reference,
        IReadOnlyDictionary<Guid, StorageCatalogRecord> storagesById,
        StorageCatalogRecord? bootstrapStorage,
        Guid bindingId)
    {
        StorageCatalogRecord? storage = null;
        if (reference.StorageId.HasValue)
        {
            storagesById.TryGetValue(reference.StorageId.Value, out storage);
        }
        else if (reference.ProviderKind == StorageProviderKind.FileSystem)
        {
            storage = bootstrapStorage;
        }

        if (storage is null)
        {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' points to a mutable storage catalog entry that does not exist.");
        }

        if (storage.ProviderKind != reference.ProviderKind)
        {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' storage provider does not match its catalog entry.");
        }

        return storage;
    }

    private static void ValidateReadableStorage(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        Guid bindingId)
    {
        if (!storage.IsEnabled ||
            storage.HealthStatus == StorageHealthStatus.Unavailable ||
            !storage.CapabilityMask.HasFlag(StorageCapability.Read))
        {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' storage is not available for package reads.");
        }

        if (reference.ProviderKind == StorageProviderKind.FileSystem &&
            string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
        {
            throw new InvalidDataException(
                $"Project media binding '{bindingId:D}' filesystem storage has no authoritative root.");
        }
    }
}

