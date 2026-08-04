using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectPackageStorageBindingPolicy
{
internal static IReadOnlyList<PackageBinding> ResolvePackageBindings(
    ProjectTransferDataSet dataSet)
{
    var bindings = new List<PackageBinding>();
    foreach (var binding in dataSet.NodeBindings
                 .OrderBy(item => item.Id))
    {
        var hasMediaPath = !string.IsNullOrWhiteSpace(binding.MediaRelativePath);
        var hasStorageReference = !string.IsNullOrWhiteSpace(
            binding.StorageObjectReferenceJson);
        if (!hasMediaPath && !hasStorageReference)
        {
            continue;
        }

        if (!hasStorageReference ||
            !StorageJson.TryParseReference(
                binding.StorageObjectReferenceJson,
                out var reference) ||
            reference is null)
        {
            throw new InvalidDataException(
                $"Project media binding '{binding.Id:D}' does not have a valid storage object reference.");
        }

        if (reference.StorageId == Guid.Empty ||
            !Enum.IsDefined(reference.ProviderKind) ||
            !Enum.IsDefined(reference.LocatorKind) ||
            reference.ContentLength < 0)
        {
            throw new InvalidDataException(
                $"Project media binding '{binding.Id:D}' has invalid storage reference values.");
        }

        ProjectManagedStorageObjectKey key;
        try
        {
            key = ProjectManagedStorageObjectKey.FromReference(reference);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException(
                $"Project media binding '{binding.Id:D}' has an invalid storage locator: {exception.Message}",
                exception);
        }

        switch (reference.ProviderKind)
        {
            case StorageProviderKind.FileSystem:
            case StorageProviderKind.Ftp:
                ValidateMutableBinding(binding, reference, key);
                break;

            case StorageProviderKind.Ipfs:
                if (ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference) &&
                    !ProjectManagedStorageProvenancePolicy.TryValidate(
                        reference,
                        binding.MediaRelativePath,
                        out var immutableError))
                {
                    throw new InvalidDataException(
                        $"Project media binding '{binding.Id:D}' has invalid immutable storage provenance: {immutableError}");
                }

                break;

            default:
                throw new InvalidDataException(
                    $"Project media binding '{binding.Id:D}' uses unsupported storage provider '{reference.ProviderKind}'.");
        }

        bindings.Add(new PackageBinding(binding, reference, key));
    }

    return bindings;
}

internal static void ValidateMutableBinding(
    ProjectNodeBindingRecord binding,
    StorageObjectReference reference,
    ProjectManagedStorageObjectKey key)
{
    if (string.IsNullOrWhiteSpace(binding.MediaRelativePath) ||
        !ProjectManagedStorageProvenancePolicy.IsManagedProjectMediaPath(
            binding.MediaRelativePath) ||
        !ProjectManagedStorageProvenancePolicy.IsManagedProjectMediaPath(key.Locator) ||
        !ProjectManagedStorageObjectKey.LocatorEquals(
            reference.ProviderKind,
            ProjectManagedStorageProvenancePolicy.NormalizeManagedPath(
                binding.MediaRelativePath),
            key.Locator))
    {
        throw new InvalidDataException(
            $"Project media binding '{binding.Id:D}' does not identify one canonical managed project-media path.");
    }

    if (ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference) &&
        !ProjectManagedStorageProvenancePolicy.TryValidate(
            reference,
            binding.MediaRelativePath,
            out var error))
    {
        throw new InvalidDataException(
            $"Project media binding '{binding.Id:D}' has invalid mutable storage provenance: {error}");
    }
}

internal static IStorageDriver ResolveReadableDriver(
        IStorageDriverRegistry storageDrivers,
        StorageProviderKind providerKind)
{
    if (!storageDrivers.TryResolve(providerKind, out var driver) ||
        driver.ProviderKind != providerKind ||
        !driver.SupportedCapabilities.HasFlag(StorageCapability.Read))
    {
        throw new InvalidDataException(
            $"No readable storage driver is registered for provider '{providerKind}'.");
    }

    return driver;
}

internal static string ResolveContentType(
    ProjectNodeBindingRecord binding,
    StorageObjectReference reference)
    => string.IsNullOrWhiteSpace(binding.MediaContentType)
        ? reference.ContentType
        : binding.MediaContentType.Trim();

internal static string ResolveOriginalFileName(
    ProjectNodeBindingRecord binding,
    StorageObjectReference reference)
{
    var value = string.IsNullOrWhiteSpace(binding.MediaOriginalFileName)
        ? reference.DisplayName
        : binding.MediaOriginalFileName;
    var fileName = Path.GetFileName(value?.Trim());
    return string.IsNullOrWhiteSpace(fileName) ? "asset.bin" : fileName;
}

internal static string ToStableStorageKey(ProjectManagedStorageObjectKey key)
    => $"{(int)key.ProviderKind:D4}|{key.StorageId:D}|{(int)key.LocatorKind:D4}|{key.Locator}";

internal static ProjectManagedStorageObjectKey CreateStorageKey(
    Guid? storageId,
    StorageProviderKind providerKind,
    StorageLocatorKind locatorKind,
    string locator)
{
    if (storageId == Guid.Empty ||
        !Enum.IsDefined(providerKind) ||
        !Enum.IsDefined(locatorKind))
    {
        throw new InvalidDataException(
            "The project package contains invalid storage identity values.");
    }

    return ProjectManagedStorageObjectKey.FromReference(
        new StorageObjectReference(
            storageId,
            providerKind,
            locatorKind,
            locator));
}
}

internal sealed record PackageBinding(
    ProjectNodeBindingRecord Binding,
    StorageObjectReference Reference,
    ProjectManagedStorageObjectKey Key);

internal sealed record StagedStorageWrite(
    StorageCatalogRecord Storage,
    StorageObjectReference Reference);

internal sealed record TargetStoragePlan(
    IReadOnlyList<StorageCatalogRecord> Storages,
    IReadOnlyList<StorageCatalogRecord> PlacementStorages,
    IReadOnlyList<StorageRoutingRule> Rules,
    StorageCatalogRecord? PendingStorage,
    StorageRoutingRule? PendingRule,
    string CatalogFingerprint);

internal sealed record ProjectPackageStorageImportPreflight(
    IReadOnlyList<PackageBinding> Bindings);

