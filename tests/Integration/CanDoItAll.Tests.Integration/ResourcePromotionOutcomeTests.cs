using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CanDoItAll.Resources.UI;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "HostPlatform")]
public sealed class ResourcePromotionOutcomeTests {
    public enum PromotionFault { Publish, Read, Log, Cleanup, PublishCleanupAndLog, LostAcknowledgement }
    public enum SelectionFault { Scope, Revision, ItemSource }

    [Fact]
    public async Task Failed_preview_preserves_its_primary_failure_when_real_grant_cleanup_also_fails() {
        await using var fixture = await Fixture.CreateAsync();
        var stored = await fixture.Services.GetRequiredService<ResourceStorageObjectPromotionService>().PromoteAsync(fixture.Command);
        var boundary = new PreviewFaultBoundary(fixture.Services.GetRequiredService<IFileToolsKnownFileSessionReleaser>());
        var owner = new ResourceStorageObjectInteractionService(fixture.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>(),
            fixture.Services.GetRequiredService<IResourceFileSourceCatalog>(), fixture.Services.GetRequiredService<IFileToolsKnownFileActivator>(), boundary, boundary);
        var failure = await Assert.ThrowsAsync<IOException>(async () => await owner.OpenAsync(stored.ResourceId));
        Assert.Same(boundary.Primary, failure);
        Assert.True(failure.Data.Contains(nameof(IFileToolsKnownFileSessionReleaser.ReleaseAsync)));
        Assert.Equal(1, boundary.Releases);
        var context = await fixture.Services.GetRequiredService<IFileAccessContextProvider>().GetCurrentAsync();
        await Assert.ThrowsAsync<FileAccessDeniedException>(async () => await fixture.Services.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>()
            .ResolveAsync(boundary.File!.Value, context, FileAccessOperation.View));
    }

