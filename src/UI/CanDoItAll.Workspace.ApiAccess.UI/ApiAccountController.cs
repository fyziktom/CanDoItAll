using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed class ApiAccountController : IDisposable {
    private readonly IApiAccountOwner owner;
    private readonly ApiViewLifetime authority;
    private readonly ApiViewLifetime life;
    private readonly ApiOperationLedger ledger;
    private long editorRevision;
    private long observationRevision;
    private Guid? outcomeOperation;
    private CancellationTokenSource? editorRead;

    public ApiAccountController(IApiAccountOwner owner, ApiAccessConfiguration configuration, ApiViewLifetime authority, ApiOperationLedger ledger) {
        this.owner = owner;
        this.authority = authority;
        this.ledger = ledger;
        Configuration = configuration;
        life = new(authority);
        life.Retired += RetireEditor;
        Page = new(owner.SearchAsync, life);
        Page.Changed += Notify;
    }

    public event Action? Changed;
    public ApiAccessConfiguration Configuration { get; }
    public ApiPageController<ApiAccountMetadata> Page { get; }
    public ApiAccountDraft? Editor { get; private set; }
    public bool EditorLoading { get; private set; }
    public string? EditorError { get; private set; }
    public ApiWriteResult? Outcome { get; private set; }
    public ApiAccountMetadata? SavedAccount { get; private set; }
    public ApiAccountMetadata? Observation { get; private set; }
    public string? ObservationMessage { get; private set; }
    public bool CanSave => life.IsActive && Editor is { Pending: false, Unknown: false } draft && !ledger.IsBlocked(Target(draft));

    public void Create() {
        if (!life.IsActive) {
            return;
        }
        RetireEditor();
        Editor = new(ApiWriteAction.CreateAccount);
        EditorError = null;
        Notify();
    }

    public async Task OpenAsync(Guid id, ApiWriteAction action) {
        if (!life.IsActive || action is not (ApiWriteAction.UpdateAccount or ApiWriteAction.ResetPassword or ApiWriteAction.DeleteAccount)) {
            return;
        }
        RetireEditor();
        var revision = editorRevision;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
        editorRead = cancellation;
        EditorLoading = true;
        EditorError = null;
        Notify();
        try {
            var account = await owner.ObserveAsync(id, cancellation.Token);
            if (CurrentEditorRead(revision)) {
                if (account is null) {
                    EditorError = "The selected account no longer exists. Refresh the list or retry the exact account.";
                } else {
                    Editor = new(action, account);
                }
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (UnauthorizedAccessException) {
            if (CurrentEditorRead(revision)) {
                authority.Dispose();
            }
        } catch (Exception) {
            if (CurrentEditorRead(revision)) {
                EditorError = "The exact account could not be loaded. Retry this account before editing.";
            }
        } finally {
            if (CurrentEditorRead(revision)) {
                EditorLoading = false;
                editorRead = null;
                Notify();
            }
        }
    }

    public void Close(Guid origin) {
        if (Editor?.Origin == origin) {
            RetireEditor();
            Notify();
        }
    }

    public async Task SaveAsync(Guid origin) {
        if (!CanSave || Editor is not { } draft || draft.Origin != origin) {
            return;
        }
        EditorError = null;
        if (draft.NeedsPassword && draft.Password.Length is < 12 or > 256) {
            EditorError = "Passwords must contain 12–256 characters; spaces are preserved.";
            Notify();
            return;
        }
        if (draft.EditsProfile && (draft.UserName.Length is < 1 or > 64 || draft.DisplayName.Length is < 1 or > 128)) {
            EditorError = "Enter a username of 1–64 characters and a display name of 1–128 characters.";
            Notify();
            return;
        }
        using var intent = draft.Capture();
        var operation = ledger.Begin(Target(draft), intent.Action);
        if (operation is null) {
            EditorError = "Unresolved operations fill the history. Review them before another write.";
            Notify();
            return;
        }
        var draftRevision = draft.Revision;
        var readRevision = Page.Revision;
        outcomeOperation = operation;
        ObservationMessage = null;
        Observation = null;
        draft.Pending = true;
        Outcome = new(ApiWriteState.Pending, Identity: draft.Id, Version: draft.Version);
        Notify();
        ApiAccountWriteResult result;
        try {
            result = await owner.ApplyAsync(intent, life.Token);
        } catch (Exception) {
            result = new(new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, draft.Id));
        }
        draft.ClearPassword();
        ledger.Complete(operation.Value, result.Outcome);
        if (result.Outcome.Failure == ApiFailure.Denied) {
            authority.Dispose();
        }
        if (!life.IsActive) {
            return;
        }
        if (outcomeOperation == operation) {
            Outcome = result.Outcome;
            if (result.Outcome.IsCommitted) {
                SavedAccount = result.Account;
            }
        }
        if (ReferenceEquals(Editor, draft)) {
            draft.Pending = false;
            draft.Unknown = result.Outcome.State == ApiWriteState.Unknown;
            if (result.Outcome.IsCommitted) {
                if (result.Account is not null) {
                    draft.Accept(result.Account);
                }
                if (draft.Revision == draftRevision || intent.Action == ApiWriteAction.DeleteAccount) {
                    RetireEditor();
                }
            } else {
                EditorError = ApiOutcomeText.Describe(result.Outcome);
            }
        }
        Notify();
        if (result.Outcome.IsCommitted) {
            await Page.RefreshIfCurrentAsync(readRevision);
        }
    }

    public async Task ObserveOutcomeAsync() {
        if (!life.IsActive || Outcome?.Identity is not { } id) {
            return;
        }
        var revision = ++observationRevision;
        var outcome = Outcome;
        ObservationMessage = null;
        bool IsCurrent() => life.IsActive && revision == observationRevision && ReferenceEquals(outcome, Outcome);
        try {
            var observed = await owner.ObserveAsync(id, life.Token);
            if (IsCurrent()) {
                Observation = observed;
                ObservationMessage = observed is null ? "No current account was found for this exact identity. The original outcome is unchanged."
                    : $"Observed current account {observed.Id} at version {observed.Version}. This does not prove the original write committed.";
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent()) {
                authority.Dispose();
            }
        } catch (Exception) {
            if (IsCurrent()) {
                ObservationMessage = "Exact observation failed. The original outcome is unchanged.";
            }
        }
        Notify();
    }

    private static ApiWriteTarget Target(ApiAccountDraft draft) => draft.Id is { } id
        ? new(ApiTargetKind.Account, id) : new(ApiTargetKind.NewAccount, UserName: draft.UserName.ToUpperInvariant());
    private bool CurrentEditorRead(long revision) => life.IsActive && revision == editorRevision;
    private void RetireEditor() {
        editorRevision++;
        editorRead?.Cancel();
        editorRead = null;
        Editor?.ClearPassword();
        Editor = null;
        EditorLoading = false;
    }
    private void Notify() {
        if (life.IsActive) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        life.Dispose();
        Page.Dispose();
        Changed = null;
    }
}
