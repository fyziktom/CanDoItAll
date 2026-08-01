using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class FileSystemStorageBrowseDriverTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SB03_INV_BOUNDED_PAGE_ONE_LargeDirectory_DoesNotEnumerateWholeDirectory()
    {
        string root = CreateRoot();
        try
        {
            CreateFiles(root, 1_000);
            int enumeratedEntries = 0;
            IEnumerable<FileSystemInfo> CountEnumeration(DirectoryInfo directory)
            {
                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
                {
                    enumeratedEntries++;
                    yield return entry;
                }
            }

            FileSystemStorageBrowseDriver sut = CreateSut(root, CountEnumeration);
            var budget = new StorageBrowseWorkBudget(
                maximumReturnedItems: 25,
                maximumInspectedItems: 64,
                maximumMetadataProbes: 0,
                maximumConcurrentMetadataProbes: 0,
                maximumDuration: TimeSpan.FromSeconds(10));

            StorageBrowsePage page = await sut.BrowseAsync(
                CreateStorage(root),
                new StorageBrowseRequest(StorageBrowseContainer.Root, pageSize: 25, budget: budget));

            output.WriteLine(
                "SB03_INV_BOUNDED_PAGE_ONE returned={0} reported-inspected={1} enumerated={2} retained={3}",
                page.Metrics.ReturnedItems,
                page.Metrics.InspectedItems,
                enumeratedEntries,
                page.Metrics.RetainedStateBytes);
            Assert.Equal(25, page.Entries.Count);
            Assert.InRange(page.Metrics.InspectedItems, 26, 64);
            Assert.InRange(enumeratedEntries, 26, 64);
            Assert.NotNull(page.NextCursor);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    [Trait("Category", "Scale")]
    public async Task SB03_INV_SCALE_100K_FirstAndSecondPages_StayWithinStructuralBudgets()
    {
        string root = CreateRoot();
        try
        {
            const int fixtureSize = 100_000;
            CreateFiles(root, fixtureSize);
            FileSystemStorageBrowseDriver sut = CreateSut(root);
            StorageCatalogRecord storage = CreateStorage(root);
            var budget = new StorageBrowseWorkBudget(
                maximumReturnedItems: 50,
                maximumInspectedItems: 256,
                maximumMetadataProbes: 0,
                maximumConcurrentMetadataProbes: 0,
                maximumDuration: TimeSpan.FromMinutes(2));
            var request = new StorageBrowseRequest(
                StorageBrowseContainer.Root,
                pageSize: 50,
                budget: budget);
            var elapsedRuns = new List<double>();
            long worstAllocation = 0;
            StorageBrowsePage? first = null;
            for (int run = 0; run < 3; run++)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                StorageBrowsePage page = await sut.BrowseAsync(storage, request);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                elapsedRuns.Add(page.Metrics.Duration.TotalMilliseconds);
                worstAllocation = Math.Max(worstAllocation, allocated);
                first = page;

                Assert.Equal(50, page.Entries.Count);
                Assert.Equal(51, page.Metrics.InspectedItems);
                Assert.Equal(0, page.Metrics.MetadataProbes);
                Assert.InRange(page.Metrics.RetainedStateBytes, 1, 512);
                Assert.InRange(allocated, 1, 4 * 1024 * 1024);
            }

            StorageBrowsePage acceptedFirst = Assert.IsType<StorageBrowsePage>(first);
            StorageBrowsePage second = await sut.BrowseAsync(
                storage,
                new StorageBrowseRequest(
                    StorageBrowseContainer.Root,
                    pageSize: 50,
                    cursor: acceptedFirst.NextCursor,
                    budget: budget));
            string[] firstIds = acceptedFirst.Entries.Select(entry => entry.Id.Value).ToArray();
            string[] secondIds = second.Entries.Select(entry => entry.Id.Value).ToArray();

            output.WriteLine(
                "SB03_INV_SCALE_100K runtime={0} os={1} fixture={2} median-ms={3:F3} worst-ms={4:F3} worst-allocated={5} first-inspected={6} second-inspected={7} retained={8}",
                Environment.Version,
                Environment.OSVersion.VersionString,
                fixtureSize,
                elapsedRuns.OrderBy(value => value).ElementAt(1),
                elapsedRuns.Max(),
                worstAllocation,
                acceptedFirst.Metrics.InspectedItems,
                second.Metrics.InspectedItems,
                acceptedFirst.Metrics.RetainedStateBytes);
            Assert.Equal(50, second.Entries.Count);
            Assert.InRange(second.Metrics.InspectedItems, 101, 256);
            Assert.Empty(firstIds.Intersect(secondIds, StringComparer.Ordinal));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_CONFINEMENT_TraversalContainer_IsRejectedWithoutRootDisclosure()
    {
        string root = CreateRoot();
        try
        {
            FileSystemStorageBrowseDriver sut = CreateSut(root);

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(new StorageBrowseContainer("../outside"))));

            Assert.Equal(StorageBrowseErrorCode.AccessDenied, exception.Error.Code);
            Assert.DoesNotContain(root, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_CONFINEMENT_ReparseContainer_IsRejected()
    {
        string root = CreateRoot();
        string outside = CreateRoot();
        try
        {
            string link = Path.Combine(root, "linked");
            Directory.CreateSymbolicLink(link, outside);
            FileSystemStorageBrowseDriver sut = CreateSut(root);

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(new StorageBrowseContainer("linked"))));

            Assert.Equal(StorageBrowseErrorCode.AccessDenied, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(root);
            DeleteRoot(outside);
        }
    }

    [Fact]
    public void LocalOpenTrust_rejects_a_reparse_point_storage_root()
    {
        string workspaceRoot = CreateRoot();
        string outsideRoot = CreateRoot();
        try
        {
            string linkedStorageRoot = Path.Combine(workspaceRoot, "linked-storage");
            Directory.CreateSymbolicLink(linkedStorageRoot, outsideRoot);
            var pathPolicy = new FileSystemStoragePathPolicy(
                new TestWorkspacePathResolver(workspaceRoot));
            StorageCatalogRecord storage = CreateStorage(linkedStorageRoot);

            Assert.False(pathPolicy.IsTrustedForLocalOpen(storage));
            StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
                pathPolicy.ResolveTrustedLocalOpenPath(storage, "report.xlsx"));
            Assert.Equal(StorageBrowseErrorCode.AccessDenied, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(workspaceRoot);
            DeleteRoot(outsideRoot);
        }
    }

    [Fact]
    public void TrustedWorkspacePath_uses_platform_path_case_semantics()
    {
        string parentRoot = CreateRoot();
        try
        {
            string workspaceRoot = Path.Combine(parentRoot, "workspace");
            Directory.CreateDirectory(workspaceRoot);
            string caseDifferentPath = Path.Combine(parentRoot, "Workspace", "report.xlsx");
            var pathPolicy = new FileSystemStoragePathPolicy(
                new TestWorkspacePathResolver(workspaceRoot));

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    Path.GetFullPath(caseDifferentPath),
                    pathPolicy.ResolveTrustedWorkspacePath(caseDifferentPath));
                return;
            }

            StorageBrowseException exception = Assert.Throws<StorageBrowseException>(() =>
                pathPolicy.ResolveTrustedWorkspacePath(caseDifferentPath));
            Assert.Equal(StorageBrowseErrorCode.AccessDenied, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(parentRoot);
        }
    }

    [Fact]
    public async Task SB03_INV_CURSOR_DirectoryMutation_InvalidatesContinuation()
    {
        string root = CreateRoot();
        try
        {
            CreateFiles(root, 3);
            FileSystemStorageBrowseDriver sut = CreateSut(root);
            StorageCatalogRecord storage = CreateStorage(root);
            var request = new StorageBrowseRequest(StorageBrowseContainer.Root, pageSize: 1);
            StorageBrowsePage first = await sut.BrowseAsync(storage, request);
            DateTime previousWrite = Directory.GetLastWriteTimeUtc(root);
            File.WriteAllText(Path.Combine(root, "added.txt"), "changed");
            Directory.SetLastWriteTimeUtc(root, previousWrite.AddSeconds(2));

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    storage,
                    new StorageBrowseRequest(
                        StorageBrowseContainer.Root,
                        pageSize: 1,
                        cursor: first.NextCursor)));

            Assert.Equal(StorageBrowseErrorCode.SourceChanged, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_FRESHNESS_ReplacedContentAndMetadata_AreVisibleWithoutBrowserState()
    {
        string root = CreateRoot();
        try
        {
            string filePath = Path.Combine(root, "live.txt");
            await File.WriteAllTextAsync(filePath, "old");
            var pathPolicy = new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root));
            var browseDriver = new FileSystemStorageBrowseDriver(
                pathPolicy,
                NullLogger<FileSystemStorageBrowseDriver>.Instance);
            var contentDriver = new FileSystemStorageDriver(pathPolicy);
            StorageCatalogRecord storage = CreateStorage(root);
            var request = new StorageBrowseRequest(
                StorageBrowseContainer.Root,
                metadata: StorageBrowseMetadataField.Size | StorageBrowseMetadataField.ModifiedAtUtc);
            StorageBrowsePage before = await browseDriver.BrowseAsync(storage, request);

            await File.WriteAllTextAsync(filePath, "replacement-content");
            StorageBrowsePage after = await browseDriver.BrowseAsync(storage, request);
            await using Stream stream = await contentDriver.OpenReadAsync(
                storage,
                new StorageObjectReference(
                    storage.Id,
                    StorageProviderKind.FileSystem,
                    StorageLocatorKind.RelativePath,
                    "live.txt"));
            using var reader = new StreamReader(stream);
            string content = await reader.ReadToEndAsync();

            Assert.Equal(3, Assert.Single(before.Entries).Size);
            Assert.Equal(19, Assert.Single(after.Entries).Size);
            Assert.Equal("replacement-content", content);
            Assert.Equal(1, after.Metrics.MetadataProbes);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB07_RevisionedReplace_RejectsStaleRevisionAndPersistsOnlyOnSuccess()
    {
        string root = CreateRoot();
        try
        {
            string filePath = Path.Combine(root, "editable.txt");
            await File.WriteAllTextAsync(filePath, "initial");
            var driver = new FileSystemStorageDriver(
                new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root)));
            StorageCatalogRecord storage = CreateStorage(root);
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "editable.txt",
                "editable.txt",
                "text/plain");
            StorageContentRevision original = Assert.IsType<StorageContentRevision>(
                await driver.GetRevisionAsync(storage, reference));
            await File.WriteAllTextAsync(filePath, "external-change");

            StorageContentConflictException conflict = await Assert.ThrowsAsync<StorageContentConflictException>(() =>
                driver.ReplaceAsync(
                    storage,
                    new StorageRevisionedWriteRequest(
                        reference,
                        "stale-write"u8.ToArray(),
                        original,
                        allowOverwrite: false)));

            Assert.Equal(original, conflict.ExpectedRevision);
            Assert.Equal("external-change", await File.ReadAllTextAsync(filePath));
            StorageContentRevision current = Assert.IsType<StorageContentRevision>(
                await driver.GetRevisionAsync(storage, reference));
            StorageRevisionedWriteResult result = await driver.ReplaceAsync(
                storage,
                new StorageRevisionedWriteRequest(
                    reference,
                    "persisted"u8.ToArray(),
                    current,
                    allowOverwrite: false));
            Assert.NotEqual(current, result.PersistedRevision);
            Assert.Equal("persisted", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_CANCELLATION_CancelledEnumeration_DoesNotPublishSuccess()
    {
        string root = CreateRoot();
        try
        {
            CreateFiles(root, 100);
            using var cancellation = new CancellationTokenSource();
            int enumerated = 0;
            IEnumerable<FileSystemInfo> CancelEnumeration(DirectoryInfo directory)
            {
                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
                {
                    enumerated++;
                    if (enumerated == 10)
                    {
                        cancellation.Cancel();
                    }

                    yield return entry;
                }
            }

            var logger = new ListLogger<FileSystemStorageBrowseDriver>();
            FileSystemStorageBrowseDriver sut = CreateSut(root, CancelEnumeration, logger);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(StorageBrowseContainer.Root),
                    cancellation.Token));

            Assert.DoesNotContain(logger.Messages, message => message.Contains("completed", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logger.Messages, message => message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_ORDERING_GlobalNameOrder_IsExplicitlyUnsupported()
    {
        string root = CreateRoot();
        try
        {
            FileSystemStorageBrowseDriver sut = CreateSut(root);

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(
                        StorageBrowseContainer.Root,
                        sort: new StorageBrowseSort(
                            StorageBrowseSortField.Name,
                            StorageBrowseSortDirection.Ascending))));

            Assert.Equal(StorageBrowseErrorCode.UnsupportedOperation, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_BUDGET_InspectionLimit_ReturnsTypedPartialPageAndContinuation()
    {
        string root = CreateRoot();
        try
        {
            CreateFiles(root, 20);
            FileSystemStorageBrowseDriver sut = CreateSut(root);
            var budget = new StorageBrowseWorkBudget(
                maximumReturnedItems: 5,
                maximumInspectedItems: 5,
                maximumMetadataProbes: 0,
                maximumConcurrentMetadataProbes: 0,
                maximumDuration: TimeSpan.FromSeconds(10));

            StorageBrowsePage page = await sut.BrowseAsync(
                CreateStorage(root),
                new StorageBrowseRequest(
                    StorageBrowseContainer.Root,
                    pageSize: 5,
                    budget: budget));

            Assert.Equal(StorageBrowseCompleteness.PartialInspectionLimit, page.Completeness);
            Assert.Equal(5, page.Entries.Count);
            Assert.Equal(5, page.Metrics.InspectedItems);
            Assert.NotNull(page.NextCursor);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task SB03_INV_REDACTION_ProviderFailure_MasksRootAndRawError()
    {
        string root = CreateRoot();
        try
        {
            const string rawError = "transport-secret-detail";
            IEnumerable<FileSystemInfo> FailEnumeration(DirectoryInfo _)
            {
                throw new IOException($"{rawError}: {root}");
            }

            var logger = new ListLogger<FileSystemStorageBrowseDriver>();
            FileSystemStorageBrowseDriver sut = CreateSut(root, FailEnumeration, logger);

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(StorageBrowseContainer.Root)));

            Assert.Equal(StorageBrowseErrorCode.ProviderUnavailable, exception.Error.Code);
            Assert.DoesNotContain(root, exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawError, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(logger.Messages, message => message.Contains(root, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Messages, message => message.Contains(rawError, StringComparison.Ordinal));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static FileSystemStorageBrowseDriver CreateSut(
        string root,
        Func<DirectoryInfo, IEnumerable<FileSystemInfo>>? enumerate = null,
        ILogger<FileSystemStorageBrowseDriver>? logger = null)
    {
        var pathPolicy = new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root));
        return enumerate is null
            ? new FileSystemStorageBrowseDriver(
                pathPolicy,
                logger ?? NullLogger<FileSystemStorageBrowseDriver>.Instance)
            : new FileSystemStorageBrowseDriver(
                pathPolicy,
                new FileSystemStorageBrowseCursorCodec(),
                logger ?? NullLogger<FileSystemStorageBrowseDriver>.Instance,
                enumerate);
    }

    private static StorageCatalogRecord CreateStorage(string root)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test files",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = root
        };

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"storage-browse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CreateFiles(string root, int count)
    {
        for (int index = 0; index < count; index++)
        {
            File.WriteAllText(Path.Combine(root, $"file-{index:D6}.txt"), index.ToString());
        }
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestWorkspacePathResolver(string root) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => root;

        public string ResolveManagedFilesRoot() => Path.Combine(root, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(root, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(root, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(root, ".artifacts");
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
