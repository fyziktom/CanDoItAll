using System.Globalization;
using System.Text;
using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.Projects.Files.UiSandbox;

public sealed class FilesFixtureProvider : IFileBrowserProvider {
    private readonly FileBrowserItem root;
    private readonly Dictionary<FileBrowserItemKey, FilesFixture> content;
    public FileBrowserSourceDescriptor Descriptor { get; }
    public IReadOnlyList<FileBrowserItem> Files { get; }
    public FilesFixtureGate? Gate { get; set; }
    public bool FailNextRead { get; set; }
    public bool Removed { get; set; }
    public int ReadCalls { get; private set; }

    public FilesFixtureProvider(string id, int count = 7) {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 500);
        Descriptor = new(new FileBrowserSourceId(id), $"Synthetic {id}", description: "Bounded in-memory fixture files");
        root = new(new(Descriptor.Id, "root", "fixture-1"), null, "Project files", FileBrowserItemKind.Container, FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren, capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        var fixtures = FilesFixtureCatalog.Create();
        content = [];
        var files = new List<FileBrowserItem>();
        for (int index = 0; index < count; index++) {
            var fixture = index < fixtures.Count ? fixtures[index] : new FilesFixture($"notes-{index:D3}.txt", "text/plain", Encoding.UTF8.GetBytes($"Bounded item {index}."));
            var key = new FileBrowserItemKey(Descriptor.Id, fixture.Name, "fixture-1");
            content.Add(key, fixture);
            files.Add(new(key, root.Key, fixture.Name, FileBrowserItemKind.File, FileBrowserItemCategory.Document,
                childState: FileBrowserChildState.Empty, size: fixture.Bytes.Length, mediaType: fixture.MediaType,
                capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Preview));
        }
        Files = files;
    }

    public FilesFixture Get(FileBrowserItemKey key) {
        RequireAvailable();
        return content.TryGetValue(key, out var fixture) ? fixture : throw new FileBrowserProviderException(new(FileBrowserErrorCode.NotFound, "The fixture item was removed."));
    }

    public ValueTask<FileBrowserItem> GetRootAsync(FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default) {
        RequireAvailable();
        return ValueTask.FromResult(root);
    }

    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(FileBrowserItemKey itemKey, FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default) {
        RequireAvailable();
        return ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root]);
    }

    public async ValueTask<FileBrowserPage> BrowseAsync(FileBrowserBrowseRequest request, CancellationToken cancellationToken = default) {
        ReadCalls++;
        if (Gate is { } gate) {
            Gate = null;
            await gate.WaitAsync();
        }
        cancellationToken.ThrowIfCancellationRequested();
        RequireAvailable();
        if (FailNextRead) {
            FailNextRead = false;
            throw new FileBrowserProviderException(new(FileBrowserErrorCode.Unavailable, "Injected fixture source read failure.", isRetryable: true));
        }
        int offset = request.ContinuationToken is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : 0;
        var page = Files.Skip(offset).Take(request.PageSize).ToArray();
        int next = offset + page.Length;
        return new(page, next < Files.Count ? next.ToString(CultureInfo.InvariantCulture) : null, Files.Count, "fixture-1");
    }

    private void RequireAvailable() {
        if (Removed) {
            throw new FileBrowserProviderException(new(FileBrowserErrorCode.NotFound, "This synthetic source is no longer available."));
        }
    }
}

public sealed class FilesFixtureGate {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task WaitAsync() {
        Entered.TrySetResult();
        await release.Task;
    }
    public void Release() => release.TrySetResult();
}
