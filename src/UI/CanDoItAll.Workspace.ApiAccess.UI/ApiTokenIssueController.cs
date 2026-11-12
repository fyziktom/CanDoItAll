using System.Globalization;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.ApiAccess.UI;

public sealed class ApiTokenDraft {
    private string scopeText = string.Empty;
    public string Subject { get; set; } = "api-client";
    public string DisplayName { get; set; } = "API client";
    public string LifetimeText { get; set; } = string.Empty;
    public long ScopeRevision { get; private set; }
    public string ScopeText {
        get => scopeText;
        set {
            if (scopeText != value) {
                scopeText = value;
                ScopeRevision++;
            }
        }
    }
}

public sealed class ApiTokenIssueController : IDisposable {
    private readonly IApiTokenOwner owner;
    private readonly ApiViewLifetime authority;
    private readonly ApiOperationLedger ledger;
    private readonly ValidationMessageStore validation;
    private readonly ApiViewLifetime life;
    private string? bearer;
    private long observationRevision;
    private static readonly ApiWriteTarget Target = new(ApiTargetKind.Issuance);

    public ApiTokenIssueController(IApiTokenOwner owner, ApiAccessConfiguration configuration, ApiViewLifetime authority, ApiOperationLedger ledger) {
        this.owner = owner;
        this.authority = authority;
        this.ledger = ledger;
        Configuration = configuration;
        life = new(authority);
        life.Retired += ClearSensitive;
        Draft.LifetimeText = configuration.DefaultTokenLifetimeMinutes.ToString(CultureInfo.InvariantCulture);
        Draft.ScopeText = configuration.DefaultMachineScopes;
        Form = new(Draft);
        validation = new(Form);
    }

    public event Action? Changed;
    public ApiAccessConfiguration Configuration { get; }
    public ApiTokenDraft Draft { get; } = new();
    public EditContext Form { get; }
    public ApiWriteResult? Outcome { get; private set; }
    public ApiTokenMetadata? Issued { get; private set; }
    public ApiTokenMetadata? Observation { get; private set; }
    public string? ObservationMessage { get; private set; }
    public string? Error { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanIssue => life.IsActive && !IsBusy && !ledger.IsBlocked(Target);
    [JsonIgnore] public string? Disclosure => life.IsActive ? bearer : null;

    public ApiTokenListController OpenTokens() {
        var controller = new ApiTokenListController(owner, authority, ledger);
        controller.Changed += () => Changed?.Invoke();
        return controller;
    }

    public async Task IssueAsync() {
        if (!CanIssue) {
            return;
        }
        validation.Clear();
        Error = null;
        int? lifetime = null;
        if (Draft.LifetimeText.Length > 0) {
            if (!int.TryParse(Draft.LifetimeText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1 || parsed > Configuration.MaxTokenLifetimeMinutes) {
                validation.Add(new FieldIdentifier(Draft, nameof(Draft.LifetimeText)), $"Enter a whole number from 1 to {Configuration.MaxTokenLifetimeMinutes}, or leave the lifetime empty to use the configured default.");
                Form.NotifyValidationStateChanged();
                Changed?.Invoke();
                return;
            }
            lifetime = parsed;
        }
        Form.NotifyValidationStateChanged();
        var intent = new ApiTokenIntent(Draft.Subject, Draft.DisplayName, lifetime, Draft.ScopeText);
        var operation = ledger.Begin(Target, ApiWriteAction.IssueToken);
        if (operation is null) {
            Error = "The operation history is full of unresolved writes. Review them before issuing another token.";
            Changed?.Invoke();
            return;
        }
        IsBusy = true;
        bearer = null;
        Issued = null;
        Observation = null;
        ObservationMessage = null;
        Outcome = new(ApiWriteState.Pending);
        Changed?.Invoke();
        ApiWriteResult result;
        try {
            using var disclosure = await owner.IssueAsync(intent, life.Token);
            result = disclosure.Outcome;
            ledger.Complete(operation.Value, result);
            if (life.IsActive) {
                Outcome = result;
                if (result.IsCommitted) {
                    Issued = disclosure.Metadata;
                    bearer = disclosure.TakeValue();
                }
            }
        } catch (Exception) {
            result = new(ApiWriteState.Unknown, ApiFailure.UnknownAcknowledgement);
            ledger.Complete(operation.Value, result);
            if (life.IsActive) {
                Outcome = result;
            }
        }
        if (result.Failure == ApiFailure.Denied) {
            authority.Dispose();
        }
        if (life.IsActive) {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    public async Task ObserveAsync() {
        if (!life.IsActive || Outcome?.Identity is not { } id) {
            return;
        }
        var origin = Outcome;
        var revision = ++observationRevision;
        ObservationMessage = null;
        bool IsCurrent() => life.IsActive && revision == observationRevision && ReferenceEquals(origin, Outcome);
        try {
            var observed = await owner.ObserveAsync(id, life.Token);
            if (IsCurrent()) {
                Observation = observed;
                ObservationMessage = observed is null ? "No current record was found for this exact identity. This does not resolve the original acknowledgement."
                    : "Current metadata observed for the exact identity. This does not prove which request registered it or recover its bearer.";
            }
        } catch (UnauthorizedAccessException) {
            if (IsCurrent()) {
                authority.Dispose();
            }
        } catch (Exception) {
            if (IsCurrent()) {
                ObservationMessage = "The exact observation failed. The original outcome is unchanged.";
            }
        }
        if (life.IsActive) {
            Changed?.Invoke();
        }
    }

    public void Dismiss() {
        bearer = null;
        Changed?.Invoke();
    }
    private void ClearSensitive() => bearer = null;
    public void Dispose() {
        life.Dispose();
        Changed = null;
    }
}
