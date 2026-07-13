using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed record AuthorizedBrowserFile(
    FileReference File,
    string FileName,
    string? MediaType,
    long? Size);

internal sealed class StorageFileBrowserItemAuthorizer(
    StorageCatalogRecord storage,
    IStorageBrowseDriver driver,
    FileToolsBrowseWorkLimits limits,
    FileToolsStorageRoot root,
    StorageFileBrowserKeyCodec keyCodec)
{
    public async ValueTask<AuthorizedBrowserFile> AuthorizeAsync(
        FileBrowserItemKey itemKey,
        FileAccessContext context,
        FileToolsSemanticScope scope,
        FileAccessOperation operations,
        IStorageFileAccessAuthorizationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        StorageFileBrowserKeyState state = keyCodec.Decode(itemKey);
        if (state.Kind != StorageFileBrowserKeyKind.Item || string.IsNullOrWhiteSpace(state.EntryId))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.InvalidHandle,
                "Only a current file occurrence can be authorized.");
        }

        StringComparison comparison = storage.ProviderKind == StorageProviderKind.FileSystem
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!root.IsStorageRoot &&
            !string.Equals(state.Container, root.Value, comparison) &&
            !state.Container.StartsWith(root.Value + "/", comparison))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Forbidden,
                "The file occurrence is outside its semantic storage root.");
        }

        var container = new StorageBrowseContainer(state.Container);
        try
        {
            if (driver.Capabilities.HasFlag(StorageBrowseCapability.Stat) &&
                driver is IStorageBrowseStatDriver statDriver)
            {
                StorageBrowseEntry entry = await statDriver.StatAsync(
                    storage,
                    new StorageBrowseStatRequest(
                        container,
                        new StorageBrowseEntryId(state.EntryId),
                        limits.MaximumMetadataProbes > 0
                            ? StorageBrowseMetadataField.Size | StorageBrowseMetadataField.MediaType
                            : StorageBrowseMetadataField.None,
                        CreateStatBudget()),
                    cancellationToken);
                if (!string.Equals(entry.Id.Value, state.EntryId, StringComparison.Ordinal))
                {
                    throw new FileAccessDeniedException(
                        FileAccessFailureCode.SourceUnavailable,
                        "The storage provider returned a different file occurrence.");
                }

                return await GrantAsync(
                    container,
                    entry,
                    itemKey,
                    context,
                    scope,
                    operations,
                    coordinator,
                    cancellationToken);
            }

            StorageBrowseCursor? cursor = null;
            StorageBrowseWorkBudget maximumBudget = CreateBudget();
            int remainingInspections = maximumBudget.MaximumInspectedItems;
            while (remainingInspections > 0)
            {
                int pageSize = Math.Min(maximumBudget.MaximumReturnedItems, remainingInspections);
                int metadataProbes = Math.Min(maximumBudget.MaximumMetadataProbes, remainingInspections);
                StorageBrowsePage page = await driver.BrowseAsync(
                    storage,
                    new StorageBrowseRequest(
                        container,
                        pageSize,
                        cursor,
                        StorageBrowseSort.ProviderOrder,
                        StorageBrowseMetadataField.Size | StorageBrowseMetadataField.MediaType,
                        new StorageBrowseWorkBudget(
                            pageSize,
                            remainingInspections,
                            metadataProbes,
                            Math.Min(maximumBudget.MaximumConcurrentMetadataProbes, metadataProbes),
                            maximumBudget.MaximumDuration)),
                    cancellationToken);
                StorageBrowseEntry? entry = null;
                foreach (StorageBrowseEntry candidate in page.Entries)
                {
                    if (string.Equals(candidate.Id.Value, state.EntryId, StringComparison.Ordinal))
                    {
                        entry = candidate;
                        break;
                    }
                }

                if (entry is not null)
                {
                    return await GrantAsync(
                        container,
                        entry,
                        itemKey,
                        context,
                        scope,
                        operations,
                        coordinator,
                        cancellationToken);
                }

                remainingInspections -= page.Metrics.InspectedItems;
                if (page.NextCursor is null || page.Metrics.InspectedItems == 0)
                {
                    break;
                }

                cursor = page.NextCursor;
            }
        }
        catch (StorageBrowseException)
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.SourceUnavailable,
                "The current file occurrence could not be re-resolved.");
        }

        throw new FileAccessDeniedException(
            FileAccessFailureCode.SourceUnavailable,
            "The current file occurrence no longer exists within the authorized browse bounds.");
    }

    private async ValueTask<AuthorizedBrowserFile> GrantAsync(
        StorageBrowseContainer container,
        StorageBrowseEntry entry,
        FileBrowserItemKey itemKey,
        FileAccessContext context,
        FileToolsSemanticScope scope,
        FileAccessOperation operations,
        IStorageFileAccessAuthorizationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (entry.Kind != StorageBrowseEntryKind.File ||
            !entry.Capabilities.HasFlag(StorageBrowseEntryCapability.Read))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.OperationDenied,
                "The current storage occurrence is not readable.");
        }

        StringComparison parentComparison = storage.ProviderKind == StorageProviderKind.FileSystem
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(entry.Parent.Key, container.Key, parentComparison) ||
            (storage.ProviderKind is StorageProviderKind.FileSystem or StorageProviderKind.Ftp &&
             !IsDirectChild(container.Key, entry.Id.Value)))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.SourceUnavailable,
                "The storage provider returned an occurrence outside the current container.");
        }

        FileReference file = await coordinator.GrantAsync(
            new FileAccessGrantRequest(
                context,
                scope,
                storage.Id,
                entry.Id.Value,
                operations,
                itemKey.Revision),
            CreateStorageReference(entry),
            cancellationToken);
        return new AuthorizedBrowserFile(file, entry.Name, entry.MediaType, entry.Size);
    }

    private static bool IsDirectChild(string container, string entryId)
    {
        string normalizedContainer = container.Trim('/');
        string normalizedEntry = entryId.Trim('/');
        if (normalizedContainer.Length == 0)
        {
            return !normalizedEntry.Contains('/');
        }

        string prefix = normalizedContainer + "/";
        return normalizedEntry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               !normalizedEntry.AsSpan(prefix.Length).Contains('/');
    }

    private StorageBrowseWorkBudget CreateBudget()
        => new(
            Math.Min(limits.MaximumReturnedItems, driver.MaximumBudget.MaximumReturnedItems),
            Math.Min(limits.MaximumInspectedItems, driver.MaximumBudget.MaximumInspectedItems),
            Math.Min(limits.MaximumMetadataProbes, driver.MaximumBudget.MaximumMetadataProbes),
            Math.Min(
                limits.MaximumConcurrentMetadataProbes,
                driver.MaximumBudget.MaximumConcurrentMetadataProbes),
            limits.MaximumDuration <= driver.MaximumBudget.MaximumDuration
                ? limits.MaximumDuration
                : driver.MaximumBudget.MaximumDuration);

    private StorageBrowseWorkBudget CreateStatBudget()
    {
        int metadataProbes = limits.MaximumMetadataProbes > 0 ? 1 : 0;
        return new StorageBrowseWorkBudget(
            maximumReturnedItems: 1,
            maximumInspectedItems: 1,
            maximumMetadataProbes: metadataProbes,
            maximumConcurrentMetadataProbes: metadataProbes,
            maximumDuration: limits.MaximumDuration <= driver.MaximumBudget.MaximumDuration
                ? limits.MaximumDuration
                : driver.MaximumBudget.MaximumDuration);
    }

    private StorageObjectReference CreateStorageReference(StorageBrowseEntry entry)
    {
        StorageLocatorKind locatorKind = storage.ProviderKind switch
        {
            StorageProviderKind.FileSystem => StorageLocatorKind.RelativePath,
            StorageProviderKind.Ipfs when entry.Id.Value.StartsWith("cid:", StringComparison.Ordinal) =>
                StorageLocatorKind.ContentAddress,
            StorageProviderKind.Ipfs => StorageLocatorKind.RemotePath,
            StorageProviderKind.Ftp => StorageLocatorKind.RemotePath,
            _ => throw new FileAccessDeniedException(
                FileAccessFailureCode.Unsupported,
                "The storage provider cannot produce an authorized content occurrence.")
        };
        string locator = locatorKind == StorageLocatorKind.ContentAddress
            ? entry.Id.Value["cid:".Length..]
            : entry.Id.Value;
        return new StorageObjectReference(
            storage.Id,
            storage.ProviderKind,
            locatorKind,
            locator,
            entry.Name,
            entry.MediaType ?? "application/octet-stream",
            entry.Size);
    }
}
