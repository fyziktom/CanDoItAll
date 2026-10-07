using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Components.Layout;

public partial class MainLayout {
    private readonly CancellationTokenSource layoutLifetime = new();
    private readonly string databaseSwitchListenerId = Guid.NewGuid().ToString("N");
    private ResolvedDatabaseProfile originalProfile = default!;
    private CancellationTokenSource? navigationCancellation;
    private Task listenerRegistration = Task.CompletedTask;
    private bool listenerRegistrationStarted;
    private bool workbenchInitialized;
    private int pendingLayoutWork;
    private long navigationGeneration;
    private bool layoutInitializationFailed;
    private bool layoutInitializationBusy;
    internal Task NavigationCompletion { get; private set; } = Task.CompletedTask;

    [Inject]
    private IActiveDatabaseProfileResolver LayoutProfileResolver { get; set; } = default!;

    private bool IsLayoutCurrent {
        get {
            if (collaborationDisposed) {
                return false;
            }
            var current = LayoutProfileResolver.ResolveCurrentProfile();
            return current.Profile.Id == originalProfile.Profile.Id &&
                string.Equals(current.Profile.Runtime.Fingerprint, originalProfile.Profile.Runtime.Fingerprint, StringComparison.Ordinal);
        }
    }

    private void EnsureLayoutCurrent() {
        if (!IsLayoutCurrent) {
            throw new OperationCanceledException("The initiating layout or runtime profile has retired.");
        }
    }

    private async Task RunLayoutWorkAsync(string operation, Func<Task> work) {
        if (!IsLayoutCurrent) {
            return;
        }
        pendingLayoutWork++;
        try {
            await work();
        } catch (OperationCanceledException) when (!IsLayoutCurrent) {
        } catch (JSDisconnectedException) {
            CollaborationLogger.LogInformation("Layout operation {Operation} lost its circuit; the browser effect was not acknowledged.", operation);
            Dispose();
        } catch (Exception exception) {
            CollaborationLogger.LogError(exception, "Layout operation {Operation} failed. ProfileId={ProfileId}; browser effects remain unacknowledged.", operation, originalProfile.Profile.Id);
            if (IsLayoutCurrent && operation == nameof(InitializeLayoutAsync)) {
                layoutInitializationFailed = true;
                StateHasChanged();
            }
        } finally {
            pendingLayoutWork--;
            if (collaborationDisposed && pendingLayoutWork == 0) {
                layoutLifetime.Dispose();
            }
        }
    }

    private async Task RegisterDatabaseSwitchListenerAsync() {
        EnsureLayoutCurrent();
        if (listenerRegistrationStarted && listenerRegistration.IsCompletedSuccessfully) {
            return;
        }
        databaseSwitchListenerReference?.Dispose();
        databaseSwitchListenerReference = DotNetObjectReference.Create(this);
        listenerRegistrationStarted = true;
        listenerRegistration = JS.InvokeVoidAsync("CanDoItAll.browserState.registerDatabaseSwitchListener",
            databaseSwitchListenerId, databaseSwitchListenerReference).AsTask();
        await listenerRegistration;
        EnsureLayoutCurrent();
    }

    private async Task ReleaseDatabaseSwitchListenerAsync() {
        var ownedReference = databaseSwitchListenerReference;
        databaseSwitchListenerReference = null;
        try {
            if (listenerRegistrationStarted) {
                try {
                    await listenerRegistration;
                } finally {
                    await JS.InvokeVoidAsync("CanDoItAll.browserState.unregisterDatabaseSwitchListener", databaseSwitchListenerId);
                }
            }
        } catch (Exception exception) when (exception is JSDisconnectedException or OperationCanceledException) {
        } catch (Exception exception) {
            CollaborationLogger.LogWarning(exception, "The retired layout could not release its browser listener. ListenerId={ListenerId}.", databaseSwitchListenerId);
        } finally {
            ownedReference?.Dispose();
        }
    }

    private sealed record NavigationRead(Uri Uri, long Generation, CancellationTokenSource Cancellation);

    private Task RetryLayoutInitializationAsync()
        => RunLayoutWorkAsync(nameof(InitializeLayoutAsync), InitializeLayoutAsync);

    private NavigationRead BeginNavigationRead() {
        navigationCancellation?.Cancel();
        navigationCancellation = CancellationTokenSource.CreateLinkedTokenSource(layoutLifetime.Token);
        return new(CurrentUri, ++navigationGeneration, navigationCancellation);
    }

    private bool IsCurrent(NavigationRead read) => IsLayoutCurrent && !read.Cancellation.IsCancellationRequested &&
        read.Generation == navigationGeneration && read.Uri == CurrentUri;

    private void EnsureCurrent(NavigationRead read) {
        if (!IsCurrent(read)) {
            throw new OperationCanceledException(read.Cancellation.Token);
        }
    }

    private async Task TrackNavigationAsync(NavigationRead read, bool pruneDeletedTabs = false) {
        try {
            EnsureCurrent(read);
            activeWorkspaceId = ResolveWorkspaceId(read.Uri.AbsolutePath);
            if (pruneDeletedTabs) {
                await CloseDeletedProjectTabsAsync(read);
            }
            EnsureCurrent(read);
            var descriptor = await ResolveCurrentTabDescriptorAsync(read);
            EnsureCurrent(read);
            await Workbench.TrackTabAsync(descriptor, read.Cancellation.Token);
            EnsureCurrent(read);
            StateHasChanged();
        } catch (OperationCanceledException) when (!IsCurrent(read)) {
        } finally {
            if (ReferenceEquals(navigationCancellation, read.Cancellation)) {
                navigationCancellation = null;
            }
            read.Cancellation.Dispose();
        }
    }
}
