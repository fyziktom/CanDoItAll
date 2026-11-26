using CanDoItAll.Modules.Security;
using CanDoItAll.RemoteInstances;
using CanDoItAll.Workspace.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace.Pages.Components;

public partial class RemoteInstancePicker : IAsyncDisposable {
    [Inject] private IRemoteInstanceRegistry Registry { get; set; } = default!;
    [Inject] private IRemoteInstanceEditing Editing { get; set; } = default!;
    [Inject] private IWorkspaceSecretsOwner Secrets { get; set; } = default!;
    [Inject] private ILogger<RemoteInstancePicker> Logger { get; set; } = default!;
    [Parameter] public bool ManageInstances { get; set; }
    [Parameter] public EventCallback<RemoteInstanceSelection?> SelectedChanged { get; set; }

    private readonly CancellationTokenSource lifetime = new();
    private CancellationToken token;
    private IRemoteInstanceOpening opening = default!;
    private IReadOnlyList<RemoteInstanceConnection> instances = [];
    private IReadOnlyList<SecretListItem> credentials = [];
    private RemoteInstanceDraft? editor;
    private RemoteInstanceConnection? deleting;
    private RemoteInstanceSelection? published;
    private Guid? selectedId;
    private string? error;
    private string? warning;
    private string? editorError;
    private long readGeneration;
    private bool available;
    private bool loading;
    private bool writing;
    private bool retired;
    private bool needsReview;
    private bool credentialsAvailable;
    private bool Blocked => retired || !available || loading || writing;

    protected override async Task OnInitializedAsync() {
        token = lifetime.Token;
        if (!RendererInfo.IsInteractive) {
            return;
        }
        opening = Editing.Open(Guid.NewGuid(), () => !retired);
        try {
            await opening.StartAsync(() => InvokeAsync(RefreshAndRenderAsync), () => InvokeAsync(RetireAsync));
            await RefreshAsync();
        } catch (UnauthorizedAccessException) {
            await RetireAsync();
        }
    }

    private async Task RefreshAndRenderAsync() {
        await RefreshAsync();
        if (!retired) {
            StateHasChanged();
        }
    }

    private async Task RefreshAsync() {
        if (retired || opening is null) {
            return;
        }
        var generation = ++readGeneration;
        loading = true;
        try {
            var rows = await opening.ReadAsync(Registry.ListAsync, token);
            if (retired || generation != readGeneration) {
                return;
            }
            instances = rows;
            available = true;
            error = null;
            if (editor is { ExpectedVersion: { } version } draft && !HasVersion(draft.Id, version)) {
                needsReview = true;
                editorError = "This instance changed or was removed. Close the editor and review the current connection before saving.";
            }
            if (deleting is { } target && !HasVersion(target.Id, target.ConcurrencyToken)) {
                deleting = null;
                error = "This instance changed. Review the current connection before deleting.";
            }
            await PublishSelectionAsync();
        } catch (OperationCanceledException) when (retired) {
        } catch (UnauthorizedAccessException) {
            await RetireAsync();
        } catch (Exception exception) {
            if (retired || generation != readGeneration) {
                return;
            }
            Logger.LogWarning("Remote instance list read failed ({ExceptionType}).", exception.GetType().Name);
            available = false;
            error = "The remote instance list could not be refreshed. Refresh to retry.";
            await PublishSelectionAsync();
        } finally {
            if (generation == readGeneration) {
                loading = false;
            }
        }
    }

    private bool HasVersion(Guid id, Guid version) => instances.Any(item => item.Id == id && item.ConcurrencyToken == version);

    private async Task SelectAsync(Guid? id) {
        if (Blocked || id.HasValue && !instances.Any(row => row.Id == id && (ManageInstances || row.IsEnabled))) {
            return;
        }
        selectedId = id;
        await PublishSelectionAsync();
    }

    private async Task PublishSelectionAsync() {
        var row = instances.SingleOrDefault(item => item.Id == selectedId);
        if (available && (row is null || !ManageInstances && !row.IsEnabled)) {
            selectedId = null;
        }
        var next = available && row is { IsEnabled: true } ? new RemoteInstanceSelection(row.Id, row.ConcurrencyToken) : null;
        if (published == next) {
            return;
        }
        published = next;
        await SelectedChanged.InvokeAsync(next);
    }

