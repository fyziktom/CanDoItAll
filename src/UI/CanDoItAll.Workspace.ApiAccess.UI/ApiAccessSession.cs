using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed class ApiAccessSession(
    IApiAccessConfigurationOwner configurationOwner,
    IApiTokenOwner tokens,
    IApiAccountOwner accounts,
    ApiOperationLedger ledger) : IDisposable {
    private readonly ApiViewLifetime life = new();
    private ApiViewLifetime? authority;
    private CancellationTokenSource? reading;
    private long revision;
    private long observationRevision;

    public event Action? Changed;
    public ApiAccessConfiguration? Configuration { get; private set; }
    public bool StatusLoading { get; private set; }
    public bool ManagementLoading { get; private set; }
    public ApiFailure ManagementFailure { get; private set; }
    public ApiTokenIssueController? Issuance { get; private set; }
    public ApiAccountController? Accounts { get; private set; }
    public IReadOnlyList<ApiOperationReceipt> Receipts => ledger.Receipts;
    public string? ObservationMessage { get; private set; }

    public async Task LoadAsync() {
        if (!life.IsActive) {
            return;
        }
        RetireChildren();
        reading?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
        var token = cancellation.Token;
        reading = cancellation;
        var origin = ++revision;
        Configuration = null;
        StatusLoading = true;
        ManagementLoading = true;
        ManagementFailure = ApiFailure.None;
        ObservationMessage = null;
        Notify();
        try {
            await Task.WhenAll(ReadConfigurationAsync(), ReadAccessAsync());
        } finally {
            if (ReferenceEquals(reading, cancellation)) {
                reading = null;
            }
        }
        if (!Current(origin)) {
            return;
        }
        if (Configuration is { AuthorizationEnabled: true, SigningKeyConfigured: true } configuration && ManagementFailure == ApiFailure.None) {
            authority = new(life);
            authority.Retired += AccessRetired;
            Issuance = new(tokens, configuration, authority, ledger);
            Accounts = new(accounts, configuration, authority, ledger);
            Issuance.Changed += Notify;
            Accounts.Changed += Notify;
            Notify();
            await Accounts.Page.RefreshAsync();
        }

        async Task ReadConfigurationAsync() {
            try {
                var value = await configurationOwner.ReadAsync(token);
                if (Current(origin)) {
                    Configuration = value;
                }
            } catch (Exception) {
                if (Current(origin)) {
                    Configuration = null;
                }
            } finally {
                if (Current(origin)) {
                    StatusLoading = false;
                    Notify();
                }
            }
        }
        async Task ReadAccessAsync() {
            try {
                var allowed = await configurationOwner.CanManageAsync(token);
                if (Current(origin)) {
                    ManagementFailure = allowed ? ApiFailure.None : ApiFailure.Denied;
                }
            } catch (Exception) {
                if (Current(origin)) {
                    ManagementFailure = ApiFailure.Unavailable;
                }
            } finally {
                if (Current(origin)) {
                    ManagementLoading = false;
                    Notify();
                }
            }
        }
    }

    public async Task ObserveAsync(Guid operationId) {
        if (authority is not { IsActive: true } current || Receipts.FirstOrDefault(receipt => receipt.OperationId == operationId) is not { Result.Identity: { } id } receipt) {
            return;
        }
        var origin = revision;
        var observation = ++observationRevision;
        ObservationMessage = null;
        Notify();
        bool IsCurrent() => Current(origin) && observation == observationRevision && current.IsActive;
        try {
            var exists = receipt.Action is ApiWriteAction.IssueToken or ApiWriteAction.RevokeToken or ApiWriteAction.DeleteToken
                ? await tokens.ObserveAsync(id, current.Token) is not null
                : await accounts.ObserveAsync(id, current.Token) is not null;
            if (IsCurrent()) {
                ObservationMessage = $"Exact identity {id} is currently {(exists ? "present" : "absent")}. Original outcome: {receipt.Result.State}. Observation does not establish causality or unlock this write.";
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent()) {
                current.Dispose();
            }
        } catch (Exception) {
            if (IsCurrent()) {
                ObservationMessage = "Exact observation failed. The original outcome is unchanged.";
            }
        }
        Notify();
    }

    private bool Current(long origin) => life.IsActive && origin == revision;
    private void AccessRetired() {
        ManagementFailure = ApiFailure.Denied;
        RetireChildren();
        Notify();
    }
    private void RetireChildren() {
        if (authority is { } retiring) {
            authority = null;
            retiring.Retired -= AccessRetired;
            retiring.Dispose();
        }
        Issuance?.Dispose();
        Accounts?.Dispose();
        Issuance = null;
        Accounts = null;
    }
    private void Notify() {
        if (life.IsActive) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        if (!life.IsActive) {
            return;
        }
        RetireChildren();
        life.Dispose();
        reading?.Cancel();
        reading = null;
        StatusLoading = false;
        ManagementLoading = false;
        Changed = null;
    }
}