    [Theory]
    [InlineData(SelectionFault.Scope)]
    [InlineData(SelectionFault.Revision)]
    [InlineData(SelectionFault.ItemSource)]
    public async Task Production_browse_adapter_refuses_stale_origins_before_any_effect(SelectionFault fault) {
        await using var fixture = await Fixture.CreateAsync();
        var owner = fixture.Services.GetRequiredService<IResourceBrowseOwner>();
        await using var lease = await owner.OpenAsync(fixture.Command.SourceKey);
        var selection = new ResourceFileSelection(lease.Source, lease.Revision, fixture.Item);
        if (fault == SelectionFault.Scope) {
            selection = selection with { Source = selection.Source with {
                Scope = new(FileToolsSemanticScopeKind.ResourceSource, new("unrelated-owned-source"), "Unrelated source")
            } };
        } else if (fault == SelectionFault.Revision) {
            fixture.Services.GetRequiredService<IFileCatalogChangeSink>().PublishStorageChanged(lease.Source.StorageId!.Value);
        } else {
            selection = selection with { Item = new(new(new("unrelated-provider"), "file", "revision"), null,
                "foreign.txt", FileBrowserItemKind.File, FileBrowserItemCategory.Document) };
        }
        await Assert.ThrowsAsync<ResourceActionRefusedException>(async () => await owner.AuthorizeDownloadAsync(selection));
        await Assert.ThrowsAsync<ResourceActionRefusedException>(async () => await owner.LaunchAsync(selection, FileToolsLocalFileAction.OpenContainingFolder));
        await Assert.ThrowsAsync<ResourceActionRefusedException>(async () => await owner.PromoteAsync(new(selection,
            fixture.Command.ExpectedProjectAdmission!, "Refused target", ResourceSensitivity.Normal)));
        await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.Set<ProjectResource>().ToArrayAsync());
    }

    [Fact]
    public async Task Real_transaction_commit_acknowledgement_loss_is_unknown_and_exact_review_does_not_create_a_duplicate() {
        var commit = new CommitAcknowledgementFault();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = services =>
            services.AddSingleton<IDbContextFactory<ResourcesDbContext>>(provider => new PooledDbContextFactory<ResourcesDbContext>(
                new DbContextOptionsBuilder<ResourcesDbContext>(provider.GetRequiredService<DbContextOptions<ResourcesDbContext>>()).AddInterceptors(commit).Options)) });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Unknown Resource commit acknowledgement");
        using var controller = new ResourceRegistryController(services.GetRequiredService<IResourceRegistryOwner>());
        await controller.LoadRouteAsync(null, admission.ProjectId);
        await controller.ChangeConnectorAsync(ResourceConnectorPluginKeys.WebLink);
        controller.Draft.Editor.Name = "Exact lost acknowledgement";
        controller.Draft.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://example.test/owned");
        commit.Armed = true;
        await controller.SaveAsync();
        Assert.True(commit.Fired);
        var receipt = Assert.Single(controller.Receipts);
        Assert.Equal(ResourceEffectState.Unknown, receipt.State);
        Assert.Null(receipt.ResourceId);
        await using var database = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        var stored = Assert.Single(await database.Set<ProjectResource>().AsNoTracking().ToListAsync());
        await controller.ReviewIdentityAsync(receipt, stored.Id);
        Assert.Equal(stored.Id, receipt.ReviewedResource!.Id);
        Assert.Equal(ResourceEffectState.Unknown, receipt.State);
        await controller.SaveAsync();
        Assert.Equal(1, await database.Set<ProjectResource>().CountAsync());
        await controller.SelectAsync(stored.Id);
        controller.Draft.Editor.Name = "Explicit edit after exact review";
        await controller.SaveAsync();
        Assert.Equal(stored.Id, controller.Draft.Editor.Id);
        Assert.Equal(1, await database.Set<ProjectResource>().CountAsync());
        Assert.Equal("Explicit edit after exact review", (await database.Set<ProjectResource>().AsNoTracking().SingleAsync()).Name);
    }

    [Theory]
    [InlineData(PromotionFault.Publish)]
    [InlineData(PromotionFault.Read)]
    [InlineData(PromotionFault.Log)]
    [InlineData(PromotionFault.Cleanup)]
    [InlineData(PromotionFault.PublishCleanupAndLog)]
    public async Task Actual_persisted_identity_survives_postwrite_faults_and_grant_is_revoked(PromotionFault fault) {
        await using var fixture = await Fixture.CreateAsync();
        if (fault == PromotionFault.Read) {
            await fixture.Services.GetRequiredService<ResourceStorageObjectPromotionService>().PromoteAsync(fixture.Command);
        }
        var faultBoundary = new FaultBoundary(fixture.Services, fault);
        var service = faultBoundary.CreateService();
        var failure = await Assert.ThrowsAsync<ResourcePromotionCommittedException>(async () => await service.PromoteAsync(fixture.Command));
        await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        var stored = Assert.Single(await database.Set<ProjectResource>().AsNoTracking().ToListAsync());
        Assert.Equal(stored.Id, failure.ResourceId);
        Assert.Equal(fault != PromotionFault.Read, failure.Created);
        Assert.Same(faultBoundary.Primary, failure.InnerException);
        Assert.Equal(fault is PromotionFault.Publish or PromotionFault.Read or PromotionFault.PublishCleanupAndLog, failure.Revision is null);
        Assert.Equal(1, faultBoundary.Revocations);
        Assert.NotNull(faultBoundary.Handle);
        var context = await fixture.Services.GetRequiredService<IFileAccessContextProvider>().GetCurrentAsync();
        await Assert.ThrowsAsync<FileAccessDeniedException>(async () => await fixture.Services.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>()
            .ResolveAsync(faultBoundary.Handle!.Value, context, FileAccessOperation.View));
        var config = StorageObjectResourceConnectorPlugin.Deserialize(stored.ConfigJson);
        Assert.Equal(fixture.Item.Name, config.Locator);
        Assert.DoesNotContain(faultBoundary.Handle.Value.Value, stored.ConfigJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.FullPath, stored.ConfigJson, StringComparison.Ordinal);
        if (fault == PromotionFault.PublishCleanupAndLog) {
            Assert.True(faultBoundary.Primary.Data.Contains(nameof(IStorageFileAccessAuthorizationCoordinator.RevokeAsync)));
            Assert.True(faultBoundary.Primary.Data.Contains(nameof(ResourceMutationDiagnostics)));
        }
        await using var preview = await fixture.Services.GetRequiredService<ResourceStorageObjectInteractionService>().OpenAsync(failure.ResourceId);
        await using var content = await preview.Session.ContentSource.OpenReadAsync(new(preview.Session.File));
        using var reader = new StreamReader(content.Stream);
        Assert.Equal(Fixture.Content, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Actual_commit_with_lost_writer_acknowledgement_remains_unknown_and_is_not_wrapped_as_confirmed() {
        await using var fixture = await Fixture.CreateAsync();
        var boundary = new FaultBoundary(fixture.Services, PromotionFault.LostAcknowledgement);
        var failure = await Assert.ThrowsAsync<ResourcePromotionException>(async () => await boundary.CreateService().PromoteAsync(fixture.Command));
        Assert.Equal(ResourcePromotionFailureCode.PersistenceFailed, failure.Code);
        Assert.Same(boundary.Primary, failure.InnerException);
        await using var database = await fixture.Services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        Assert.Equal(boundary.StoredId, Assert.Single(await database.Set<ProjectResource>().ToListAsync()).Id);
        Assert.Equal(0, boundary.Publications);
        Assert.Equal(1, boundary.Revocations);
    }

    [Theory]
    [InlineData(ResourceMutationKind.Save)]
    [InlineData(ResourceMutationKind.Delete)]
    public async Task Resource_postcommit_primary_failure_survives_throwing_diagnostics(ResourceMutationKind kind) {
        var probe = new OwnerPostcommitTestProbe();
        var diagnostics = new ThrowingLogger<ResourcesService>();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            probe.ConfigureServices(services);
            services.AddSingleton<ILogger<ResourcesService>>(diagnostics);
        } });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Resources diagnostic outcome");
        var owner = services.GetRequiredService<ResourcesService>();
        var editor = OwnerPostcommitTestProbe.Resource(services, admission);
        Guid? id = null;
        if (kind == ResourceMutationKind.Delete) {
            id = (await owner.SaveAsync(editor)).Value;
        }
        var primary = new IOException("Owned postcommit search failure.");
        probe.ArmFault(PostcommitOwner.Resource, PostcommitFault.Search, primary);
        diagnostics.Armed = true;
        var observed = await Assert.ThrowsAsync<ResourceCommittedMutationException>(() => kind == ResourceMutationKind.Save
            ? owner.SaveAsync(editor) : owner.DeleteAsync(id!.Value, admission));
        Assert.Same(primary, observed.InnerException);
        Assert.Equal(kind, observed.MutationKind);
        Assert.True(primary.Data.Contains(nameof(ResourceMutationDiagnostics)));
        await using var database = await services.GetRequiredService<IDbContextFactory<ResourcesDbContext>>().CreateDbContextAsync();
        Assert.Equal(kind == ResourceMutationKind.Save, await database.Set<ProjectResource>().AnyAsync(r => r.Id == observed.ResourceId));
    }

    private sealed class PreviewFaultBoundary(IFileToolsKnownFileSessionReleaser releaser) : IFileToolsKnownFileSessionFactory, IFileToolsKnownFileSessionReleaser {
        public IOException Primary { get; } = new("Owned preview creation failure.");
        public FileReference? File { get; private set; }
        public int Releases { get; private set; }
        public ValueTask<FileToolsKnownFileSession> CreateAsync(FileToolsKnownFileRequest request, CancellationToken cancellationToken = default) {
            File = request.File;
            return ValueTask.FromException<FileToolsKnownFileSession>(Primary);
        }
        public async ValueTask ReleaseAsync(FileReference file, CancellationToken cancellationToken = default) {
            Assert.Equal(File, file);
            Releases++;
            await releaser.ReleaseAsync(file, cancellationToken);
            throw new IOException("Owned secondary preview cleanup failure.");
        }
    }

    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, string fullPath, FileBrowserItem item, ResourceStorageObjectPromotionCommand command) : IAsyncDisposable {
        public const string Content = "Task-owned PostgreSQL promotion outcome proof";
        public IServiceProvider Services => scope.ServiceProvider;
        public string FullPath => fullPath;
        public FileBrowserItem Item => item;
        public ResourceStorageObjectPromotionCommand Command => command;
        public static async Task<Fixture> CreateAsync() {
            var application = await TestApplication.CreateAsync();
            var scope = application.Services.CreateAsyncScope();
            string? path = null;
            try {
                var services = scope.ServiceProvider;
                var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(services, "Resource promotion outcome");
                var storage = await services.GetRequiredService<IStorageCatalogService>().EnsureBootstrapFileSystemStorageAsync();
                var name = $"resources-outcome-{Guid.NewGuid():N}.txt";
                path = Path.Combine(storage.EndpointOrRoot, name);
                await File.WriteAllTextAsync(path, Content);
                var key = ResourceFileSourceKey.ForStorage(storage.Id);
                await using var browse = await services.GetRequiredService<ResourceFileBrowseCoordinator>().OpenAsync(key);
                await browse.Browser.InitializeAsync();
                var item = Assert.Single(browse.Browser.Snapshot.Items, candidate => candidate.Name == name);
                return new(application, scope, path, item, new(key, item.Key, admission.ProjectId, "Owned promoted resource", admission));
            } catch {
                if (path is not null) {
                    File.Delete(path);
                }
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }
        public async ValueTask DisposeAsync() {
            try {
                File.Delete(fullPath);
            } finally {
                await scope.DisposeAsync();
                await application.DisposeAsync();
            }
        }
    }

    private sealed class FaultBoundary(IServiceProvider services, PromotionFault fault) : IStorageFileAccessAuthorizationCoordinator,
        IFileCatalogChangeSink, IFileCatalogRevisionReader, IStorageObjectResourceWriter, ILogger<ResourceStorageObjectPromotionService> {
        public IOException Primary { get; } = new($"Owned {fault} failure");
        public FileReference? Handle { get; private set; }
        public Guid? StoredId { get; private set; }
        public int Revocations { get; private set; }
        public int Publications { get; private set; }
        public ResourceStorageObjectPromotionService CreateService() => new(services.GetRequiredService<IResourceFileSourceCatalog>(),
            services.GetRequiredService<IFileToolsBrowseItemActivator>(), services.GetRequiredService<IFileAccessContextProvider>(), this, this, this, this, this);
        public ValueTask<FileReference> GrantAsync(FileAccessGrantRequest request, StorageObjectReference reference, CancellationToken cancellationToken = default) =>
            services.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>().GrantAsync(request, reference, cancellationToken);
        public ValueTask<AuthorizedStorageFile> ResolveAsync(FileReference file, FileAccessContext context, FileAccessOperation operation, CancellationToken cancellationToken = default) {
            Handle = file;
            return services.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>().ResolveAsync(file, context, operation, cancellationToken);
        }
        public async ValueTask RevokeAsync(FileReference file, CancellationToken cancellationToken = default) {
            Revocations++;
            await services.GetRequiredService<IStorageFileAccessAuthorizationCoordinator>().RevokeAsync(file, cancellationToken);
            if (fault == PromotionFault.Cleanup) {
                throw Primary;
            }
            if (fault == PromotionFault.PublishCleanupAndLog) {
                throw new IOException("Owned secondary revoke failure");
            }
        }
        public ValueTask RevokeAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public FileCatalogRevision PublishStorageChanged(Guid storageId) => throw new NotSupportedException();
        public FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId) {
            if (fault is PromotionFault.Publish or PromotionFault.PublishCleanupAndLog) {
                throw Primary;
            }
            Publications++;
            return services.GetRequiredService<IFileCatalogChangeSink>().PublishScopeChanged(scope, storageId);
        }
        public FileCatalogRevision Get(FileToolsSemanticScope scope, Guid storageId) => fault == PromotionFault.Read
            ? throw Primary : services.GetRequiredService<IFileCatalogRevisionReader>().Get(scope, storageId);
        public async Task<StorageObjectResourceWriteResult> SaveAsync(StorageObjectResourceWriteRequest request, CancellationToken cancellationToken = default) {
            var result = await services.GetRequiredService<IStorageObjectResourceWriter>().SaveAsync(request, cancellationToken);
            StoredId = result.ResourceId;
            if (fault == PromotionFault.LostAcknowledgement) {
                throw Primary;
            }
            return result;
        }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (fault == PromotionFault.Log) {
                throw Primary;
            }
            if (fault == PromotionFault.PublishCleanupAndLog) {
                throw new IOException("Owned secondary diagnostic failure");
            }
        }
    }
    private sealed class ThrowingLogger<T> : ILogger<T> {
        public bool Armed { get; set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Armed) {
                throw new IOException("Owned diagnostic failure");
            }
        }
    }
    private sealed class CommitAcknowledgementFault : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Fired = true;
                throw new IOException("Owned commit acknowledgement loss after PostgreSQL committed.");
            }
            return Task.CompletedTask;
        }
    }
}
