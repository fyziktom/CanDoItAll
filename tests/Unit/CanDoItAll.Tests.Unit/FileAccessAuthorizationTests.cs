using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class FileAccessAuthorizationTests
{
    [Fact]
    public async Task HandleResolve_rechecks_current_semantic_binding()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View);

        fixture.BindingProvider.IsAvailable = false;

        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.Forbidden);
        Assert.Equal(0, fixture.Driver.EffectCalls);
    }

    [Fact]
    public async Task Browser_activation_reresolves_current_occurrence_before_grant()
    {
        await using TestFixture fixture = CreateFixture();
        var browseDriver = new FakeBrowseDriver();
        var provider = new StorageFileBrowserProvider(
            fixture.Scope,
            new FileToolsStorageBinding(
                fixture.Storage.Id,
                "Files",
                new FileToolsBrowseWorkLimits(
                    maximumReturnedItems: 10,
                    maximumInspectedItems: 20,
                    maximumMetadataProbes: 10,
                    maximumConcurrentMetadataProbes: 1,
                    maximumDuration: TimeSpan.FromSeconds(2))),
            fixture.Storage,
            browseDriver);
        FileBrowserItem root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        FileBrowserPage page = await provider.BrowseAsync(new FileBrowserBrowseRequest(
            root.Key,
            pageSize: 5,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FoldersFirst: false)));
        FileBrowserItem currentItem = Assert.Single(page.Items);

        AuthorizedBrowserFile authorized = await provider.AuthorizeItemAsync(
            currentItem.Key,
            fixture.ContextProvider.Current,
            fixture.Scope,
            FileAccessOperation.View,
            fixture.Coordinator);
        FileReference file = authorized.File;

        Assert.Equal(AuthorizedFileReference.SourceId, file.SourceId);
        Assert.Equal("readme.md", authorized.FileName);
        Assert.Equal("text/plain", authorized.MediaType);
        Assert.Equal(15, authorized.Size);
        Assert.Equal(1, browseDriver.StatCallCount);
        browseDriver.EntryExists = false;
        fixture.Driver.ResetEffectCalls();
        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            provider.AuthorizeItemAsync(
                currentItem.Key,
                fixture.ContextProvider.Current,
                fixture.Scope,
                FileAccessOperation.View,
                fixture.Coordinator).AsTask());
        Assert.Equal(FileAccessFailureCode.SourceUnavailable, exception.Code);
        Assert.Equal(0, fixture.Driver.EffectCalls);
    }

    [Fact]
    public async Task Browser_activation_rejects_locator_outside_semantic_root()
    {
        await using TestFixture fixture = CreateFixture();
        var browseDriver = new FakeBrowseDriver { EntryId = "secret.txt" };
        var provider = new StorageFileBrowserProvider(
            fixture.Scope,
            new FileToolsStorageBinding(
                fixture.Storage.Id,
                "Project files",
                new FileToolsBrowseWorkLimits(
                    maximumReturnedItems: 10,
                    maximumInspectedItems: 20,
                    maximumMetadataProbes: 10,
                    maximumConcurrentMetadataProbes: 1,
                    maximumDuration: TimeSpan.FromSeconds(2)),
                new FileToolsStorageRoot("allowed")),
            fixture.Storage,
            browseDriver);
        FileBrowserItem root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        FileBrowserPage page = await provider.BrowseAsync(new FileBrowserBrowseRequest(
            root.Key,
            pageSize: 5,
            sort: new FileBrowserSortDescriptor(
                FileBrowserSortField.ProviderNative,
                FoldersFirst: false)));
        fixture.Driver.ResetEffectCalls();

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            provider.AuthorizeItemAsync(
                Assert.Single(page.Items).Key,
                fixture.ContextProvider.Current,
                fixture.Scope,
                FileAccessOperation.View,
                fixture.Coordinator).AsTask());

        Assert.Equal(FileAccessFailureCode.SourceUnavailable, exception.Code);
        Assert.Equal(0, fixture.Driver.EffectCalls);
    }

    [Fact]
    public async Task Forged_cross_context_wrong_operation_revoked_and_expired_handles_fail_before_storage()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View);
        fixture.Driver.ResetEffectCalls();

        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                new FileReference(file.SourceId, file.Value[..^1] + (file.Value[^1] == 'A' ? "B" : "A")),
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.InvalidHandle);
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                CopyContext(fixture.ContextProvider.Current, actorId: new FileAccessActorId("other-actor")),
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.ContextMismatch);
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                CopyContext(fixture.ContextProvider.Current, runtimeProfileId: Guid.NewGuid()),
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.ContextMismatch);
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                CopyContext(fixture.ContextProvider.Current, sessionId: new FileAccessSessionId("other-session")),
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.ContextMismatch);
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                fixture.ContextProvider.Current,
                FileAccessOperation.Edit).AsTask(),
            FileAccessFailureCode.OperationDenied);
        await fixture.Coordinator.RevokeAsync(file);
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.Revoked);

        FileReference expiring = await fixture.GrantAsync(FileAccessOperation.View);
        fixture.Time.Advance(TimeSpan.FromSeconds(11));
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                expiring,
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.Expired);
        Assert.Equal(0, fixture.Driver.EffectCalls);
    }

    [Fact]
    public async Task Source_removal_and_authorization_revision_change_fail_before_storage()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View);
        fixture.Driver.ResetEffectCalls();

        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                CopyContext(fixture.ContextProvider.Current, authorizationRevision: 1),
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.ContextMismatch);
        fixture.Catalog.IsAvailable = false;
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                file,
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.SourceUnavailable);
        Assert.Equal(0, fixture.Driver.EffectCalls);
    }

    [Fact]
    public async Task Handle_registry_evicts_oldest_deterministically_at_capacity()
    {
        await using TestFixture fixture = CreateFixture();
        var handles = new List<FileReference>();
        for (var index = 0; index < 17; index++)
        {
            handles.Add(await fixture.GrantAsync(FileAccessOperation.View));
            fixture.Time.Advance(TimeSpan.FromMilliseconds(1));
        }

        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                handles[0],
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.InvalidHandle);
        AuthorizedStorageFile newest = await fixture.Coordinator.ResolveAsync(
            handles[^1],
            fixture.ContextProvider.Current,
            FileAccessOperation.View);
        Assert.Equal("readme.md", newest.Reference.Locator);
        await fixture.Coordinator.RevokeAllAsync();
        await AssertDeniedAsync(
            () => fixture.Coordinator.ResolveAsync(
                handles[^1],
                fixture.ContextProvider.Current,
                FileAccessOperation.View).AsTask(),
            FileAccessFailureCode.InvalidHandle);
    }

    [Fact]
    public async Task Known_file_content_opens_without_any_browser_dependency()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();

        FileToolsKnownFileSession session = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.ReadOnly));
        await using FileContentLease lease = await session.ContentSource.OpenReadAsync(new FileContentReadRequest(file));
        using var reader = new StreamReader(lease.Stream);

        Assert.Equal("initial-content", await reader.ReadToEndAsync());
        Assert.Null(session.SaveTarget);
        Assert.Equal(1, fixture.Driver.OpenReadCalls);
        Assert.False(serviceScope.ServiceProvider.GetService<IStorageBrowseDriver>() is not null);
        string log = Assert.Single(fixture.Logs.Messages);
        Assert.Contains("correlation-a", log, StringComparison.Ordinal);
        Assert.DoesNotContain(file.Value, log, StringComparison.Ordinal);
        Assert.DoesNotContain("actor-a", log, StringComparison.Ordinal);
        Assert.DoesNotContain("readme.md", log, StringComparison.Ordinal);
        Assert.DoesNotContain("initial-content", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Known_occurrence_activation_grants_without_browser_or_content_calls()
    {
        await using TestFixture fixture = CreateFixture();
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileActivator activator = serviceScope.ServiceProvider
            .GetRequiredService<IFileToolsKnownFileActivator>();
        fixture.Driver.ResetEffectCalls();

        FileToolsKnownFileActivation activation = await activator.ActivateAsync(
            fixture.Scope,
            new FileToolsKnownFileOccurrence(
                fixture.Storage.Id,
                FileToolsKnownFileOccurrenceKind.RelativePath,
                "readme.md",
                "readme.md",
                "text/plain",
                15),
            FileToolsKnownFileIntent.ReadOnly);

        Assert.Equal(AuthorizedFileReference.SourceId, activation.Request.File.SourceId);
        Assert.Equal("readme.md", activation.FileName);
        Assert.Equal(0, fixture.Driver.EffectCalls);
        Assert.Null(serviceScope.ServiceProvider.GetService<IStorageBrowseDriver>());
    }

    [Theory]
    [InlineData(FakeSaveOutcome.Conflict, FileSaveOperationStatus.Conflict)]
    [InlineData(FakeSaveOutcome.Failure, FileSaveOperationStatus.Failed)]
    public async Task Save_conflict_failure_and_cancellation_keep_dirty_revision(
        FakeSaveOutcome outcome,
        FileSaveOperationStatus expectedStatus)
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        FileToolsKnownFileSession interaction = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.Edit));
        Assert.NotNull(interaction.SaveTarget);
        fixture.Driver.SaveOutcome = outcome;
        var session = new FileEditSession(
            new FileEditSnapshot(file, 0, "initial-content"u8.ToArray(), "text/plain"),
            new FileContentRevision("r1"));
        await using var saves = new FileSaveCoordinator(session, interaction.SaveTarget!);
        saves.ApplyEdit("changed"u8.ToArray(), changedTextUnits: 7);

        FileSaveOperationResult result = await saves.SaveNowAsync();

        Assert.Equal(expectedStatus, result.Status);
        Assert.True(saves.State.IsDirty);
        Assert.Equal(0, saves.State.SavedEditRevision);
        Assert.Equal("r1", saves.State.BaseRevision?.Value);
        Assert.Equal(new FileCatalogRevision(0, 0), fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    [Fact]
    public async Task Save_cancellation_keeps_dirty_revision()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        FileToolsKnownFileSession interaction = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.Edit));
        fixture.Driver.SaveOutcome = FakeSaveOutcome.Cancelled;
        var session = new FileEditSession(
            new FileEditSnapshot(file, 0, "initial-content"u8.ToArray(), "text/plain"),
            new FileContentRevision("r1"));
        var saves = new FileSaveCoordinator(session, interaction.SaveTarget!);
        saves.ApplyEdit("changed"u8.ToArray(), changedTextUnits: 7);

        Task<FileSaveOperationResult> saveTask = saves.SaveNowAsync().AsTask();
        await fixture.Driver.ReplaceStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await saves.DisposeAsync();
        FileSaveOperationResult result = await saveTask;

        Assert.Equal(FileSaveOperationStatus.Cancelled, result.Status);
        Assert.True(saves.State.IsDirty);
        Assert.Equal(0, saves.State.SavedEditRevision);
        Assert.Equal("r1", saves.State.BaseRevision?.Value);
        Assert.Equal(new FileCatalogRevision(0, 0), fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    [Fact]
    public async Task Successful_save_updates_persisted_revision_after_write()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        FileToolsKnownFileSession interaction = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.Edit));
        var session = new FileEditSession(
            new FileEditSnapshot(file, 0, "initial-content"u8.ToArray(), "text/plain"),
            new FileContentRevision("r1"));
        await using var saves = new FileSaveCoordinator(session, interaction.SaveTarget!);
        saves.ApplyEdit("changed"u8.ToArray(), changedTextUnits: 7);

        FileSaveOperationResult result = await saves.SaveNowAsync();

        Assert.Equal(FileSaveOperationStatus.Saved, result.Status);
        Assert.False(saves.State.IsDirty);
        Assert.Equal("r2", saves.State.BaseRevision?.Value);
        Assert.Equal("changed", System.Text.Encoding.UTF8.GetString(fixture.Driver.Content));
        Assert.Equal(new FileCatalogRevision(0, 1), fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    [Fact]
    public async Task Sequential_saves_accept_persisted_revision_beyond_grant_snapshot()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        FileToolsKnownFileSession interaction = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.Edit));
        var session = new FileEditSession(
            new FileEditSnapshot(file, 0, "initial-content"u8.ToArray(), "text/plain"),
            new FileContentRevision("r1"));
        await using var saves = new FileSaveCoordinator(session, interaction.SaveTarget!);
        saves.ApplyEdit("first"u8.ToArray(), changedTextUnits: 5);

        FileSaveOperationResult first = await saves.SaveNowAsync();
        saves.ApplyEdit("second"u8.ToArray(), changedTextUnits: 6);
        FileSaveOperationResult second = await saves.SaveNowAsync();

        Assert.Equal(FileSaveOperationStatus.Saved, first.Status);
        Assert.Equal(FileSaveOperationStatus.Saved, second.Status);
        Assert.False(saves.State.IsDirty);
        Assert.Equal("r2", saves.State.BaseRevision?.Value);
        Assert.Equal("second", System.Text.Encoding.UTF8.GetString(fixture.Driver.Content));
        Assert.Equal(2, fixture.Driver.ReplaceCalls);
        Assert.Equal(new FileCatalogRevision(0, 2), fixture.Revisions.Get(fixture.Scope, fixture.Storage.Id));
    }

    [Fact]
    public async Task Overwrite_without_explicit_permission_fails_before_write()
    {
        await using TestFixture fixture = CreateFixture();
        FileReference file = await fixture.GrantAsync(FileAccessOperation.View | FileAccessOperation.Edit);
        await using AsyncServiceScope serviceScope = fixture.Services.CreateAsyncScope();
        IFileToolsKnownFileSessionFactory factory = serviceScope.ServiceProvider.GetRequiredService<IFileToolsKnownFileSessionFactory>();
        FileToolsKnownFileSession interaction = await factory.CreateAsync(new FileToolsKnownFileRequest(
            fixture.Scope,
            file,
            FileToolsKnownFileIntent.Edit));
        fixture.Driver.ResetEffectCalls();

        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(() =>
            interaction.SaveTarget!.SaveAsync(new FileSaveRequest(
                file,
                editRevision: 1,
                new BufferedFileSaveContent("overwrite"u8.ToArray()),
                expectedRevision: null)).AsTask());

        Assert.Equal(FileAccessFailureCode.OperationDenied, exception.Code);
        Assert.Equal(0, fixture.Driver.ReplaceCalls);
    }

    private static TestFixture CreateFixture()
    {
        var storage = new StorageCatalogRecord
        {
            Id = Guid.NewGuid(),
            Name = "Authorized storage",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = "test",
            IsEnabled = true
        };
        var catalog = new FakeStorageCatalog(storage);
        var driver = new FakeStorageDriver();
        var contextProvider = new MutableContextProvider(CreateContext());
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-13T00:00:00Z"));
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(logs));
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<IFileAccessPolicy, AllowFileAccessPolicy>();
        services.AddSingleton<IFileAccessContextProvider>(contextProvider);
        services.AddSingleton<IStorageCatalogService>(catalog);
        services.AddSingleton<IStorageDriverRegistry>(new FakeStorageDriverRegistry(driver));
        var bindingProvider = new TestBindingProvider(storage.Id);
        services.AddSingleton<IFileToolsStorageBindingProvider>(bindingProvider);
        services.Configure<FileAccessHandleOptions>(options =>
        {
            options.MaximumEntries = 16;
            options.Lifetime = TimeSpan.FromSeconds(10);
            options.MaximumContentBytes = 1024 * 1024;
        });
        services.AddCanDoItAllFileToolsIntegration();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        IServiceScope primaryScope = provider.CreateScope();
        return new TestFixture(
            provider,
            primaryScope,
            catalog,
            driver,
            bindingProvider,
            contextProvider,
            time,
            storage,
            logs);
    }

    private static FileAccessContext CreateContext()
        => new(
            new FileAccessActorId("actor-a"),
            new FileAccessSessionId("session-a"),
            Guid.NewGuid(),
            runtimeGeneration: 2,
            authorizationRevision: 0,
            new FileAccessCorrelationId("correlation-a"));

    private static FileAccessContext CopyContext(
        FileAccessContext source,
        FileAccessActorId? actorId = null,
        FileAccessSessionId? sessionId = null,
        Guid? runtimeProfileId = null,
        long? authorizationRevision = null)
        => new(
            actorId ?? source.ActorId,
            sessionId ?? source.SessionId,
            runtimeProfileId ?? source.RuntimeProfileId,
            source.RuntimeGeneration,
            authorizationRevision ?? source.AuthorizationRevision,
            source.CorrelationId);

    private static async Task AssertDeniedAsync(Func<Task> action, FileAccessFailureCode code)
    {
        FileAccessDeniedException exception = await Assert.ThrowsAsync<FileAccessDeniedException>(action);
        Assert.Equal(code, exception.Code);
    }

    public enum FakeSaveOutcome
    {
        Success,
        Conflict,
        Failure,
        Cancelled
    }

    private sealed class TestFixture(
        ServiceProvider services,
        IServiceScope primaryScope,
        FakeStorageCatalog catalog,
        FakeStorageDriver driver,
        TestBindingProvider bindingProvider,
        MutableContextProvider contextProvider,
        ManualTimeProvider time,
        StorageCatalogRecord storage,
        CapturingLoggerProvider logs) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;

        public FakeStorageCatalog Catalog { get; } = catalog;

        public FakeStorageDriver Driver { get; } = driver;

        public TestBindingProvider BindingProvider { get; } = bindingProvider;

        public MutableContextProvider ContextProvider { get; } = contextProvider;

        public ManualTimeProvider Time { get; } = time;

        public CapturingLoggerProvider Logs { get; } = logs;

        public IFileCatalogRevisionReader Revisions =>
            Services.GetRequiredService<IFileCatalogRevisionReader>();

        public FileToolsSemanticScope Scope { get; } = new(
            FileToolsSemanticScopeKind.Project,
            new FileToolsSemanticScopeId("project-a"),
            "Project A");

        public StorageCatalogRecord Storage { get; } = storage;

        public IStorageFileAccessAuthorizationCoordinator Coordinator =>
            primaryScope.ServiceProvider.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>();

        public async ValueTask<FileReference> GrantAsync(FileAccessOperation operations)
            => await Coordinator.GrantAsync(
                new FileAccessGrantRequest(
                    ContextProvider.Current,
                    Scope,
                    Storage.Id,
                    "readme.md",
                    operations),
                new StorageObjectReference(
                    Storage.Id,
                    StorageProviderKind.FileSystem,
                    StorageLocatorKind.RelativePath,
                    "readme.md",
                    "readme.md",
                    "text/plain",
                    Driver.Content.LongLength));

        public async ValueTask DisposeAsync()
        {
            primaryScope.Dispose();
            await Services.DisposeAsync();
        }
    }

    private sealed class AllowFileAccessPolicy : IFileAccessPolicy
    {
        public ValueTask AuthorizeAsync(
            FileAccessGrantRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutableContextProvider(FileAccessContext context) : IFileAccessContextProvider
    {
        public FileAccessContext Current { get; set; } = context;

        public ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Current);
    }

    private sealed class TestBindingProvider(Guid storageId) : IFileToolsStorageBindingProvider
    {
        public bool IsAvailable { get; set; } = true;

        public ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
            FileToolsSemanticScope scope,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<FileToolsStorageBinding>>(
                IsAvailable
                    ?
                    [
                        new FileToolsStorageBinding(
                            storageId,
                            "Test files",
                            new FileToolsBrowseWorkLimits())
                    ]
                    : []);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class FakeStorageDriver : IStorageDriver, IStorageRevisionedContentDriver
    {
        public byte[] Content { get; private set; } = "initial-content"u8.ToArray();

        public string Revision { get; private set; } = "r1";

        public FakeSaveOutcome SaveOutcome { get; set; }

        public TaskCompletionSource ReplaceStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int OpenReadCalls { get; private set; }

        public int ReplaceCalls { get; private set; }

        public int EffectCalls => OpenReadCalls + ReplaceCalls;

        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageCapability SupportedCapabilities =>
            StorageCapability.Read | StorageCapability.Write | StorageCapability.MutableUpdate;

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            OpenReadCalls++;
            return Task.FromResult<Stream>(new MemoryStream(Content, writable: false));
        }

        public Task<StorageContentRevision?> GetRevisionAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => Task.FromResult<StorageContentRevision?>(new StorageContentRevision(Revision));

        public Task<StorageRevisionedWriteResult> ReplaceAsync(
            StorageCatalogRecord storage,
            StorageRevisionedWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            ReplaceCalls++;
            return SaveOutcome switch
            {
                FakeSaveOutcome.Conflict => Task.FromException<StorageRevisionedWriteResult>(
                    new StorageContentConflictException(request.ExpectedRevision, new StorageContentRevision("external"))),
                FakeSaveOutcome.Failure => Task.FromException<StorageRevisionedWriteResult>(new IOException("storage failed")),
                FakeSaveOutcome.Cancelled => WaitForCancellationAsync(cancellationToken),
                _ => Persist(request)
            };
        }

        public void ResetEffectCalls()
        {
            OpenReadCalls = 0;
            ReplaceCalls = 0;
        }

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

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private Task<StorageRevisionedWriteResult> Persist(StorageRevisionedWriteRequest request)
        {
            Content = request.Content;
            Revision = "r2";
            return Task.FromResult(new StorageRevisionedWriteResult(
                new StorageWriteResult(
                    request.Reference with { ContentLength = Content.LongLength },
                    new StorageAccessDescriptor("", "", null, false, false, false, "readme.md", "text/plain", Content.LongLength, "")),
                new StorageContentRevision(Revision)));
        }

        private async Task<StorageRevisionedWriteResult> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            ReplaceStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait completed unexpectedly.");
        }
    }

    private sealed class FakeStorageDriverRegistry(FakeStorageDriver driver) : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => [StorageProviderKind.FileSystem];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver resolved)
        {
            resolved = driver;
            return providerKind == StorageProviderKind.FileSystem;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => providerKind == StorageProviderKind.FileSystem
                ? driver
                : throw new InvalidOperationException("Unknown fake storage provider.");
    }

    private sealed class FakeBrowseDriver : IStorageBrowseDriver, IStorageBrowseStatDriver
    {
        public bool EntryExists { get; set; } = true;

        public string EntryId { get; set; } = "readme.md";

        public int StatCallCount { get; private set; }

        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageBrowseCapability Capabilities =>
            StorageBrowseCapability.Browse |
            StorageBrowseCapability.Stat |
            StorageBrowseCapability.ProviderNativeOrdering |
            StorageBrowseCapability.Metadata;

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
        {
            StorageBrowseEntry[] entries = EntryExists
                ?
                [
                    new StorageBrowseEntry(
                        new StorageBrowseEntryId(EntryId),
                        request.Container,
                        "readme.md",
                        "readme.md",
                        StorageBrowseEntryKind.File,
                        StorageBrowseEntryCapability.Read,
                        size: 15,
                        mediaType: "text/plain")
                ]
                : [];
            return Task.FromResult(new StorageBrowsePage(
                request.Container,
                [],
                entries,
                StorageBrowseSort.ProviderOrder,
                StorageBrowseCompleteness.Complete,
                new StorageBrowseOperationMetrics(entries.Length, entries.Length, entries.Length, 0, TimeSpan.Zero)));
        }

        public Task<StorageBrowseEntry> StatAsync(
            StorageCatalogRecord storage,
            StorageBrowseStatRequest request,
            CancellationToken cancellationToken = default)
        {
            StatCallCount++;
            if (!EntryExists)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.ProviderUnavailable,
                    "The entry is unavailable."));
            }

            return Task.FromResult(new StorageBrowseEntry(
                new StorageBrowseEntryId(EntryId),
                request.Container,
                "readme.md",
                "readme.md",
                StorageBrowseEntryKind.File,
                StorageBrowseEntryCapability.Read,
                size: 15,
                mediaType: "text/plain"));
        }
    }

    private sealed class FakeStorageCatalog(StorageCatalogRecord storage) : IStorageCatalogService
    {
        public bool IsAvailable { get; set; } = true;

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(IsAvailable && id == storage.Id ? storage : null);

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>(IsAvailable ? [storage] : []);

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storage);

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => Task.FromResult(rule);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
