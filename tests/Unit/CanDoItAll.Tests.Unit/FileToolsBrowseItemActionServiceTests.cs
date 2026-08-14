using CanDoItAll.FileTools.Desktop;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class FileToolsBrowseItemActionServiceTests
{
    [Fact]
    public async Task LaunchAsync_safe_system_associated_file_reaches_the_desktop_launcher()
    {
        using var fixture = new ActionFixture("report.xlsx");

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            fixture.ItemKey,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.True(result.IsSuccess);
        DesktopFileLaunchRequest request = Assert.IsType<DesktopFileLaunchRequest>(fixture.Launcher.LastRequest);
        Assert.Equal(DesktopFileLaunchOperation.Open, request.Operation);
        Assert.Equal(fixture.TargetPath, request.TargetPath);
        Assert.Null(request.ExecutablePath);
        Assert.Equal(FileAccessOperation.OpenLocally, fixture.Authorization.LastGrant?.Operations);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task AuthorizeDownloadAsync_keeps_authority_until_the_owned_lease_is_disposed()
    {
        using var fixture = new ActionFixture("report.xlsx");

        IFileToolsDownloadLease lease = await fixture.Sut.AuthorizeDownloadAsync(
            fixture.Scope,
            fixture.ItemKey);

        Assert.Equal("report.xlsx", lease.FileName);
        Assert.Equal(FileAccessOperation.Download, fixture.Authorization.LastGrant?.Operations);
        Assert.Equal(0, fixture.Authorization.RevokeCount);
        await using (FileContentLease content = await lease.OpenReadAsync())
        {
            using var reader = new StreamReader(content.Stream);
            Assert.Equal("download payload", await reader.ReadToEndAsync());
        }

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task AuthorizeDownloadAsync_revokes_the_grant_when_authorization_resolution_fails()
    {
        using var fixture = new ActionFixture("report.xlsx");
        fixture.Authorization.ResolveFailure = new FileAccessDeniedException(
            FileAccessFailureCode.ContextMismatch,
            "The file authorization context changed.");

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.AuthorizeDownloadAsync(fixture.Scope, fixture.ItemKey).AsTask());

        Assert.Equal(FileAccessFailureCode.ContextMismatch, exception.Code);
        Assert.Equal(1, fixture.Authorization.GrantCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task AuthorizeDownloadAsync_revokes_the_grant_when_download_is_not_supported()
    {
        using var fixture = new ActionFixture(
            "report.xlsx",
            LocalOpenRejection.SourceCapabilityUnavailable);

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.AuthorizeDownloadAsync(fixture.Scope, fixture.ItemKey).AsTask());

        Assert.Equal(FileAccessFailureCode.Unsupported, exception.Code);
        Assert.Equal(1, fixture.Authorization.GrantCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_known_file_reauthorizes_the_current_occurrence_for_local_open()
    {
        using var fixture = new ActionFixture("report.xlsx");
        var occurrence = new FileToolsKnownFileOccurrence(
            fixture.Storage.Id,
            FileToolsKnownFileOccurrenceKind.RelativePath,
            "files/report.xlsx",
            "report.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            0);

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            occurrence,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.True(result.IsSuccess);
        Assert.Equal(FileAccessOperation.OpenLocally, fixture.Authorization.LastGrant?.Operations);
        Assert.Equal(fixture.TargetPath, fixture.Launcher.LastRequest?.TargetPath);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_unsafe_extension_without_an_override_is_rejected_before_launch()
    {
        using var fixture = new ActionFixture("deploy.ps1");

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.LaunchAsync(
                fixture.Scope,
                fixture.ItemKey,
                FileToolsLocalFileAction.OpenInPreferredApplication).AsTask());

        Assert.Equal(FileAccessFailureCode.Unsupported, exception.Code);
        Assert.Equal(0, fixture.Launcher.CallCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_rejects_an_item_from_another_semantic_scope_before_granting_authority()
    {
        using var fixture = new ActionFixture("report.xlsx");
        var otherScope = CreateScope("other-node");
        FileBrowserItemKey otherItemKey = fixture.SessionFactory.CreateItemKey(otherScope);

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.LaunchAsync(
                fixture.Scope,
                otherItemKey,
                FileToolsLocalFileAction.OpenInPreferredApplication).AsTask());

        Assert.Equal(FileAccessFailureCode.InvalidHandle, exception.Code);
        Assert.Equal(0, fixture.Authorization.GrantCount);
        Assert.Equal(0, fixture.Launcher.CallCount);
    }

    [Fact]
    public async Task LaunchAsync_rejects_a_current_item_under_the_wrong_semantic_scope()
    {
        using var fixture = new ActionFixture("report.xlsx");

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.LaunchAsync(
                CreateScope("other-node"),
                fixture.ItemKey,
                FileToolsLocalFileAction.OpenInPreferredApplication).AsTask());

        Assert.Equal(FileAccessFailureCode.InvalidHandle, exception.Code);
        Assert.Equal(0, fixture.Authorization.GrantCount);
        Assert.Equal(0, fixture.Launcher.CallCount);
    }

    [Theory]
    [InlineData(LocalOpenRejection.SourceCapabilityUnavailable)]
    [InlineData(LocalOpenRejection.DriverUnavailable)]
    [InlineData(LocalOpenRejection.DriverCapabilityUnavailable)]
    [InlineData(LocalOpenRejection.UntrustedStorageRoot)]
    public async Task LaunchAsync_rejects_a_source_that_cannot_support_trusted_local_open(
        LocalOpenRejection rejection)
    {
        using var fixture = new ActionFixture("report.xlsx", rejection);

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.LaunchAsync(
                fixture.Scope,
                fixture.ItemKey,
                FileToolsLocalFileAction.OpenInPreferredApplication).AsTask());

        Assert.Equal(FileAccessFailureCode.Unsupported, exception.Code);
        Assert.Equal(0, fixture.Launcher.CallCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_passes_an_explicit_override_to_the_desktop_launcher()
    {
        using var fixture = new ActionFixture("report.xlsx");
        string executablePath = fixture.CreateFile("applications", "spreadsheet-viewer.exe");
        fixture.Preferences.Preference = new FileApplicationPreference(
            new FileApplicationExtension(".xlsx"),
            executablePath);

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            fixture.ItemKey,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.True(result.IsSuccess);
        Assert.Equal(executablePath, fixture.Launcher.LastRequest?.ExecutablePath);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_maps_invalid_preference_settings_without_falling_back_to_the_system_association()
    {
        using var fixture = new ActionFixture("report.xlsx");
        fixture.Preferences.ResolveFailure = new InvalidOperationException(
            "The preferred application settings are invalid.");

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            fixture.ItemKey,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            FileToolsBrowseItemActionFailureCode.PreferredApplicationUnavailable,
            result.FailureCode);
        Assert.Equal(0, fixture.Launcher.CallCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_maps_a_missing_configured_executable_to_a_distinct_failure()
    {
        using var fixture = new ActionFixture("report.xlsx");
        string missingExecutablePath = fixture.ResolvePath("applications", "missing-viewer.exe");
        fixture.Preferences.Preference = new FileApplicationPreference(
            new FileApplicationExtension("xlsx"),
            missingExecutablePath);
        fixture.Launcher.ResultFactory = request => request.ExecutablePath is not null && !File.Exists(request.ExecutablePath)
            ? DesktopFileLaunchResult.Failed(
                DesktopFileLaunchFailureCode.ApplicationNotFound,
                "The configured application does not exist.")
            : DesktopFileLaunchResult.Success(request.TargetPath);

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            fixture.ItemKey,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            FileToolsBrowseItemActionFailureCode.PreferredApplicationUnavailable,
            result.FailureCode);
        Assert.Equal(missingExecutablePath, fixture.Launcher.LastRequest?.ExecutablePath);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Theory]
    [InlineData(
        DesktopFileLaunchFailureCode.TargetNotFound,
        FileToolsBrowseItemActionFailureCode.TargetUnavailable)]
    [InlineData(
        DesktopFileLaunchFailureCode.ProcessStartFailed,
        FileToolsBrowseItemActionFailureCode.LaunchFailed)]
    public async Task LaunchAsync_revokes_authorization_for_each_desktop_failure(
        DesktopFileLaunchFailureCode desktopFailure,
        FileToolsBrowseItemActionFailureCode expectedFailure)
    {
        using var fixture = new ActionFixture("report.xlsx");
        fixture.Launcher.ResultFactory = _ => DesktopFileLaunchResult.Failed(
            desktopFailure,
            "Desktop launch failed.");

        FileToolsBrowseItemActionResult result = await fixture.Sut.LaunchAsync(
            fixture.Scope,
            fixture.ItemKey,
            FileToolsLocalFileAction.OpenInPreferredApplication);

        Assert.Equal(expectedFailure, result.FailureCode);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
    }

    [Fact]
    public async Task LaunchAsync_revokes_a_granted_handle_when_authorization_resolution_fails()
    {
        using var fixture = new ActionFixture("report.xlsx");
        fixture.Authorization.ResolveFailure = new FileAccessDeniedException(
            FileAccessFailureCode.ContextMismatch,
            "The file authorization context changed.");

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            fixture.Sut.LaunchAsync(
                fixture.Scope,
                fixture.ItemKey,
                FileToolsLocalFileAction.OpenInPreferredApplication).AsTask());

        Assert.Equal(FileAccessFailureCode.ContextMismatch, exception.Code);
        Assert.Equal(1, fixture.Authorization.GrantCount);
        Assert.Equal(1, fixture.Authorization.RevokeCount);
        Assert.Equal(0, fixture.Launcher.CallCount);
    }

    private static FileToolsSemanticScope CreateScope(string id = "node-1")
        => new(
            FileToolsSemanticScopeKind.ProjectNode,
            new FileToolsSemanticScopeId(id),
            $"Files for {id}");

    public enum LocalOpenRejection
    {
        None,
        SourceCapabilityUnavailable,
        DriverUnavailable,
        DriverCapabilityUnavailable,
        UntrustedStorageRoot
    }

    private sealed class ActionFixture : IDisposable
    {
        private const string Container = "files";
        private readonly string rootPath = Path.Combine(
            Path.GetTempPath(),
            nameof(FileToolsBrowseItemActionServiceTests),
            Guid.NewGuid().ToString("N"));

        public ActionFixture(
            string fileName,
            LocalOpenRejection rejection = LocalOpenRejection.None)
        {
            Scope = CreateScope();
            string workspaceRoot = ResolvePath("workspace");
            string storageRoot = rejection == LocalOpenRejection.UntrustedStorageRoot
                ? ResolvePath("untrusted-storage")
                : ResolvePath("workspace", "storage");
            Directory.CreateDirectory(storageRoot);
            TargetPath = CreateFileAt(storageRoot, Container, fileName);
            Storage = new StorageCatalogRecord
            {
                Id = Guid.NewGuid(),
                Name = "Test files",
                ProviderKind = StorageProviderKind.FileSystem,
                EndpointOrRoot = storageRoot,
                IsEnabled = true,
                CapabilityMask = rejection == LocalOpenRejection.SourceCapabilityUnavailable
                    ? StorageCapability.Read
                    : StorageCapability.Read |
                      StorageCapability.Download |
                       StorageCapability.OpenLocally
            };
            StorageCatalogHostBindingPolicy.BindCurrent(Storage, storageRoot, DateTimeOffset.UtcNow);
            var browseDriver = new StaticBrowseDriver(
                Storage.ProviderKind,
                $"{Container}/{fileName}",
                fileName);
            SessionFactory = new ScopedBrowseSessionFactory(Storage, browseDriver, Container);
            var storageDriver = new StaticStorageDriver(
                rejection == LocalOpenRejection.DriverCapabilityUnavailable
                    ? StorageCapability.Read
                    : StorageCapability.Read |
                      StorageCapability.Download |
                      StorageCapability.OpenLocally);
            var storageDrivers = new StaticStorageDriverRegistry(
                storageDriver,
                isAvailable: rejection != LocalOpenRejection.DriverUnavailable);
            var contextProvider = new StaticFileAccessContextProvider();
            Authorization = new RecordingAuthorizationCoordinator(Storage);
            Preferences = new StaticFileApplicationPreferenceService();
            Launcher = new RecordingDesktopFileLauncher();
            var pathPolicy = new FileSystemStoragePathPolicy(
                new StaticWorkspacePathResolver(workspaceRoot));
            var contentSource = new AuthorizedFileContentSource(
                Authorization,
                contextProvider,
                storageDrivers,
                Options.Create(new FileAccessHandleOptions()),
                NullLogger<AuthorizedFileContentSource>.Instance);
            Sut = new FileToolsBrowseItemActionService(
                new StorageFileToolsBrowseItemResolver(SessionFactory),
                new StorageFileToolsKnownFileResolver(new StaticStorageCatalogService(Storage)),
                contextProvider,
                Authorization,
                storageDrivers,
                pathPolicy,
                Preferences,
                contentSource,
                Launcher,
                NullLogger<FileToolsBrowseItemActionService>.Instance);
            ItemKey = SessionFactory.CreateItemKey(Scope);
        }

        public FileToolsSemanticScope Scope { get; }

        public StorageCatalogRecord Storage { get; }

        public ScopedBrowseSessionFactory SessionFactory { get; }

        public RecordingAuthorizationCoordinator Authorization { get; }

        public StaticFileApplicationPreferenceService Preferences { get; }

        public RecordingDesktopFileLauncher Launcher { get; }

        public FileToolsBrowseItemActionService Sut { get; }

        public FileBrowserItemKey ItemKey { get; }

        public string TargetPath { get; }

        public string CreateFile(params string[] segments)
            => CreateFileAt(rootPath, segments);

        public string ResolvePath(params string[] segments)
            => Path.GetFullPath(Path.Combine([rootPath, .. segments]));

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }

        private static string CreateFileAt(string basePath, params string[] segments)
        {
            string path = Path.GetFullPath(Path.Combine([basePath, .. segments]));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }

    private sealed class ScopedBrowseSessionFactory(
        StorageCatalogRecord storage,
        StaticBrowseDriver browseDriver,
        string container) : IFileToolsBrowseSessionFactory
    {
        public ValueTask<FileToolsBrowseSession> CreateAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorageFileBrowserProvider provider = CreateProvider(scope);
            return ValueTask.FromResult(new FileToolsBrowseSession(
                scope,
                [provider],
                new FileBrowserSortDescriptor(
                    FileBrowserSortField.ProviderNative,
                    FileBrowserSortDirection.Ascending,
                    FoldersFirst: false),
                new FileToolsBrowseSessionRevision("test-revision")));
        }

        public FileBrowserItemKey CreateItemKey(FileToolsSemanticScope scope)
        {
            StorageFileBrowserProvider provider = CreateProvider(scope);
            return new StorageFileBrowserKeyCodec(provider.Descriptor.Id)
                .EncodeItem(container, $"{container}/{browseDriver.FileName}", revision: null);
        }

        private StorageFileBrowserProvider CreateProvider(FileToolsSemanticScope scope)
            => new(
                scope,
                new FileToolsStorageBinding(
                    storage.Id,
                    "Test files",
                    new FileToolsBrowseWorkLimits(),
                    new FileToolsStorageRoot(container)),
                storage,
                browseDriver);
    }

    private sealed class StaticBrowseDriver(
        StorageProviderKind providerKind,
        string entryId,
        string fileName) : IStorageBrowseDriver, IStorageBrowseStatDriver
    {
        public string FileName => fileName;

        public StorageProviderKind ProviderKind => providerKind;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.Stat |
            StorageBrowseCapability.ProviderNativeOrdering;

        public StorageBrowseWorkBudget MaximumBudget { get; } = new(
            maximumReturnedItems: 10,
            maximumInspectedItems: 20,
            maximumMetadataProbes: 10,
            maximumConcurrentMetadataProbes: 1,
            maximumDuration: TimeSpan.FromSeconds(2));

        public Task<StorageBrowsePage> BrowseAsync(
            StorageCatalogRecord storage,
            StorageBrowseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageBrowseEntry> StatAsync(
            StorageCatalogRecord storage,
            StorageBrowseStatRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StorageBrowseEntry(
                new StorageBrowseEntryId(entryId),
                request.Container,
                fileName,
                entryId,
                StorageBrowseEntryKind.File,
                StorageBrowseEntryCapability.Read,
                size: 0,
                mediaType: "application/octet-stream"));
    }

    private sealed class StaticStorageDriver(StorageCapability supportedCapabilities) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageCapability SupportedCapabilities => supportedCapabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream("download payload"u8.ToArray()));

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StaticStorageCatalogService(StorageCatalogRecord storage)
        : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        public Task<StorageCatalogRecord?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(id == storage.Id ? storage : null);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(storage);

        public Task<StorageCatalogRecord> SaveAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }

    private sealed class StaticStorageDriverRegistry(
        IStorageDriver driver,
        bool isAvailable) : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => isAvailable
            ? [driver.ProviderKind]
            : [];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver resolved)
        {
            resolved = driver;
            return isAvailable && providerKind == driver.ProviderKind;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => TryResolve(providerKind, out IStorageDriver resolved)
                ? resolved
                : throw new InvalidOperationException("The test storage driver is unavailable.");
    }

    private sealed class StaticFileAccessContextProvider : IFileAccessContextProvider
    {
        private readonly FileAccessContext context = new(
            new FileAccessActorId("test-actor"),
            new FileAccessSessionId("test-session"),
            Guid.NewGuid(),
            runtimeGeneration: 1,
            authorizationRevision: 1,
            new FileAccessCorrelationId("test-correlation"));

        public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(context);
        }
    }

    private sealed class RecordingAuthorizationCoordinator(StorageCatalogRecord storage)
        : IStorageFileAccessAuthorizationCoordinator
    {
        private readonly Dictionary<FileReference, (FileAccessGrantRequest Request, StorageObjectReference Reference)> grants = [];

        public int GrantCount { get; private set; }

        public int RevokeCount { get; private set; }

        public FileAccessGrantRequest? LastGrant { get; private set; }

        public FileAccessDeniedException? ResolveFailure { get; set; }

        public ValueTask<FileReference> GrantAsync(
            FileAccessGrantRequest request,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GrantCount++;
            LastGrant = request;
            var file = new FileReference(
                AuthorizedFileReference.SourceId,
                $"test-handle-{GrantCount}");
            grants.Add(file, (request, reference));
            return ValueTask.FromResult(file);
        }

        public ValueTask<AuthorizedStorageFile> ResolveAsync(
            FileReference file,
            FileAccessContext context,
            FileAccessOperation operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ResolveFailure is not null)
            {
                return ValueTask.FromException<AuthorizedStorageFile>(ResolveFailure);
            }

            if (!grants.TryGetValue(file, out var grant) || !grant.Request.Operations.HasFlag(operation))
            {
                return ValueTask.FromException<AuthorizedStorageFile>(new FileAccessDeniedException(
                    FileAccessFailureCode.InvalidHandle,
                    "The test file authorization is unavailable."));
            }

            return ValueTask.FromResult(new AuthorizedStorageFile(
                storage,
                grant.Reference,
                grant.Request.Scope,
                grant.Request.Operations,
                grant.Request.ExpectedRevision));
        }

        public ValueTask RevokeAsync(
            FileReference file,
            CancellationToken cancellationToken = default)
        {
            RevokeCount++;
            grants.Remove(file);
            return ValueTask.CompletedTask;
        }

        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default)
        {
            grants.Clear();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticFileApplicationPreferenceService : IFileApplicationPreferenceService
    {
        public FileApplicationPreference? Preference { get; set; }

        public Exception? ResolveFailure { get; set; }

        public Task<IReadOnlyList<FileApplicationPreference>> ListAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FileApplicationPreference>>(
                Preference is null ? [] : [Preference]);

        public Task SaveAsync(
            FileApplicationPreference preference,
            CancellationToken cancellationToken = default)
        {
            Preference = preference;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(
            FileApplicationExtension extension,
            CancellationToken cancellationToken = default)
        {
            bool removed = Preference?.Extension == extension;
            if (removed)
            {
                Preference = null;
            }

            return Task.FromResult(removed);
        }

        public Task<bool> RollbackPathMigrationAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public FileApplicationPreference? ResolveForFile(string fileName)
        {
            if (ResolveFailure is not null)
            {
                throw ResolveFailure;
            }

            string extension = Path.GetExtension(fileName);
            return Preference is not null &&
                   Preference.Extension == new FileApplicationExtension(extension)
                ? Preference
                : null;
        }
    }

    private sealed class RecordingDesktopFileLauncher : IDesktopFileLauncher
    {
        public bool IsAvailable => true;

        public int CallCount { get; private set; }

        public DesktopFileLaunchRequest? LastRequest { get; private set; }

        public Func<DesktopFileLaunchRequest, DesktopFileLaunchResult> ResultFactory { get; set; }
            = request => DesktopFileLaunchResult.Success(request.TargetPath);

        public ValueTask<DesktopFileLaunchResult> LaunchAsync(
            DesktopFileLaunchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return ValueTask.FromResult(ResultFactory(request));
        }
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
