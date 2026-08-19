using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FileSystemStorageBrowseDriver : IStorageBrowseDriver, IStorageBrowseStatDriver
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        MatchType = MatchType.Simple
    };

    private readonly FileSystemStoragePathPolicy _pathPolicy;
    private readonly FileSystemStorageBrowseCursorCodec _cursorCodec;
    private readonly ILogger<FileSystemStorageBrowseDriver> _logger;
    private readonly Func<DirectoryInfo, IEnumerable<FileSystemInfo>> _enumerateEntries;

    public FileSystemStorageBrowseDriver(
        FileSystemStoragePathPolicy pathPolicy,
        ILogger<FileSystemStorageBrowseDriver> logger)
        : this(
            pathPolicy,
            new FileSystemStorageBrowseCursorCodec(),
            logger,
            static directory => directory.EnumerateFileSystemInfos("*", EnumerationOptions))
    {
    }

    internal FileSystemStorageBrowseDriver(
        FileSystemStoragePathPolicy pathPolicy,
        FileSystemStorageBrowseCursorCodec cursorCodec,
        ILogger<FileSystemStorageBrowseDriver> logger,
        Func<DirectoryInfo, IEnumerable<FileSystemInfo>> enumerateEntries)
    {
        _pathPolicy = pathPolicy ?? throw new ArgumentNullException(nameof(pathPolicy));
        _cursorCodec = cursorCodec ?? throw new ArgumentNullException(nameof(cursorCodec));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enumerateEntries = enumerateEntries ?? throw new ArgumentNullException(nameof(enumerateEntries));
    }

    public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

    public StorageBrowseCapability Capabilities =>
        StorageBrowseCapability.Browse |
        StorageBrowseCapability.Stat |
        StorageBrowseCapability.GlobalNameOrdering |
        StorageBrowseCapability.ConsistentContinuation |
        StorageBrowseCapability.Metadata;

    public StorageBrowseWorkBudget MaximumBudget { get; } = new(
        maximumReturnedItems: 500,
        maximumInspectedItems: 100_000,
        maximumMetadataProbes: 10_000,
        maximumConcurrentMetadataProbes: 1,
        maximumDuration: TimeSpan.FromMinutes(2));

    public Task<StorageBrowsePage> BrowseAsync(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStorage(storage);
        ValidateRequest(request);

        try
        {
            string directoryPath = _pathPolicy.ResolveDirectory(storage, request.Container);
            var directory = new DirectoryInfo(directoryPath);
            var stopwatch = Stopwatch.StartNew();
            IReadOnlyList<FileSystemInfo> orderedEntries = EnumerateDeterministically(
                directory,
                request,
                stopwatch,
                cancellationToken,
                out int inspectedItems,
                out long directoryVersion);
            int offset = ResolveOffset(storage, request, directoryVersion);
            if (offset >= request.Budget.MaximumInspectedItems)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.BudgetExceeded,
                    "The filesystem continuation offset exceeds the current inspection budget."));
            }

            var entries = new List<StorageBrowseEntry>(request.PageSize);
            int metadataProbes = 0;
            int nextOffset = offset;
            bool hasMore = false;
            StorageBrowseCompleteness completeness = StorageBrowseCompleteness.Complete;
            for (var currentIndex = offset; currentIndex < orderedEntries.Count; currentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stopwatch.Elapsed >= request.Budget.MaximumDuration)
                {
                    completeness = StorageBrowseCompleteness.PartialTimeLimit;
                    hasMore = true;
                    break;
                }

                if (entries.Count == request.PageSize)
                {
                    nextOffset = currentIndex;
                    hasMore = true;
                    break;
                }

                if (request.Metadata != StorageBrowseMetadataField.None &&
                    metadataProbes >= request.Budget.MaximumMetadataProbes)
                {
                    nextOffset = currentIndex;
                    completeness = StorageBrowseCompleteness.PartialMetadataLimit;
                    hasMore = true;
                    break;
                }

                FileSystemInfo info = orderedEntries[currentIndex];
                entries.Add(FileSystemStorageBrowseEntryMapper.CreateEntry(request.Container, info, request.Metadata));
                nextOffset = currentIndex + 1;
                if (request.Metadata != StorageBrowseMetadataField.None)
                {
                    metadataProbes++;
                }
            }

            StorageBrowseCursor? nextCursor = hasMore
                ? CreateCursor(storage, request, nextOffset, directoryVersion)
                : null;
            stopwatch.Stop();
            var metrics = new StorageBrowseOperationMetrics(
                entries.Count,
                inspectedItems,
                metadataProbes,
                nextCursor?.Token.Length ?? 0,
                stopwatch.Elapsed);
            StorageBrowsePage page = FileSystemStorageBrowseEntryMapper.CreatePage(
                storage,
                request,
                entries,
                completeness,
                metrics,
                nextCursor,
                directoryVersion);
            LogCompleted(storage, page);
            return Task.FromResult(page);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Filesystem browse cancelled for provider {ProviderKind} and storage {StorageId}.",
                ProviderKind,
                storage.Id);
            throw;
        }
        catch (StorageBrowseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                "Filesystem browse failed for provider {ProviderKind}, storage {StorageId}, and failure {FailureType}.",
                ProviderKind,
                storage.Id,
                exception.GetType().Name);
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The filesystem source could not complete the browse operation.",
                isRetryable: true));
        }
    }

    public Task<StorageBrowseEntry> StatAsync(
        StorageCatalogRecord storage,
        StorageBrowseStatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStorage(storage);
        string fullPath = _pathPolicy.ResolveFullPath(storage, request.EntryId.Value);
        FileSystemInfo info = Directory.Exists(fullPath)
            ? new DirectoryInfo(fullPath)
            : new FileInfo(fullPath);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.ProviderUnavailable,
                "The requested filesystem entry is unavailable."));
        }

        return Task.FromResult(FileSystemStorageBrowseEntryMapper.CreateEntry(request.Container, info, request.Metadata));
    }

    private static void ValidateStorage(StorageCatalogRecord storage)
    {
        if (storage.ProviderKind != StorageProviderKind.FileSystem)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The filesystem browse driver received a different provider kind."));
        }
    }

    private void ValidateRequest(StorageBrowseRequest request)
    {
        if (request.PageSize > MaximumBudget.MaximumReturnedItems ||
            request.Budget.MaximumInspectedItems > MaximumBudget.MaximumInspectedItems ||
            request.Budget.MaximumMetadataProbes > MaximumBudget.MaximumMetadataProbes ||
            request.Budget.MaximumDuration > MaximumBudget.MaximumDuration)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The filesystem browse request exceeds provider limits."));
        }

        if (request.Sort != StorageBrowseSort.ProviderOrder &&
            request.Sort != new StorageBrowseSort(
                StorageBrowseSortField.Name,
                StorageBrowseSortDirection.Ascending))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.UnsupportedOperation,
                "The filesystem provider supports only deterministic ascending logical-name ordering."));
        }
    }

    private IReadOnlyList<FileSystemInfo> EnumerateDeterministically(
        DirectoryInfo directory,
        StorageBrowseRequest request,
        Stopwatch stopwatch,
        CancellationToken cancellationToken,
        out int inspectedItems,
        out long directoryVersion)
    {
        inspectedItems = 0;
        var candidates = new List<(string LogicalKey, string PhysicalName, FileSystemInfo Info)>();
        foreach (FileSystemInfo info in _enumerateEntries(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stopwatch.Elapsed >= request.Budget.MaximumDuration)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.BudgetExceeded,
                    "Deterministic filesystem ordering could not inspect the complete container within the time budget."));
            }

            inspectedItems++;
            if (inspectedItems > request.Budget.MaximumInspectedItems)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.BudgetExceeded,
                    "Deterministic filesystem ordering requires a complete container snapshot within the inspection budget."));
            }

            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (info.Name.EndsWith(".candoitall.lock", StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add((
                FileSystemStorageKeyCodec.Append(request.Container.Key, info.Name),
                info.Name,
                info));
        }

        FileSystemInfo[] orderedEntries = candidates
            .OrderBy(candidate => candidate.LogicalKey, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.PhysicalName, StringComparer.Ordinal)
            .Select(candidate => candidate.Info)
            .ToArray();
        directoryVersion = ComputeSnapshotVersion(request.Container.Key, orderedEntries);
        return orderedEntries;
    }

    private int ResolveOffset(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        long directoryVersion)
    {
        if (request.Cursor is null)
        {
            return 0;
        }

        FileSystemStorageBrowseCursorState state = _cursorCodec.Decode(request.Cursor);
        bool matchesRequest = state.StorageId == storage.Id &&
                              state.ContainerFingerprint ==
                              FileSystemStorageBrowseCursorCodec.CreateContainerFingerprint(request.Container) &&
                              state.PageSize == request.PageSize &&
                              state.SortField == request.Sort.Field &&
                              state.SortDirection == request.Sort.Direction &&
                              state.Metadata == request.Metadata;
        if (!matchesRequest)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidCursor,
                "The filesystem browse cursor does not match the current request."));
        }

        if (state.DirectoryVersion != directoryVersion)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.SourceChanged,
                "The filesystem container changed after the cursor was issued."));
        }

        return state.Offset;
    }

    private StorageBrowseCursor CreateCursor(
        StorageCatalogRecord storage,
        StorageBrowseRequest request,
        int offset,
        long directoryVersion)
        => _cursorCodec.Encode(new FileSystemStorageBrowseCursorState(
            storage.Id,
            FileSystemStorageBrowseCursorCodec.CreateContainerFingerprint(request.Container),
            offset,
            request.PageSize,
            request.Sort.Field,
            request.Sort.Direction,
            request.Metadata,
            directoryVersion));

    private static long ComputeSnapshotVersion(
        string containerKey,
        IReadOnlyList<FileSystemInfo> entries)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, containerKey);
        Span<byte> metadata = stackalloc byte[20];
        foreach (FileSystemInfo entry in entries)
        {
            entry.Refresh();
            AppendString(hash, entry.Name);
            BinaryPrimitives.WriteInt32LittleEndian(metadata, (int)entry.Attributes);
            BinaryPrimitives.WriteInt64LittleEndian(metadata[4..], entry.LastWriteTimeUtc.Ticks);
            BinaryPrimitives.WriteInt64LittleEndian(
                metadata[12..],
                entry is FileInfo file ? file.Length : -1L);
            hash.AppendData(metadata);
        }

        byte[] digest = hash.GetHashAndReset();
        return BinaryPrimitives.ReadInt64LittleEndian(digest);
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, byteCount);
        hash.AppendData(length);
        hash.AppendData(Encoding.UTF8.GetBytes(value));
    }

    private void LogCompleted(StorageCatalogRecord storage, StorageBrowsePage page)
    {
        _logger.LogInformation(
            "Filesystem browse completed for provider {ProviderKind}, storage {StorageId}, returned {ReturnedItems}, inspected {InspectedItems}, metadata {MetadataProbes}, retained {RetainedStateBytes}, duration {DurationMs}, completeness {Completeness}.",
            ProviderKind,
            storage.Id,
            page.Metrics.ReturnedItems,
            page.Metrics.InspectedItems,
            page.Metrics.MetadataProbes,
            page.Metrics.RetainedStateBytes,
            page.Metrics.Duration.TotalMilliseconds,
            page.Completeness);
    }
}
