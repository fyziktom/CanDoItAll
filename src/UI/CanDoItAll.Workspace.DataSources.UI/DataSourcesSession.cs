using CanDoItAll.Modules.Workspace.DataSources.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.DataSources.UI;

public enum DataSourceSelectionCause { Operator, Owner }

public readonly record struct DataSourceSelection(Guid? ProfileId, DataSourceSelectionCause Cause);

public sealed class DataSourcesSession : IDisposable {
    private readonly IDataSourcesOwner owner;
    private readonly DataSourcesContext context;
    private readonly DataSourceReadLane listReads = new();
    private readonly DataSourceReadLane runtimeReads = new();
    private readonly DataSourceReadLane editorReads = new();
    private readonly DataSourceReadLane schemaReads = new();
    private bool disposed;
    private long editorRevision;
    private bool acquired;
    public DataSourcesSession(IDataSourcesOwner owner, DataSourceOperationLedger operations) {
        this.owner = owner;
        context = owner.Context;
        Operations = operations;
        EditContext = new(Draft);
        operations.Changed += Publish;
    }

    public event Action? Changed;
    public event Action<DataSourceSelection>? SelectionChanged;
    public DataSourceOperationLedger Operations { get; }
    public Guid? SelectedProfileId { get; private set; }
    public IReadOnlyList<ProfileSummary> Profiles { get; private set; } = [];
    public RuntimeSelection? CurrentSelection { get; private set; }
    public ProfileDraft Draft { get; private set; } = new();
    public EditContext EditContext { get; private set; }
    public string Password { get; set; } = string.Empty;
    public string Search { get; set; } = string.Empty;
    public string? ListError { get; private set; }
    public string? RuntimeError { get; private set; }
    public string? EditorError { get; private set; }
    public string? Message { get; private set; }
    public SchemaHealth? SelectedSchemaHealth { get; private set; }
    public DataSourceTransferSession? Transfer { get; private set; }
    public bool EditorLoading { get; private set; }
    public bool IsLocked => CurrentSelection is null || CurrentSelection.IsRuntimeLocked || Draft.IsRuntimeLocked;
    public bool CanMutate => !disposed && acquired && !IsLocked && Operations.CanDispatch && context == owner.Context;
    public bool IsActive => Draft.Id == CurrentSelection?.RuntimeProfileId;
    public bool IsPending => Draft.Id is not null && Draft.Id == CurrentSelection?.PendingRestartProfileId;
    public bool CanDelete => CanMutate && Draft.Id.HasValue && !IsActive && !IsPending;
    public IReadOnlyList<ProfileSummary> FilteredProfiles => Profiles.Where(profile => string.IsNullOrWhiteSpace(Search)
        || profile.DisplayName.Contains(Search, StringComparison.OrdinalIgnoreCase)
        || profile.Descriptor.Contains(Search, StringComparison.OrdinalIgnoreCase)).OrderBy(profile => profile.DisplayName).ToArray();

    public async Task InitializeAsync(Guid? profileId = null) {
        var revision = editorRevision;
        await RefreshAsync();
        if (disposed || revision != editorRevision) {
            return;
        }
        if (profileId is { } requested) {
            await SelectAsync(requested);
            return;
        }
        await SelectDefaultAsync();
    }

    public Task SelectDefaultAsync() => CurrentSelection is { } selection
        ? SelectCoreAsync(selection.RuntimeProfileId, false, DataSourceSelectionCause.Owner)
        : Task.CompletedTask;

    public Task RefreshAsync() => disposed ? Task.CompletedTask : Task.WhenAll(RefreshListAsync(), RefreshRuntimeAsync());

    private async Task RefreshListAsync() {
        using var read = listReads.Begin();
        try {
            var values = await owner.ListAsync(read.Token);
            if (read.IsCurrent && context == owner.Context) {
                Profiles = values.ToArray();
                ListError = null;
            }
        } catch (Exception exception) {
            if (read.IsCurrent) {
                ListError = ReadError(exception);
            }
        } finally {
            if (read.IsCurrent) {
                Publish();
            }
        }
    }

    private async Task RefreshRuntimeAsync() {
        using var read = runtimeReads.Begin();
        try {
            var selection = await owner.ReadRuntimeAsync(read.Token);
            if (read.IsCurrent && context == owner.Context) {
                CurrentSelection = selection;
                RuntimeError = null;
            }
        } catch (Exception exception) {
            if (read.IsCurrent) {
                RuntimeError = ReadError(exception);
                CurrentSelection = null;
            }
        } finally {
            if (read.IsCurrent) {
                Publish();
            }
        }
    }

    public Task SelectAsync(Guid id, bool reset = false) => SelectCoreAsync(id, reset, DataSourceSelectionCause.Operator);

    private async Task SelectCoreAsync(Guid id, bool reset, DataSourceSelectionCause cause) {
        if (disposed || acquired && Draft.Id == id && !reset) {
            return;
        }
        using var read = editorReads.Begin();
        var revision = ++editorRevision;
        acquired = false;
        EditorLoading = true;
        Password = string.Empty;
        EditorError = null;
        SelectedSchemaHealth = null;
        ChangeSelection(id, cause);
        Publish();
        try {
            var editor = await owner.ReadEditorAsync(id, read.Token);
            if (editor.Values.Id != id) {
                throw new DataSourcesException(DataSourceFailure.Missing);
            }
            if (read.IsCurrent && revision == editorRevision && context == owner.Context) {
                Draft = ProfileDraft.From(editor);
                EditContext = new(Draft);
                acquired = true;
                await RefreshSchemaAsync(id, revision);
            }
        } catch (Exception exception) {
            if (read.IsCurrent && revision == editorRevision) {
                EditorError = ReadError(exception);
            }
        } finally {
            if (read.IsCurrent && revision == editorRevision) {
                EditorLoading = false;
                Publish();
            }
        }
    }

