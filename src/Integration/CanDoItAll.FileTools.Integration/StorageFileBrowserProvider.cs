using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

public sealed class StorageFileBrowserProvider : IFileBrowserProvider
{
    private const string PartialWarningCode = "storage-page-partial";
    private readonly StorageCatalogRecord _storage;
    private readonly IStorageBrowseDriver _driver;
    private readonly FileToolsBrowseWorkLimits _limits;
    private readonly FileToolsStorageRoot _root;
    private readonly StorageFileBrowserKeyCodec _keyCodec;
    private readonly StorageFileBrowserItemAuthorizer _itemAuthorizer;

    public StorageFileBrowserProvider(
        FileToolsSemanticScope scope,
        FileToolsStorageBinding binding,
        StorageCatalogRecord storage,
        IStorageBrowseDriver driver)
        : this(scope, binding, storage, driver, driver)
    {
    }

    internal StorageFileBrowserProvider(
        FileToolsSemanticScope scope,
        FileToolsStorageBinding binding,
        StorageCatalogRecord storage,
        IStorageBrowseDriver listingDriver,
        IStorageBrowseDriver authorityDriver)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(binding);
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _driver = listingDriver ?? throw new ArgumentNullException(nameof(listingDriver));
        ArgumentNullException.ThrowIfNull(authorityDriver);
        if (storage.Id != binding.StorageId ||
            storage.ProviderKind != listingDriver.ProviderKind ||
            storage.ProviderKind != authorityDriver.ProviderKind)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.CorruptProviderResponse,
                "The storage browser binding does not match its native provider."));
        }

        _limits = binding.WorkLimits;
        _root = binding.Root;
        FileBrowserSourceId sourceId = CreateSourceId(scope, storage.Id, _root);
        _keyCodec = new StorageFileBrowserKeyCodec(sourceId);
        _itemAuthorizer = new StorageFileBrowserItemAuthorizer(
            storage,
            authorityDriver,
            _limits,
            _root,
            _keyCodec);
        int maximumPageSize = Math.Min(_limits.MaximumReturnedItems, _driver.MaximumBudget.MaximumReturnedItems);
        Descriptor = new FileBrowserSourceDescriptor(
            sourceId,
            binding.DisplayName,
            description: scope.DisplayName,
            capabilities: FileBrowserSourceCapabilities.PagedBrowse,
            recommendedPageSize: Math.Min(50, maximumPageSize),
            maximumPageSize: maximumPageSize,
            supportedSortFields: [FileBrowserSortField.ProviderNative],
            supportedSearchScopes:
            [
                FileBrowserSearchScope.LoadedFolder,
                FileBrowserSearchScope.LoadedDescendants,
                FileBrowserSearchScope.Progressive
            ]);
    }

    public FileBrowserSourceDescriptor Descriptor { get; }

    public ValueTask<FileBrowserItem> GetRootAsync(
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new FileBrowserItem(
            _keyCodec.Root,
            parentKey: null,
            Descriptor.DisplayName,
            FileBrowserItemKind.Container,
            FileBrowserItemCategory.Folder,
            displayPath: Descriptor.DisplayName,
            childState: FileBrowserChildState.Unknown,
            metadataState: new FileBrowserMetadataState(
                FileBrowserMetadataFields.Name |
                FileBrowserMetadataFields.DisplayPath |
                FileBrowserMetadataFields.Kind |
                FileBrowserMetadataFields.ChildState),
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate));
    }

    public async ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
        FileBrowserItemKey itemKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        StorageFileBrowserKeyState state = _keyCodec.Decode(itemKey);
        StorageBrowseContainer container = ResolveContainer(state);
        try
        {
            StorageBrowsePage nativePage = await _driver.BrowseAsync(
                _storage,
                new StorageBrowseRequest(
                    container,
                    pageSize: 1,
                    metadata: StorageBrowseMetadataField.None,
                    budget: CreateBudget()),
                cancellationToken);
            var path = new List<FileBrowserItem>(nativePage.Path.Count + 1)
            {
                await GetRootAsync(metadata, cancellationToken)
            };
            FileBrowserItemKey parent = _keyCodec.Root;
            bool rootReached = _root.IsStorageRoot;
            foreach (StorageBrowsePathSegment segment in nativePage.Path)
            {
                if (!rootReached)
                {
                    rootReached = IsSameContainer(segment.Container.Key, _root.Value);
                    continue;
                }

                if (segment.Container.IsRoot || IsSameContainer(segment.Container.Key, _root.Value))
                {
                    continue;
                }

                FileBrowserItemKey key = _keyCodec.EncodeContainer(segment.Container.Key);
                path.Add(new FileBrowserItem(
                    key,
                    parent,
                    segment.DisplayName,
                    FileBrowserItemKind.Container,
                    FileBrowserItemCategory.Folder,
                    segment.DisplayName,
                    FileBrowserChildState.Unknown,
                    capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate));
                parent = key;
            }

            return path;
        }
        catch (StorageBrowseException exception)
        {
            throw StorageFileBrowserMapping.MapException(exception);
        }
    }

    public async ValueTask<FileBrowserPage> BrowseAsync(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        StorageFileBrowserKeyState parentState = _keyCodec.Decode(request.ParentKey);
        if (parentState.Kind == StorageFileBrowserKeyKind.Item)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.InvalidLocation,
                "Only a container can be browsed."));
        }

        StorageBrowseContainer container = ResolveContainer(parentState);
        try
        {
            StorageBrowsePage nativePage = await _driver.BrowseAsync(
                _storage,
                new StorageBrowseRequest(
                    container,
                    request.PageSize,
                    request.ContinuationToken is null
                        ? null
                        : new StorageBrowseCursor(request.ContinuationToken),
                    StorageBrowseSort.ProviderOrder,
                    StorageFileBrowserMapping.MapMetadata(request.Metadata),
                    CreateBudget()),
                cancellationToken);
            FileBrowserItem[] items = nativePage.Entries
                .Select(entry => StorageFileBrowserMapping.MapItem(
                    _keyCodec,
                    request.ParentKey,
                    container,
                    entry,
                    nativePage.Completeness))
                .ToArray();
            FileBrowserCompleteness completeness = nativePage.Completeness == StorageBrowseCompleteness.Complete
                ? FileBrowserCompleteness.Complete
                : FileBrowserCompleteness.Partial;
            FileBrowserPageWarning[] warnings = completeness == FileBrowserCompleteness.Partial
                ? [new FileBrowserPageWarning(PartialWarningCode, "The storage provider reached a declared work limit.")]
                : [];
            return new FileBrowserPage(
                items,
                nativePage.NextCursor?.Token,
                consistencyToken: nativePage.Consistency?.Value,
                completeness: completeness,
                warnings: warnings);
        }
        catch (StorageBrowseException exception)
        {
            throw StorageFileBrowserMapping.MapException(exception);
        }
    }

    internal async ValueTask<AuthorizedBrowserFile> AuthorizeItemAsync(
        FileBrowserItemKey itemKey,
        FileAccessContext context,
        FileToolsSemanticScope scope,
        FileAccessOperation operations,
        IStorageFileAccessAuthorizationCoordinator coordinator,
        CancellationToken cancellationToken = default)
        => await _itemAuthorizer.AuthorizeAsync(
            itemKey,
            context,
            scope,
            operations,
            coordinator,
            cancellationToken);

    private StorageBrowseWorkBudget CreateBudget()
        => new(
            Math.Min(_limits.MaximumReturnedItems, _driver.MaximumBudget.MaximumReturnedItems),
            Math.Min(_limits.MaximumInspectedItems, _driver.MaximumBudget.MaximumInspectedItems),
            Math.Min(_limits.MaximumMetadataProbes, _driver.MaximumBudget.MaximumMetadataProbes),
            Math.Min(
                _limits.MaximumConcurrentMetadataProbes,
                _driver.MaximumBudget.MaximumConcurrentMetadataProbes),
            _limits.MaximumDuration <= _driver.MaximumBudget.MaximumDuration
                ? _limits.MaximumDuration
                : _driver.MaximumBudget.MaximumDuration);

    private static void ValidateRequest(FileBrowserBrowseRequest request)
    {
        if (request.IncludeDescendants || !request.Filter.IsEmpty)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.Unsupported,
                "The native storage adapter supports shallow unfiltered browsing only."));
        }

        if (request.Sort.Field != FileBrowserSortField.ProviderNative ||
            request.Sort.Direction != FileBrowserSortDirection.Ascending ||
            request.Sort.FoldersFirst)
        {
            throw new FileBrowserProviderException(new FileBrowserError(
                FileBrowserErrorCode.Unsupported,
                "The native storage adapter supports provider-native forward ordering only."));
        }
    }

    private StorageBrowseContainer ResolveContainer(StorageFileBrowserKeyState state)
    {
        string key = state.Kind == StorageFileBrowserKeyKind.Root ? _root.Value : state.Container;
        StringComparison comparison = _storage.ProviderKind == StorageProviderKind.FileSystem
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!_root.IsStorageRoot &&
            !string.Equals(key, _root.Value, comparison) &&
            !key.StartsWith(_root.Value + "/", comparison))
        {
            throw new FileAccessDeniedException(
                FileAccessFailureCode.Forbidden,
                "The file browser location is outside its semantic storage root.");
        }

        return new StorageBrowseContainer(key);
    }

    private bool IsSameContainer(string left, string right)
        => string.Equals(
            left,
            right,
            _storage.ProviderKind == StorageProviderKind.FileSystem
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static FileBrowserSourceId CreateSourceId(
        FileToolsSemanticScope scope,
        Guid storageId,
        FileToolsStorageRoot root)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"{scope.Kind}:{scope.Id}:{storageId:N}:{root.Value}");
        return new FileBrowserSourceId($"storage-{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))}");
    }

}