    private async Task OpenAsync(RemoteInstanceConnection? row) {
        if (Blocked || editor is not null || row is not null && !HasVersion(row.Id, row.ConcurrencyToken)) {
            return;
        }
        var draft = new RemoteInstanceDraft(row);
        editor = draft;
        editorError = null;
        needsReview = false;
        credentialsAvailable = false;
        credentials = [];
        try {
            var choices = await opening.ReadAsync(Secrets.ListAsync, token);
            if (editor == draft && !retired) {
                credentials = choices;
                credentialsAvailable = true;
            }
        } catch (OperationCanceledException) when (retired) {
        } catch (UnauthorizedAccessException) {
            await RetireAsync();
        } catch (Exception exception) {
            Logger.LogWarning("Remote instance credential choices could not be read ({ExceptionType}).", exception.GetType().Name);
            if (editor == draft) {
                editorError = "Stored credentials could not be loaded. Reopen the editor to retry.";
            }
        }
    }

    private void CloseEditor() {
        if (!writing) {
            editor = null;
        }
    }

    private async Task SaveAsync(RemoteInstanceDraft draft) {
        if (Blocked || editor != draft || needsReview || !credentialsAvailable) {
            return;
        }
        if (string.IsNullOrWhiteSpace(draft.Name) || !Uri.TryCreate(draft.Address.Trim(), UriKind.Absolute, out var address) ||
            !credentials.Any(choice => choice.Id == draft.CredentialId)) {
            editorError = "Enter an instance name, an absolute HTTP or HTTPS base URL, and choose a stored credential.";
            return;
        }
        var settings = new RemoteInstanceSettings(draft.Name, address, draft.CredentialId, draft.Enabled, draft.AllowPrivateNetwork);
        writing = true;
        try {
            var saved = await opening.WriteAsync(
                () => draft.ExpectedVersion is { } version ? Registry.UpdateAsync(draft.Id, version, settings, token) : Registry.CreateAsync(draft.Id, settings, token),
                () => !retired && editor == draft && !needsReview, token);
            if (retired) {
                return;
            }
            selectedId = saved.Id;
            warning = saved.Warning;
            editor = null;
            await RefreshAsync();
        } catch (OperationCanceledException) when (retired) {
        } catch (UnauthorizedAccessException) {
            await RetireAsync();
        } catch (Exception exception) when (exception is RemoteInstanceConflictException or KeyNotFoundException) {
            needsReview = true;
            editorError = "The connection or its credential changed. Close the editor, refresh and review the current values.";
        } catch (ArgumentException) {
            editorError = "Check the instance name, credential, base URL and private network setting. Use an HTTP or HTTPS URL without a password, query or fragment.";
        } catch (Exception exception) {
            Logger.LogWarning("Remote instance {InstanceId} save outcome is unknown ({ExceptionType}).", draft.Id, exception.GetType().Name);
            needsReview = true;
            available = false;
            editorError = "The save outcome is unknown. Close the editor and refresh the list to check the connection.";
            await PublishSelectionAsync();
        } finally {
            writing = false;
        }
    }

    private void ConfirmDeletion(RemoteInstanceConnection row) {
        if (ManageInstances && !Blocked && HasVersion(row.Id, row.ConcurrencyToken)) {
            deleting = row;
        }
    }

    private void CloseDelete() {
        if (!writing) {
            deleting = null;
        }
    }

    private async Task DeleteAsync(RemoteInstanceConnection target) {
        if (Blocked || deleting != target) {
            return;
        }
        writing = true;
        try {
            await opening.WriteAsync(async () => {
                await Registry.DeleteAsync(target.Id, target.ConcurrencyToken, token);
                return target.Id;
            }, () => !retired && deleting == target, token);
            if (!retired) {
                deleting = null;
                warning = null;
                await RefreshAsync();
            }
        } catch (OperationCanceledException) when (retired) {
        } catch (UnauthorizedAccessException) {
            await RetireAsync();
        } catch (RemoteInstanceInUseException) {
            error = "This connection has retained imports. Remove its dependent imports before deleting the connection.";
        } catch (Exception exception) when (exception is RemoteInstanceConflictException or KeyNotFoundException) {
            error = "This instance was changed or removed. Refresh the list before trying again.";
            available = false;
            await PublishSelectionAsync();
        } catch (Exception exception) {
            Logger.LogWarning("Remote instance {InstanceId} deletion outcome is unknown ({ExceptionType}).", target.Id, exception.GetType().Name);
            available = false;
            error = "The deletion outcome is unknown. Refresh the list to check whether the instance still exists.";
            await PublishSelectionAsync();
        } finally {
            writing = false;
            deleting = null;
        }
    }

    private async Task RetireAsync() {
        if (retired) {
            return;
        }
        retired = true;
        available = false;
        opening?.Stop();
        instances = [];
        credentials = [];
        editor = null;
        deleting = null;
        warning = null;
        error = "Access to these remote instances changed. Reopen the page to use the current workspace.";
        await PublishSelectionAsync();
        StateHasChanged();
    }

    public async ValueTask DisposeAsync() {
        retired = true;
        lifetime.Cancel();
        if (opening is not null) {
            await opening.DisposeAsync();
        }
        lifetime.Dispose();
    }
}
