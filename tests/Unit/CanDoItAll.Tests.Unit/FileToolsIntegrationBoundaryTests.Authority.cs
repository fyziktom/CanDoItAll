using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed partial class FileToolsIntegrationBoundaryTests {
    public enum SourceChange { None, Disabled, Retargeted, Unbound }

    [Theory]
    [InlineData(false, SourceChange.None)]
    [InlineData(false, SourceChange.Disabled)]
    [InlineData(false, SourceChange.Retargeted)]
    [InlineData(false, SourceChange.Unbound)]
    [InlineData(true, SourceChange.Disabled)]
    [InlineData(true, SourceChange.Retargeted)]
    [InlineData(true, SourceChange.Unbound)]
    public async Task Bound_native_browse_refuses_removed_disabled_or_retargeted_sources_at_read_publication(bool duringRead, SourceChange change) {
        var storage = CreateStorage();
        var native = new FakeBrowseDriver(storage.ProviderKind) { Page = CreateNativePage(StorageBrowseCompleteness.Complete) };
        var bindings = new List<FileToolsStorageBinding> { CreateBinding(storage.Id) };
        var factory = CreateFactory(storage, bindings, native);
        var session = await factory.CreateAsync(CreateScope(), authorizeRead: _ => ValueTask.CompletedTask);
        var provider = Assert.Single(session.Providers);
        var root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        void ChangeSource() {
            switch (change) {
                case SourceChange.Disabled:
                    storage.IsEnabled = false;
                    break;
                case SourceChange.Retargeted:
                    storage.EndpointOrRoot = "unreviewed-peer-source";
                    break;
                case SourceChange.Unbound:
                    bindings.Clear();
                    break;
            }
        }
        if (duringRead) {
            native.AfterRead = ChangeSource;
        } else {
            ChangeSource();
        }
        var request = new FileBrowserBrowseRequest(root.Key, pageSize: 5,
            sort: new(FileBrowserSortField.ProviderNative, FileBrowserSortDirection.Ascending, FoldersFirst: false));
        if (change == SourceChange.None) {
            Assert.Single((await provider.BrowseAsync(request)).Items);
        } else {
            await Assert.ThrowsAsync<FileAccessDeniedException>(() => provider.BrowseAsync(request).AsTask());
        }
        Assert.Equal(change == SourceChange.None || duringRead ? 1 : 0, native.CallCount);
    }

    public enum ReadBoundary { Allowed, BeforeNativeRead, AfterNativeRead }

    [Theory]
    [InlineData(false, ReadBoundary.Allowed)]
    [InlineData(false, ReadBoundary.BeforeNativeRead)]
    [InlineData(false, ReadBoundary.AfterNativeRead)]
    [InlineData(true, ReadBoundary.Allowed)]
    [InlineData(true, ReadBoundary.BeforeNativeRead)]
    [InlineData(true, ReadBoundary.AfterNativeRead)]
    public async Task Native_browse_and_path_withhold_private_rows_after_original_read_grant_retires(bool path, ReadBoundary boundary) {
        var allowed = true;
        var checks = 0;
        var native = new FakeBrowseDriver(StorageProviderKind.FileSystem) { Page = CreateNativePage(StorageBrowseCompleteness.Complete) };
        var storage = CreateStorage();
        ValueTask AuthorizeAsync(CancellationToken token) {
            token.ThrowIfCancellationRequested();
            checks++;
            if (!allowed) {
                throw new UnauthorizedAccessException("Original native collection read has retired.");
            }
            return ValueTask.CompletedTask;
        }
        var provider = new StorageFileBrowserProvider(CreateScope(), CreateBinding(storage.Id), storage.ToDriverInput(), native, native,
            authorizeRead: AuthorizeAsync);
        var root = await provider.GetRootAsync(FileBrowserMetadataRequest.Standard);
        allowed = boundary != ReadBoundary.BeforeNativeRead;
        native.AfterRead = () => allowed = boundary != ReadBoundary.AfterNativeRead;
        async Task ReadAsync() {
            if (path) {
                Assert.NotEmpty(await provider.GetPathAsync(root.Key, FileBrowserMetadataRequest.Standard));
            } else {
                var page = await provider.BrowseAsync(new(root.Key, pageSize: 5,
                    sort: new(FileBrowserSortField.ProviderNative, FileBrowserSortDirection.Ascending, FoldersFirst: false)));
                Assert.Single(page.Items);
            }
        }
        if (boundary == ReadBoundary.Allowed) {
            await ReadAsync();
            Assert.True(checks >= 3);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(ReadAsync);
        }
        Assert.Equal(boundary == ReadBoundary.BeforeNativeRead ? 0 : 1, native.CallCount);
    }
}
