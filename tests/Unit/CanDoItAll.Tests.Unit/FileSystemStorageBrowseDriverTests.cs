using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class FileSystemStorageBrowseDriverTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Save_allocates_distinct_names_for_generated_name_and_occupied_suffix_collisions()
    {
        string root = CreateRoot();
        try
        {
            var driver = new FileSystemStorageDriver(
                new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root)));
            StorageCatalogRecord storage = CreateStorage(root);
            StorageWriteResult first = await SaveAsync(driver, storage, "report:final.txt", "first");
            StorageWriteResult second = await SaveAsync(
                driver,
                storage,
                first.Reference.Locator,
                "second");
            StorageWriteResult third = await SaveAsync(
                driver,
                storage,
                first.Reference.Locator,
                "third");

            Assert.Equal(3, new[]
            {
                first.Reference.Locator,
                second.Reference.Locator,
                third.Reference.Locator
            }.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal("first", await ReadAsync(driver, storage, first.Reference));
            Assert.Equal("second", await ReadAsync(driver, storage, second.Reference));
            Assert.Equal("third", await ReadAsync(driver, storage, third.Reference));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Concurrent_auto_allocation_never_overwrites_the_committed_winner()
    {
        string root = CreateRoot();
        using var firstTemporaryFlushed = new ManualResetEventSlim();
        using var allowFirstCommit = new ManualResetEventSlim();
        var firstObserverEntry = 0;
        try
        {
            var durableWriter = new DurableFileWriter(
                new PhysicalFileSystemPathPolicyFactory(),
                stage =>
                {
                    if (stage == DurableFileWriteStage.TemporaryFileFlushed &&
                        Interlocked.CompareExchange(ref firstObserverEntry, 1, 0) == 0)
                    {
                        firstTemporaryFlushed.Set();
                        allowFirstCommit.Wait(TimeSpan.FromSeconds(10));
                    }
                });
            var driver = new FileSystemStorageDriver(
                new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root)),
                durableWriter);
            StorageCatalogRecord storage = CreateStorage(root);
            Task<StorageWriteResult> first = Task.Run(
                () => SaveAsync(driver, storage, "report.txt", "first"));
            Assert.True(firstTemporaryFlushed.Wait(TimeSpan.FromSeconds(10)));
            Task<StorageWriteResult> competing = SaveAsync(
                driver,
                storage,
                "report.txt",
                "competing");

            allowFirstCommit.Set();
            StorageWriteResult winner = await first;
            IOException conflict = await Assert.ThrowsAsync<IOException>(() => competing);

            Assert.Contains("became occupied", conflict.Message, StringComparison.Ordinal);
            Assert.Equal("first", await ReadAsync(driver, storage, winner.Reference));
        }
        finally
        {
            allowFirstCommit.Set();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Auto_allocation_never_overwrites_target_created_after_precommit_guard()
    {
        string root = CreateRoot();
        string targetPath = Path.Combine(root, "report.txt");
        var injectedOccupancy = 0;
        try
        {
            var durableWriter = new DurableFileWriter(
                new PhysicalFileSystemPathPolicyFactory(),
                stage =>
                {
                    if (stage == DurableFileWriteStage.BeforeCommit &&
                        Interlocked.CompareExchange(ref injectedOccupancy, 1, 0) == 0)
                    {
                        File.WriteAllText(targetPath, "outside-winner");
                    }
                });
            var driver = new FileSystemStorageDriver(
                new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root)),
                durableWriter);
            StorageCatalogRecord storage = CreateStorage(root);

            await Assert.ThrowsAsync<IOException>(
                () => SaveAsync(driver, storage, "report.txt", "losing-allocation"));

            Assert.Equal("outside-winner", await File.ReadAllTextAsync(targetPath));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Unix_backslash_filename_write_preserves_the_physical_segment_and_opaque_locator()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateRoot();
        try
        {
            const string physicalName = "report\\final.txt";
            string encodedName = Uri.EscapeDataString(physicalName);
            var pathPolicy = new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root));
            var driver = new FileSystemStorageDriver(pathPolicy);
            StorageCatalogRecord storage = CreateStorage(root);

            StorageWriteResult result = await driver.SaveAsync(
                storage,
                new StorageWriteRequest(
                    physicalName,
                    "text/plain",
                    "content"u8.ToArray(),
                    StorageUsagePurpose.ProjectAsset,
                    RelativePathHint: encodedName));

            Assert.Equal(encodedName, result.Reference.Locator, ignoreCase: true);
            Assert.Equal("content", await File.ReadAllTextAsync(Path.Combine(root, physicalName)));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Unix_backslash_filename_round_trips_through_an_opaque_browse_key()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateRoot();
        try
        {
            const string physicalName = "report\\final.txt";
            string physicalPath = Path.Combine(root, physicalName);
            await File.WriteAllTextAsync(physicalPath, "content");
            FileSystemStorageBrowseDriver sut = CreateSut(root);
            StorageCatalogRecord storage = CreateStorage(root);

            StorageBrowsePage page = await sut.BrowseAsync(
                storage,
                new StorageBrowseRequest(StorageBrowseContainer.Root));

            StorageBrowseEntry entry = Assert.Single(page.Entries);
            Assert.Equal(physicalName, entry.Name);
            Assert.Contains("%5C", entry.Id.Value, StringComparison.Ordinal);
            var pathPolicy = new FileSystemStoragePathPolicy(new TestWorkspacePathResolver(root));
            Assert.Equal(physicalPath, pathPolicy.ResolveFullPath(storage, entry.Id.Value));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Incomplete_snapshot_fails_before_publishing_nondeterministic_page()
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

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(StorageBrowseContainer.Root, pageSize: 25, budget: budget)));

            Assert.Equal(StorageBrowseErrorCode.BudgetExceeded, exception.Error.Code);
            Assert.Equal(65, enumeratedEntries);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    [Trait("Category", "Scale")]
    public async Task Large_deterministic_snapshot_pages_stay_within_declared_provider_budget()
    {
        string root = CreateRoot();
        try
        {
            const int fixtureSize = 10_000;
            CreateFiles(root, fixtureSize);
            FileSystemStorageBrowseDriver sut = CreateSut(root);
            StorageCatalogRecord storage = CreateStorage(root);
            var budget = new StorageBrowseWorkBudget(
                maximumReturnedItems: 50,
                maximumInspectedItems: fixtureSize,
                maximumMetadataProbes: 0,
                maximumConcurrentMetadataProbes: 0,
                maximumDuration: TimeSpan.FromMinutes(2));
            var request = new StorageBrowseRequest(
                StorageBrowseContainer.Root,
                pageSize: 50,
                budget: budget);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            StorageBrowsePage acceptedFirst = await sut.BrowseAsync(storage, request);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.Equal(50, acceptedFirst.Entries.Count);
            Assert.Equal(fixtureSize, acceptedFirst.Metrics.InspectedItems);
            Assert.Equal(0, acceptedFirst.Metrics.MetadataProbes);
            Assert.InRange(acceptedFirst.Metrics.RetainedStateBytes, 1, 512);
            Assert.InRange(allocated, 1, 32 * 1024 * 1024);

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
                "Large-directory pagination runtime={0} os={1} fixture={2} elapsed-ms={3:F3} allocated={4} first-inspected={5} second-inspected={6} retained={7}",
                Environment.Version,
                Environment.OSVersion.VersionString,
                fixtureSize,
                acceptedFirst.Metrics.Duration.TotalMilliseconds,
                allocated,
                acceptedFirst.Metrics.InspectedItems,
                second.Metrics.InspectedItems,
                acceptedFirst.Metrics.RetainedStateBytes);
            Assert.Equal(50, second.Entries.Count);
            Assert.Equal(fixtureSize, second.Metrics.InspectedItems);
            Assert.Empty(firstIds.Intersect(secondIds, StringComparer.Ordinal));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Traversal_container_is_rejected_without_root_disclosure()
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
    public async Task Reparse_container_is_rejected()
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
    public async Task Directory_mutation_invalidates_continuation()
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
    public async Task Replaced_content_and_metadata_are_visible_without_browser_state()
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
    public async Task Revisioned_replace_rejects_stale_revision_and_persists_only_on_success()
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
    public async Task Cancelled_enumeration_does_not_publish_success()
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
    public async Task Global_name_order_is_deterministic_across_different_provider_sequences()
    {
        string root = CreateRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "zeta.txt"), "z");
            File.WriteAllText(Path.Combine(root, "alpha.txt"), "a");
            File.WriteAllText(Path.Combine(root, "beta.txt"), "b");
            var reverse = false;
            IEnumerable<FileSystemInfo> ReorderedEnumeration(DirectoryInfo directory)
            {
                FileSystemInfo[] entries = directory.EnumerateFileSystemInfos().ToArray();
                reverse = !reverse;
                return reverse ? entries.Reverse() : entries;
            }

            FileSystemStorageBrowseDriver sut = CreateSut(root, ReorderedEnumeration);
            var request = new StorageBrowseRequest(
                StorageBrowseContainer.Root,
                sort: new StorageBrowseSort(
                    StorageBrowseSortField.Name,
                    StorageBrowseSortDirection.Ascending));

            StorageBrowsePage first = await sut.BrowseAsync(CreateStorage(root), request);
            StorageBrowsePage second = await sut.BrowseAsync(CreateStorage(root), request);

            Assert.Equal(["alpha.txt", "beta.txt", "zeta.txt"], first.Entries.Select(entry => entry.Name));
            Assert.Equal(first.Entries.Select(entry => entry.Id), second.Entries.Select(entry => entry.Id));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Inspection_limit_returns_typed_budget_error_without_partial_order()
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

            StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
                sut.BrowseAsync(
                    CreateStorage(root),
                    new StorageBrowseRequest(
                        StorageBrowseContainer.Root,
                        pageSize: 5,
                        budget: budget)));

            Assert.Equal(StorageBrowseErrorCode.BudgetExceeded, exception.Error.Code);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Provider_failure_masks_root_and_raw_error()
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
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "Test files",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = root
        };
        StorageCatalogHostBindingPolicy.BindCurrent(storage, root, DateTimeOffset.UtcNow);
        return storage;
    }

    private static Task<StorageWriteResult> SaveAsync(
        FileSystemStorageDriver driver,
        StorageCatalogRecord storage,
        string fileName,
        string content)
        => driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                fileName,
                "text/plain",
                System.Text.Encoding.UTF8.GetBytes(content),
                StorageUsagePurpose.ProjectAsset));

    private static async Task<string> ReadAsync(
        FileSystemStorageDriver driver,
        StorageCatalogRecord storage,
        StorageObjectReference reference)
    {
        await using Stream stream = await driver.OpenReadAsync(storage, reference);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

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
