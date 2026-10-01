using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Projects.Files.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectFilesSurfaceSession(
    object receiver,
    IFileAccessContextProvider contexts,
    IFileToolsBrowseItemActionService actions,
    FileInteractionComponentComposition composition,
    IJSRuntime js,
    ILogger logger) : IAsyncDisposable {
    private ProjectFilesActivation? active;
    private long generation;
    private bool disposed;
    public ProjectFilesViewState State { get; private set; } = new();

    public async Task OpenAsync(Func<CancellationToken, ValueTask<ProjectFilesOwnedWorkspace>> open, string summary, EventCallback closed = default) {
        if (disposed) {
            return;
        }
        var selectedSource = active?.Workspace?.Browser.Snapshot.CurrentSource?.Id;
        var selectedLocation = active?.Workspace?.Browser.Snapshot.Location?.Current.Key;
        var old = Detach();
        var origin = new ProjectFilesActivation(open, summary, closed);
        active = origin;
        var operation = Begin(origin);
        State = new() {
            IsLoading = true,
            SourceSummary = summary,
            Retry = Callback(() => ReopenAsync(origin)),
            Refresh = Callback(() => ReopenAsync(origin)),
            Back = Callback(() => ReopenAsync(origin)),
            Closed = Callback(() => CloseAsync(origin))
        };
        ProjectFilesOwnedWorkspace? acquired = null;
        var cleanup = ReleaseAsync(old).AsTask();
        try {
            origin.Context = await contexts.GetCurrentAsync(operation.Token);
            await cleanup;
            await RequireContextAsync(origin, operation.Token);
            acquired = await open(operation.Token);
            await RequireContextAsync(origin, operation.Token);
            var browser = acquired.Browser;
            var source = selectedSource is { } selected && browser.Snapshot.Sources.Any(item => item.Id == selected) ? selectedSource : null;
            if (browser.Snapshot.Sources.Count > 0) {
                await browser.InitializeAsync(source, source is null ? null : selectedLocation, operation.Token);
            }
            await RequireContextAsync(origin, operation.Token);
            origin.Workspace = acquired;
            acquired = null;
            var workspace = origin.Workspace;
            State = State with {
                IsLoading = false,
                IsEmpty = workspace.ProjectCount == 0,
                SourceSummary = workspace.Summary,
                Snapshot = browser.Snapshot,
                Browse = new(browser, new DefaultFileToolsHostActionCatalog(workspace.Availability, actions.IsLocalLaunchAvailable),
                    EventCallback.Factory.Create<FileBrowserSnapshot>(receiver, snapshot => SnapshotChanged(origin, workspace, snapshot)),
                    EventCallback.Factory.Create<FileBrowserItemInvokedEventArgs>(receiver, args => ActivateAsync(origin, workspace, args)),
                    EventCallback.Factory.Create<FileBrowserItemActionEventArgs>(receiver, args => ActAsync(origin, workspace, args)))
            };
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            Report(exception, "open", origin);
            if (IsCurrent(origin)) {
                State = State with { IsLoading = false, OpenError = SafeMessage(exception, "Unable to open project files. Retry to resolve the current sources.") };
            }
        } finally {
            await cleanup;
            await ReleaseAsync(acquired?.Owner);
            Finish(origin, operation);
        }
    }

    private Task ReopenAsync(ProjectFilesActivation origin)
        => IsCurrent(origin) ? OpenAsync(origin.Open, origin.Summary, origin.Closed) : Task.CompletedTask;

    private EventCallback Callback(Func<Task> action) => EventCallback.Factory.Create(receiver, action);

    private async Task ActivateAsync(ProjectFilesActivation origin, ProjectFilesOwnedWorkspace workspace, FileBrowserItemInvokedEventArgs args) {
        if (!IsBound(origin, workspace) || args.Item.IsContainer || origin.Operation?.IsAction == true || !ContainsItem(workspace, args.Item.Key)) {
            return;
        }
        var item = args.Item;
        if (args.Kind == FileBrowserInvocationKind.PointerDoubleClick &&
            !FileInteractionDefaultActivationPolicy.ShouldOpenInternally(item, composition.Core) && SupportsLocalOpen(workspace, item.Key.SourceId)) {
            await ExecuteActionAsync(origin, workspace, item.Key, FileToolsHostAction.OpenInPreferredApplication);
            return;
        }
        var operation = Begin(origin);
        var previous = origin.Preview;
        origin.Preview = null;
        origin.PreviewItem = null;
        State = State with { Preview = null, ActivationError = null, ActionFeedback = null };
        ProjectFilesPilotInteraction? acquired = null;
        try {
            await ReleaseAsync(previous);
            await RequireContextAsync(origin, operation.Token);
            acquired = await workspace.Activate(item.Key, operation.Token);
            await RequireContextAsync(origin, operation.Token);
            origin.Preview = acquired;
            origin.PreviewItem = item.Key;
            var preview = acquired;
            acquired = null;
            State = State with { Preview = new(preview.Request, preview.Session.ContentSource, composition,
                SupportsLocalOpen(workspace, item.Key.SourceId)
                    ? EventCallback.Factory.Create<FileInteractionRequest>(receiver, request =>
                        IsBound(origin, workspace) && ReferenceEquals(origin.Preview, preview) && request.File == preview.Request.File
                            ? ExecuteActionAsync(origin, workspace, item.Key, FileToolsHostAction.OpenInPreferredApplication)
                            : Task.CompletedTask)
                    : default) };
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            Report(exception, "preview", origin);
            if (IsCurrent(origin)) {
                State = State with { ActivationError = SafeMessage(exception, "Unable to open the selected project file.") };
            }
        } finally {
            await ReleaseAsync(acquired);
            Finish(origin, operation);
        }
    }

    private Task ActAsync(ProjectFilesActivation origin, ProjectFilesOwnedWorkspace workspace, FileBrowserItemActionEventArgs args) {
        if (!IsBound(origin, workspace) || args.Origin != FileBrowserActionOrigin.Host || !ContainsItem(workspace, args.Item.Key)) {
            return Task.CompletedTask;
        }
        FileToolsHostAction? action = args.ActionId switch {
            var id when id == FileBrowserActionIds.Open => FileToolsHostAction.OpenInPreferredApplication,
            var id when id == FileToolsBrowseHostActionIds.OpenContainingFolder => FileToolsHostAction.OpenContainingFolder,
            var id when id == FileBrowserActionIds.Download => FileToolsHostAction.Download,
            _ => null
        };
        if (action is null) {
            State = State with { ActivationError = "The selected project file action is not supported by this host." };
            return Task.CompletedTask;
        }
        return ExecuteActionAsync(origin, workspace, args.Item.Key, action.Value);
    }

    private async Task ExecuteActionAsync(ProjectFilesActivation origin, ProjectFilesOwnedWorkspace workspace, FileBrowserItemKey item, FileToolsHostAction action) {
        if (!IsBound(origin, workspace) || origin.Operation?.IsAction == true) {
            return;
        }
        var scope = workspace.ResolveScope(item.SourceId);
        var operation = Begin(origin, action: true);
        State = State with { ActivationError = null, ActionFeedback = null };
        await using var runner = new FileToolsHostActionRunner(new ProjectFilesActionInterop(js, token => RequireContextAsync(origin, token)));
        try {
            await RequireContextAsync(origin, operation.Token);
            var result = await runner.ExecuteAsync(action,
                (localAction, token) => actions.LaunchAsync(scope, item, localAction, token),
                token => actions.AuthorizeDownloadAsync(scope, item, token), operation.Token);
            await RequireContextAsync(origin, operation.Token);
            State = result.IsSuccess ? State with { ActionFeedback = result.Message } : State with { ActivationError = result.Message };
        } catch (OperationCanceledException) when (operation.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            Report(exception, "action", origin);
            if (IsCurrent(origin)) {
                State = State with { ActivationError = SafeMessage(exception, action == FileToolsHostAction.Download
                    ? "Download completion could not be confirmed. Inspect browser downloads before another explicit attempt."
                    : "Local-open completion could not be confirmed. Inspect the application before another explicit attempt.") };
            }
        } finally {
            Finish(origin, operation);
        }
    }

    private void SnapshotChanged(ProjectFilesActivation origin, ProjectFilesOwnedWorkspace workspace, FileBrowserSnapshot snapshot) {
        if (!IsBound(origin, workspace)) {
            return;
        }
        State = State with { Snapshot = snapshot };
        if (snapshot.Search is { } search && !snapshot.IsBusy) {
            logger.LogInformation("Project file search completed. Returned={Returned} Inspected={Inspected} Retained={Retained} Bytes={Bytes} DurationMs={DurationMs} Partial={Partial}.",
                snapshot.Items.Count, search.ScannedItems, search.RetainedItems, search.RetainedBytes, search.Elapsed.TotalMilliseconds, search.IsPartial);
        }
    }

    private bool IsCurrent(ProjectFilesActivation origin) => !disposed && !origin.Retired && ReferenceEquals(active, origin);
    private bool IsBound(ProjectFilesActivation origin, ProjectFilesOwnedWorkspace workspace) => IsCurrent(origin) && ReferenceEquals(origin.Workspace, workspace);
    private static bool ContainsItem(ProjectFilesOwnedWorkspace workspace, FileBrowserItemKey key) => workspace.Browser.Snapshot.Items.Any(item => item.Key == key);
    private bool SupportsLocalOpen(ProjectFilesOwnedWorkspace workspace, FileBrowserSourceId source) => actions.IsLocalLaunchAvailable && workspace.Availability(source).SupportsLocalOpen;

    private void RequireCurrent(ProjectFilesActivation origin, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (!IsCurrent(origin)) {
            throw new OperationCanceledException("The project file activation has retired.", token);
        }
    }

    private async ValueTask RequireContextAsync(ProjectFilesActivation origin, CancellationToken token) {
        RequireCurrent(origin, token);
        var current = await contexts.GetCurrentAsync(token);
        RequireCurrent(origin, token);
        var expected = origin.Context;
        if (expected is null || current.ActorId != expected.ActorId || current.SessionId != expected.SessionId ||
            current.RuntimeProfileId != expected.RuntimeProfileId || current.RuntimeGeneration != expected.RuntimeGeneration ||
            current.AuthorizationRevision != expected.AuthorizationRevision) {
            throw new FileAccessDeniedException(FileAccessFailureCode.ContextMismatch, "The file access context changed. Reopen project files in the current workspace.");
        }
    }

    private static ProjectFilesOperation Begin(ProjectFilesActivation origin, bool action = false) {
        origin.Operation?.Cancel();
        var operation = new ProjectFilesOperation(action);
        origin.Operation = operation;
        return operation;
    }

    private static void Finish(ProjectFilesActivation origin, ProjectFilesOperation operation) {
        if (ReferenceEquals(origin.Operation, operation)) {
            origin.Operation = null;
        }
        operation.Dispose();
    }

    private IAsyncDisposable?[] Detach() {
        generation++;
        var old = active;
        active = null;
        State = new();
        if (old is null) {
            return [];
        }
        old.Retired = true;
        old.Operation?.Cancel();
        IAsyncDisposable?[] resources = [old.Preview, old.Workspace?.Owner];
        old.Preview = null;
        old.PreviewItem = null;
        old.Workspace = null;
        return resources;
    }

    private async Task CloseAsync(ProjectFilesActivation origin) {
        if (!IsCurrent(origin)) {
            return;
        }
        var closed = origin.Closed;
        var resources = Detach();
        long closing = generation;
        await ReleaseAsync(resources);
        if (!disposed && generation == closing && active is null) {
            await closed.InvokeAsync();
        }
    }

    public async Task ResetAsync() => await ReleaseAsync(Detach());

    private async ValueTask ReleaseAsync(params IAsyncDisposable?[] resources) {
        foreach (var resource in resources) {
            if (resource is null) {
                continue;
            }
            try {
                await resource.DisposeAsync();
            } catch (Exception exception) {
                logger.LogError(exception, "Project file resource cleanup is incomplete. ResourceType={ResourceType}.", resource.GetType().Name);
            }
        }
    }

    private void Report(Exception exception, string operation, ProjectFilesActivation origin)
        => logger.LogWarning(exception, "Project files {Operation} failed. Retired={Retired}.", operation, !IsCurrent(origin));

    private static string SafeMessage(Exception exception, string message) => exception switch {
        FileBrowserProviderException provider => provider.Error.Message,
        FileAccessDeniedException denied => denied.Message,
        _ => message
    };

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        disposed = true;
        await ResetAsync();
    }
}