    public void NewProfile() => NewProfile(DataSourceSelectionCause.Operator);

    private void NewProfile(DataSourceSelectionCause cause) {
        if (disposed || CurrentSelection is null || CurrentSelection.IsRuntimeLocked) {
            return;
        }
        editorRevision++;
        Draft = new() { WorkspaceRoot = CurrentSelection.WorkspaceRoot };
        EditContext = new(Draft);
        Password = string.Empty;
        EditorError = null;
        SelectedSchemaHealth = null;
        Message = null;
        EditorLoading = false;
        acquired = true;
        ChangeSelection(null, cause);
        Publish();
    }

    public Task ResetAsync() {
        if (IsLocked) {
            return Task.CompletedTask;
        }
        if (Draft.Id is { } id) {
            return SelectAsync(id, reset: true);
        }
        NewProfile();
        return Task.CompletedTask;
    }

    public async Task SaveAsync() {
        if (!CanMutate || !EditContext.Validate()) {
            return;
        }
        var draft = Draft;
        var revision = editorRevision;
        var request = draft.Capture();
        using var password = new ProfilePasswordIntent(Password);
        Password = string.Empty;
        var result = await Operations.RunAsync(context, DataSourceAction.Save, request.Id ?? Guid.Empty, null,
            () => owner.SaveAsync(context, request, password));
        if (result is null || disposed) {
            return;
        }
        if (revision == editorRevision && ReferenceEquals(draft, Draft)) {
            if (result.Outcome is DataSourceOutcome.Confirmed or DataSourceOutcome.Partial && result.ProfileId != Guid.Empty) {
                Draft.Id = result.ProfileId;
                ChangeSelection(result.ProfileId, DataSourceSelectionCause.Owner);
            }
            Message = result.Message;
        }
        await RefreshAsync();
        Publish();
    }

    public async Task ExecuteAsync(DataSourceAction action) {
        if (!CanMutate || Draft.Id is not { } id || action is DataSourceAction.Save or DataSourceAction.Transfer
            || action == DataSourceAction.Delete && !CanDelete
            || action == DataSourceAction.ActivateForRestart && (IsActive || IsPending || SelectedSchemaHealth?.Status != SchemaStatus.Current)
            || action == DataSourceAction.ApplySchema && SelectedSchemaHealth?.CanApplySchema != true) {
            return;
        }
        var revision = editorRevision;
        var result = await Operations.RunAsync(context, action, id, null, () => owner.ExecuteAsync(new(context, id, action)));
        if (result is null || disposed) {
            return;
        }
        if (revision == editorRevision) {
            Message = result.Message;
            if (action == DataSourceAction.Delete && result.Outcome == DataSourceOutcome.Confirmed) {
                NewProfile(DataSourceSelectionCause.Owner);
                Message = result.Message;
            }
        }
        await RefreshAsync();
        if (!disposed && revision == editorRevision && Draft.Id == id) {
            await RefreshSchemaAsync(id, revision);
            if (action == DataSourceAction.CreateEmpty && result.Outcome == DataSourceOutcome.Confirmed) {
                await OpenTransferAsync(id);
            }
        }
        Publish();
    }

    public async Task RefreshSelectedSchemaAsync() {
        if (!disposed && Draft.Id is { } id && acquired) {
            await RefreshSchemaAsync(id, editorRevision);
        }
    }

    private async Task RefreshSchemaAsync(Guid id, long revision) {
        using var read = schemaReads.Begin();
        try {
            var health = await owner.ReadSchemaAsync(id, read.Token);
            if (read.IsCurrent && revision == editorRevision && Draft.Id == id) {
                SelectedSchemaHealth = health.ProfileId == id ? health : null;
            }
        } catch {
            if (read.IsCurrent && revision == editorRevision) {
                SelectedSchemaHealth = new(id, SchemaStatus.Unavailable, "Schema observation failed. No healthy state is assumed.", [], [], false);
            }
        } finally {
            if (read.IsCurrent) {
                Publish();
            }
        }
    }

    public async Task OpenTransferAsync(Guid targetId) {
        if (disposed || IsLocked || !acquired || Draft.Id != targetId) {
            return;
        }
        CloseTransfer();
        var target = Profiles.SingleOrDefault(profile => profile.Id == targetId);
        if (target is null) {
            return;
        }
        var transfer = new DataSourceTransferSession(owner, Operations, target, context);
        transfer.Changed += Publish;
        Transfer = transfer;
        Publish();
        await transfer.InitializeAsync(CurrentSelection?.RuntimeProfileId);
    }

    public void CloseTransfer() {
        if (Transfer is { } previous) {
            previous.Changed -= Publish;
            previous.Dispose();
            Transfer = null;
            Publish();
        }
    }

    internal static string ReadError(Exception exception) => DataSourceOperationLedger.Describe(
        exception is DataSourcesException failure ? failure.Failure : DataSourceFailure.Unavailable);
    private void ChangeSelection(Guid? id, DataSourceSelectionCause cause) {
        if (disposed || SelectedProfileId == id) {
            return;
        }
        SelectedProfileId = id;
        SelectionChanged?.Invoke(new(id, cause));
    }
    private void Publish() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        disposed = true;
        SelectionChanged = null;
        Operations.Changed -= Publish;
        CloseTransfer();
        Password = string.Empty;
        listReads.Dispose();
        runtimeReads.Dispose();
        editorReads.Dispose();
        schemaReads.Dispose();
    }
}
