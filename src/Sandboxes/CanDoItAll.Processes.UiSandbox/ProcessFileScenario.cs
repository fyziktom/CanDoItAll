using System.Text;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Processes.UI;

namespace CanDoItAll.Processes.UiSandbox;

public sealed class ProcessFileScenario : IFileBrowserProvider, IFileContentSource, IAsyncDisposable {
    private readonly FileBrowserItem root;
    private readonly FileBrowserItem file;
    private readonly byte[] content = Encoding.UTF8.GetBytes("# Process evidence\n\nThis managed artifact belongs to the selected synthetic process run.\n");
    private readonly IFileBrowserSession browser;
    private readonly FileReference reference;
    public FileBrowserSourceDescriptor Descriptor { get; }
    public ProcessRunFilesPresentation View { get; private set; }
    public bool FailNextActivation { get; set; }

    public ProcessFileScenario(Guid runId) {
        Descriptor = new(new FileBrowserSourceId($"scenario-{runId:N}"), "Managed artifacts");
        reference = new("process-scenario", runId.ToString("N"));
        root = new(new(Descriptor.Id, "root", "scenario-0001"), null, "Run artifacts", FileBrowserItemKind.Container, FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren, capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        file = new(new(Descriptor.Id, "evidence.md", "scenario-0001"), root.Key, "evidence.md", FileBrowserItemKind.File, FileBrowserItemCategory.Document,
            childState: FileBrowserChildState.Empty, size: content.Length, mediaType: "text/markdown",
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Preview);
        browser = new FileBrowserSession(new FileBrowserSourceSet("scenario-0001", [this]), options: new FileBrowserSessionOptions(retentionMode: FileBrowserStateRetentionMode.Disabled));
        View = new(runId, browser, 1, "scenario-0001", null, null, null, false, null, null, null);
    }

    public ValueTask<FileBrowserItem> GetRootAsync(FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default) => ValueTask.FromResult(root);
    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(FileBrowserItemKey itemKey, FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root]);
    public ValueTask<FileBrowserPage> BrowseAsync(FileBrowserBrowseRequest request, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new FileBrowserPage([file], null, 1, "scenario-0001"));
    public Task ActivateAsync(FileBrowserItemInvokedEventArgs args) {
        if (args.Item.Key != file.Key) {
            return Task.CompletedTask;
        }
        if (FailNextActivation) {
            FailNextActivation = false;
            View = View with { ActivationError = "The scenario file is temporarily unavailable. Select it again to retry." };
        } else {
            View = View with { Request = new(reference, file.Name, FileInteractionMode.View, "text/markdown", content.Length), ContentSource = this, ActivationError = null };
        }
        return Task.CompletedTask;
    }
    public Task RefreshAsync() {
        View = View with { Request = null, ContentSource = null, ActivationError = null, ActionFeedback = null };
        return browser.RefreshAsync().AsTask();
    }
    public void RecordExternalAction(FileInteractionRequest request) => View = View with { ActionFeedback = $"Scenario open requested: {request.FileName}." };
    public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.File != reference) {
            throw new InvalidOperationException("The file does not belong to this scenario opening.");
        }
        var offset = checked((int)Math.Min(request.Offset, content.Length));
        var count = checked((int)Math.Min(request.Length ?? content.Length, content.Length - offset));
        return ValueTask.FromResult(new FileContentLease(new MemoryStream(content, offset, count, writable: false), "text/markdown", count));
    }
    public ValueTask DisposeAsync() => browser.DisposeAsync();
}
