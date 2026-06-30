using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Factory.Pages;

public partial class PromptFactoryPage
{
    private static StorageSummaryModel? BuildAttachmentStorageSummary(PromptSessionAttachmentSummary attachment)
    {
        if (!StorageJson.TryParseReference(attachment.StorageObjectReferenceJson, out var storageReference) ||
            storageReference is null)
        {
            return null;
        }

        return new StorageSummaryModel
        {
            Eyebrow = "Storage context",
            Title = string.IsNullOrWhiteSpace(storageReference.DisplayName)
                ? ResolveAttachmentStorageTitle(attachment)
                : storageReference.DisplayName,
            Description = $"{StoragePresentation.DescribeProvider(storageReference.ProviderKind)} via {StoragePresentation.DescribeLocator(storageReference.LocatorKind)}",
            Badges =
            [
                new StorageSummaryBadge(StoragePresentation.DescribeProvider(storageReference.ProviderKind), "info"),
                new StorageSummaryBadge(StoragePresentation.DescribeLocator(storageReference.LocatorKind))
            ],
            Facts =
            [
                new StorageSummaryFact("Locator", storageReference.Locator),
                new StorageSummaryFact("Route", string.IsNullOrWhiteSpace(storageReference.Route) ? attachment.MediaRoute : storageReference.Route),
                new StorageSummaryFact("Content type", string.IsNullOrWhiteSpace(storageReference.ContentType) ? ResolveAttachmentContentType(attachment) : storageReference.ContentType)
            ],
            Footnote = storageReference.ContentLength.HasValue
                ? $"Stored size: {FormatAttachmentStorageLength(storageReference.ContentLength.Value)}"
                : string.Empty
        };
    }

    private static string ResolveAttachmentStorageTitle(PromptSessionAttachmentSummary attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.MediaOriginalFileName))
        {
            return attachment.MediaOriginalFileName;
        }

        if (!string.IsNullOrWhiteSpace(attachment.Title))
        {
            return attachment.Title;
        }

        return "Stored attachment";
    }

    private static string ResolveAttachmentContentType(PromptSessionAttachmentSummary attachment)
    {
        return string.IsNullOrWhiteSpace(attachment.MediaContentType)
            ? "Unknown"
            : attachment.MediaContentType;
    }

    private static string FormatAttachmentStorageLength(long contentLength)
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
