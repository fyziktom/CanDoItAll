using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private IReadOnlyList<StorageCatalogSummary> storageCatalog = [];

    private IReadOnlyList<CanvasWorkbenchInputOption> BuildStorageCatalogOptions()
    {
        return storageCatalog
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Name, StringComparer.OrdinalIgnoreCase)
            .Select(storage => new CanvasWorkbenchInputOption
            {
                Value = storage.Id.ToString("D"),
                Label = $"{storage.Name} ({StoragePresentation.DescribeProvider(storage.ProviderKind)})"
            })
            .ToList();
    }

    private StorageSummaryModel? BuildSelectionStorageSummary(ProjectStructureNode node)
    {
        if (StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var storageReference) &&
            storageReference is not null)
        {
            var catalogEntry = FindStorageCatalog(storageReference.StorageId);
            return new StorageSummaryModel
            {
                Eyebrow = "Stored artifact",
                Title = string.IsNullOrWhiteSpace(storageReference.DisplayName)
                    ? string.IsNullOrWhiteSpace(node.MediaOriginalFileName) ? node.Title : node.MediaOriginalFileName
                    : storageReference.DisplayName,
                Description = $"{StoragePresentation.DescribeProvider(storageReference.ProviderKind)} via {StoragePresentation.DescribeLocator(storageReference.LocatorKind)}",
                Badges = BuildStorageBadges(catalogEntry),
                Facts =
                [
                    new StorageSummaryFact("Catalog", catalogEntry?.Name ?? "Untracked"),
                    new StorageSummaryFact("Locator", storageReference.Locator),
                    new StorageSummaryFact("Route", string.IsNullOrWhiteSpace(storageReference.Route) ? node.Route : storageReference.Route),
                    new StorageSummaryFact("Content type", string.IsNullOrWhiteSpace(storageReference.ContentType) ? "Unknown" : storageReference.ContentType)
                ],
                Footnote = storageReference.ContentLength.HasValue
                    ? $"Stored size: {FormatContentLength(storageReference.ContentLength.Value)}"
                    : string.Empty
            };
        }

        var metadata = ProjectObjectMetadataSerializer.Parse(node.MetadataJson);
        if (node.ObjectType != ProjectObjectType.Infrastructure ||
            metadata.Infrastructure?.InfrastructureKind != ProjectInfrastructureKind.StorageSystem)
        {
            return null;
        }

        var storage = FindStorageCatalog(node.NodeReferences?.InfrastructureStorageCatalogId);
        return new StorageSummaryModel
        {
            Eyebrow = "Infrastructure storage lane",
            Title = storage?.Name ?? node.Title,
            Description = storage is null
                ? "This node references a storage lane that is not currently present in the workspace catalog."
                : $"{StoragePresentation.DescribeProvider(storage.ProviderKind)} over {StoragePresentation.DescribeConnectionMode(storage.ConnectionMode)}",
            Badges = BuildStorageBadges(storage),
            Facts =
            [
                new StorageSummaryFact("Purpose", ResolveStoragePurposeLabel(metadata.Infrastructure.StoragePurpose)),
                new StorageSummaryFact("Path prefix", string.IsNullOrWhiteSpace(metadata.Infrastructure.StoragePathPrefix) ? "Not set" : metadata.Infrastructure.StoragePathPrefix),
                new StorageSummaryFact("Endpoint", storage?.EndpointOrRoot ?? "Catalog entry unavailable"),
                new StorageSummaryFact("Reference", string.IsNullOrWhiteSpace(metadata.Infrastructure.ConnectionReference) ? "Not set" : metadata.Infrastructure.ConnectionReference)
            ],
            Footnote = string.IsNullOrWhiteSpace(storage?.LastHealthMessage)
                ? string.Empty
                : storage.LastHealthMessage
        };
    }

    private StorageCatalogSummary? FindStorageCatalog(Guid? storageCatalogId)
    {
        return !storageCatalogId.HasValue
            ? null
            : storageCatalog.FirstOrDefault(storage => storage.Id == storageCatalogId.Value);
    }

    private static IReadOnlyList<StorageSummaryBadge> BuildStorageBadges(StorageCatalogSummary? storage)
    {
        if (storage is null)
        {
            return [];
        }

        var badges = new List<StorageSummaryBadge>
        {
            new(StoragePresentation.DescribeHealth(storage.HealthStatus), ResolveStorageHealthTone(storage.HealthStatus)),
            new(StoragePresentation.DescribeProvider(storage.ProviderKind), "info")
        };

        if (storage.IsReadOnly)
        {
            badges.Add(new StorageSummaryBadge("Read only", "warning"));
        }

        if (storage.IsSystemDefault)
        {
            badges.Add(new StorageSummaryBadge("System default", "accent"));
        }

        return badges;
    }

    private static string ResolveStoragePurposeLabel(string? storagePurpose)
    {
        return Enum.TryParse<StorageUsagePurpose>(storagePurpose, true, out var parsedPurpose)
            ? StoragePresentation.DescribeUsagePurpose(parsedPurpose)
            : string.IsNullOrWhiteSpace(storagePurpose)
                ? "Not set"
                : storagePurpose.Trim();
    }

    private static string ResolveStorageHealthTone(StorageHealthStatus healthStatus)
    {
        return healthStatus switch
        {
            StorageHealthStatus.Healthy => "success",
            StorageHealthStatus.Degraded => "warning",
            StorageHealthStatus.Unavailable => "danger",
            _ => "neutral"
        };
    }

    private static string FormatContentLength(long contentLength)
    {
        const double oneKilobyte = 1024d;
        const double oneMegabyte = 1024d * 1024d;

        return contentLength switch
        {
            < 1024 => $"{contentLength} B",
            < 1024 * 1024 => $"{contentLength / oneKilobyte:0.#} KB",
            _ => $"{contentLength / oneMegabyte:0.#} MB"
        };
    }
}
