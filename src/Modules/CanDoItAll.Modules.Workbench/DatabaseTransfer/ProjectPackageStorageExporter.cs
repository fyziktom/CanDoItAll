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

internal sealed class ProjectPackageStorageExporter(StorageProfileTransferService storageTransfers,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory) {
    internal async Task CopyReferencedStorageAsync(
        ResolvedDatabaseProfile sourceProfile,
        string workingRoot,
        ProjectTransferDataSet dataSet,
        StorageProfileTransferSession storageSession,
        ProjectPackageManifest manifest,
        CancellationToken cancellationToken) {
        var bindings = ResolvePackageBindings(dataSet);
        if (bindings.Count == 0) {
            return;
        }

        var mutableIndex = 0;

        foreach (var group in bindings
                     .GroupBy(binding => binding.Key)
                     .OrderBy(group => ToStableStorageKey(group.Key), StringComparer.Ordinal)) {
            var first = group.OrderBy(binding => binding.Binding.Id).First();
            var reference = first.Reference;
            await using var sourceStream = await storageTransfers.OpenSourceReadAsync(
                storageSession, reference, first.Binding.Id, cancellationToken);
            if (reference.ProviderKind == StorageProviderKind.Ipfs) {
                var immutableIntegrity = await ComputeStreamIntegrityAsync(
                    sourceStream,
                    ProjectStructureAssetUploadLimits.MaximumFileBytes,
                    cancellationToken);
                if (reference.ContentLength.HasValue &&
                    reference.ContentLength.Value != immutableIntegrity.Length) {
                    throw new InvalidDataException(
                        $"Project media binding '{first.Binding.Id:D}' content length does not match its immutable storage reference.");
                }

                manifest.ImmutableStorageReferences.Add(
                    new ProjectPackageImmutableStorageReferenceManifest {
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
            var packagePath = ResolvePackageFilePath(
                workingRoot,
                packageRelativePath,
                physicalPathPolicyFactory);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

            var integrity = await CopyToNewFileWithIntegrityAsync(
                sourceStream,
                packagePath,
                ProjectStructureAssetUploadLimits.MaximumFileBytes,
                cancellationToken);
            if (reference.ContentLength.HasValue &&
                reference.ContentLength.Value != integrity.Length) {
                throw new InvalidDataException(
                    $"Project media binding '{first.Binding.Id:D}' content length does not match its storage reference.");
            }

            manifest.StorageFiles.Add(new ProjectPackageStorageFileManifest {
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

}
