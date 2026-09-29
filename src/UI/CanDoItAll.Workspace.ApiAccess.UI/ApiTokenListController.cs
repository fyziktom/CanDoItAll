using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed record ApiTokenConfirmation(Guid Origin, ApiTokenMetadata Token, ApiWriteAction Action);

public sealed class ApiTokenListController : IDisposable {
    private readonly IApiTokenOwner owner;
    private readonly ApiViewLifetime authority;
    private readonly ApiViewLifetime life;
    private readonly ApiOperationLedger ledger;
    private long observationRevision;
    private Guid? outcomeOperation;

    public ApiTokenListController(IApiTokenOwner owner, ApiViewLifetime authority, ApiOperationLedger ledger) {
        this.owner = owner;
        this.authority = authority;
        this.ledger = ledger;
        life = new(authority);
        Page = new(owner.SearchAsync, life);
        Page.Changed += Notify;
    }

    public event Action? Changed;
    public ApiPageController<ApiTokenMetadata> Page { get; }
    public ApiTokenConfirmation? Confirmation { get; private set; }
    public ApiWriteResult? Outcome { get; private set; }
    public string? Error { get; private set; }
    public string? ObservationMessage { get; private set; }
    public bool ConfirmationBlocked => Confirmation is null || !life.IsActive || ledger.IsBlocked(Target(Confirmation.Token.Id));

    public bool CanAct(ApiTokenMetadata token, ApiWriteAction action) => life.IsActive && !Page.IsStale &&
        token.Kind == ApiTokenCategory.Machine && Page.Page?.Items.Any(item => item.Id == token.Id && item.Kind == token.Kind) == true &&
        !ledger.IsBlocked(Target(token.Id)) && (action == ApiWriteAction.DeleteToken ||
            action == ApiWriteAction.RevokeToken && token.GetStatus(DateTimeOffset.UtcNow) == ApiCredentialStatus.Active);

    public void Confirm(ApiTokenMetadata token, ApiWriteAction action) {
        if (!CanAct(token, action)) {
            return;
        }
        Confirmation = new(Guid.NewGuid(), token, action);
        Error = null;
        Notify();
    }

    public void CancelConfirmation(Guid origin) {
        if (Confirmation?.Origin == origin) {
            Confirmation = null;
            Notify();
        }
    }

    public async Task ApplyAsync(Guid origin) {
        if (!life.IsActive || Confirmation is not { } confirmation || confirmation.Origin != origin || ConfirmationBlocked) {
            return;
        }
        var operation = ledger.Begin(Target(confirmation.Token.Id), confirmation.Action);
        if (operation is null) {
            Error = "Unresolved operations fill the history. Review them before another write.";
            Notify();
            return;
        }
        var readRevision = Page.Revision;
        outcomeOperation = operation;
        ObservationMessage = null;
        Outcome = new(ApiWriteState.Pending, Identity: confirmation.Token.Id);
        Notify();
        ApiWriteResult result;
        try {
            result = await owner.ApplyAsync(new(confirmation.Token.Id, confirmation.Token.Kind, confirmation.Action), life.Token);
        } catch (Exception) {
            result = new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement, confirmation.Token.Id);
        }
        ledger.Complete(operation.Value, result);
        if (life.IsActive && result.Failure == ApiFailure.Denied) {
            authority.Dispose();
        }
        if (!life.IsActive) {
            return;
        }
        if (outcomeOperation == operation) {
            Outcome = result;
        }
        if (ReferenceEquals(Confirmation, confirmation)) {
            if (result.State != ApiWriteState.Unknown) {
                Confirmation = null;
            }
        }
        Notify();
        if (result.IsCommitted) {
            await Page.RefreshIfCurrentAsync(readRevision);
        }
    }

    public async Task ObserveAsync() {
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
                ObservationMessage = observed is null ? "No current record was found for the exact identity. The original outcome is unchanged."
                    : $"Current exact record: {observed.Id}, {observed.GetStatus(DateTimeOffset.UtcNow)}. Observation does not establish the original request's outcome.";
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

    private static ApiWriteTarget Target(Guid id) => new(ApiTargetKind.Token, id);
    private void Notify() {
        if (life.IsActive) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        life.Dispose();
        Page.Dispose();
        Confirmation = null;
        Changed = null;
    }
}
