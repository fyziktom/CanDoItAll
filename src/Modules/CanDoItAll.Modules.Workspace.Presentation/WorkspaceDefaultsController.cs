using CanDoItAll.Workspace.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultsController(IWorkspaceDefaultsOwner owner) : IWorkspaceDefaultsState, IDisposable {
    private readonly SettingsOperationLedger ledger = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    private bool acquired;
    private Task? refresh;
    private Task? currentRead;
    private bool readingCurrent;
    private bool requiresReconciliation;
    private string? defaultsError;
    private string? providerError;
    public event Action? Changed;
    public WorkspaceDefaultsDraft Draft { get; private set; } = new();
    public WorkspaceDefaultsDraft? PreviousDraft { get; private set; }
    public WorkspaceSettingsSnapshotV2? CurrentSnapshot { get; private set; }
    public WorkspaceSettingsSnapshot? LastAccepted { get; private set; }
    public WorkspaceSettingsSnapshotV2? LastAcceptedV2 { get; private set; }
    public WorkspaceSettingsRejectionReason? Rejection { get; private set; }
    public IReadOnlyList<WorkspaceProviderOption> Providers { get; private set; } = [];
    public IReadOnlyList<SettingsReceipt> Receipts => ledger.Entries;
    public bool CanSave => !disposed && owner.IsCurrent && acquired && !requiresReconciliation &&
        Rejection is not (WorkspaceSettingsRejectionReason.HeadChanged or WorkspaceSettingsRejectionReason.DatasetChanged or WorkspaceSettingsRejectionReason.OwnerRetired) &&
        !ledger.Blocks(Draft.Origin);
    public bool CanRecover => !disposed && owner.IsCurrent && acquired && CurrentSnapshot is not null &&
        !readingCurrent && !ledger.Blocks(Draft.Origin);
    public bool IsLoading { get; private set; }
    public string? Error => defaultsError ?? providerError;

    public Task RefreshAsync() {
        if (disposed || !owner.IsCurrent) {
            return Task.CompletedTask;
        }
        return refresh is { IsCompleted: false } pending ? pending : refresh = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync() {
        IsLoading = true;
        Notify();
        try {
            await Task.WhenAll(ReadDefaultsAsync(), ReadProvidersAsync());
        } finally {
            IsLoading = false;
            Notify();
        }
    }

    private async Task ReadDefaultsAsync() {
        if (acquired) {
            return;
        }
        var versions = Draft.CaptureVersions();
        try {
            var model = await owner.ReadV2Async(lifetime.Token);
            if (!disposed && owner.IsCurrent) {
                Draft.Acquire(model, versions);
                acquired = true;
                defaultsError = null;
            }
        } catch (WorkspaceSettingsPolicyUnavailableException) {
            if (!disposed) {
                defaultsError = "The stored Workspace policy is invalid or unsupported. Settings cannot be edited until it is repaired; no default policy was substituted.";
            }
        } catch (Exception) {
            if (!disposed) {
                defaultsError = "Defaults could not be loaded. Your input is retained.";
            }
        }
    }

    private async Task ReadProvidersAsync() {
        try {
            var providers = await owner.ProvidersAsync(lifetime.Token);
            if (!disposed && owner.IsCurrent) {
                Providers = providers;
                providerError = null;
                Notify();
            }
        } catch (Exception) {
            if (!disposed) {
                providerError = "Provider choices could not be refreshed. Existing references and input are retained.";
            }
        }
    }

    public async Task SaveAsync() {
        if (!CanSave) {
            return;
        }
        var target = Draft;
        ReplaceWorkspaceSettingsCommand? command = null;
        ReplaceWorkspaceSettingsV2Command? policyCommand = null;
        try {
            if (!target.EditContext.Validate()) {
                defaultsError = "Correct the invalid fields before saving. Your original draft is retained.";
                Notify();
                return;
            }
            if (target.PolicyEditing) {
                policyCommand = target.CapturePolicy();
            } else {
                command = target.Capture();
            }
        } catch (ArgumentException) {
            defaultsError = "A policy change requires all six policy fields, defined choices, positive durations, and replans greater than zero only for Replan. Durations use the invariant days.hours:minutes:seconds format.";
            Notify();
            return;
        }
        var versions = target.CaptureVersions();
        var action = policyCommand is null ? SettingsAction.SaveDefaults : SettingsAction.SaveDefaultsWithPolicy;
        if (ledger.Admit(target.Origin, action) is not { } receipt) {
            return;
        }
        var accepted = false;
        try {
            Notify();
            if (policyCommand is { } submittedPolicy) {
                switch (await owner.SaveV2Async(submittedPolicy, lifetime.Token)) {
                    case WorkspaceSettingsSaveOutcomeV2.Accepted saved:
                        receipt.DefaultsAcceptedV2 = saved;
                        LastAcceptedV2 = saved.Snapshot;
                        await AcceptAsync(saved.Snapshot.Expectation, saved.Diagnostic,
                            () => target.Acknowledge(submittedPolicy, saved.Snapshot, versions));
                        break;
                    case WorkspaceSettingsSaveOutcomeV2.Rejected rejected:
                        Reject(rejected.Reason);
                        break;
                    case WorkspaceSettingsSaveOutcomeV2.Unconfirmed:
                        Unknown();
                        break;
                    default:
                        throw new InvalidOperationException("The Workspace owner returned an unsupported v2 outcome.");
                }
            } else {
                var submittedDefaults = command!;
                switch (await owner.SaveAsync(submittedDefaults, lifetime.Token)) {
                    case WorkspaceSettingsSaveOutcome.Accepted saved:
                        receipt.DefaultsAccepted = saved;
                        LastAccepted = saved.Snapshot;
                        await AcceptAsync(saved.Snapshot.Expectation, saved.Diagnostic,
                            () => target.Acknowledge(submittedDefaults, saved.Snapshot, versions));
                        break;
                    case WorkspaceSettingsSaveOutcome.Rejected rejected:
                        Reject(rejected.Reason);
                        break;
                    case WorkspaceSettingsSaveOutcome.Unconfirmed:
                        Unknown();
                        break;
                    default:
                        throw new InvalidOperationException("The Workspace owner returned an unsupported outcome.");
                }
            }
        } catch (Exception) {
            if (accepted) {
                receipt.Effect = SettingsEffect.CommittedWarning;
                receipt.Diagnostic = SettingsDiagnostic.ReadBackPending;
                if (!disposed && owner.IsCurrent && ReferenceEquals(target, Draft)) {
                    requiresReconciliation = true;
                    defaultsError = "Settings were saved. The editor could not reconcile the acknowledgement; review current values before continuing.";
                }
            } else {
                Unknown();
            }
        } finally {
            receipt.IsReconciling = false;
            Notify();
        }

        async Task AcceptAsync(WorkspaceSettingsExpectation expected, WorkspaceSettingsSecondaryDiagnostic? diagnostic, Action reconcile) {
            accepted = true;
            receipt.RecordId = expected.Head!.Id;
            receipt.Effect = diagnostic is null ? SettingsEffect.Committed : SettingsEffect.CommittedWarning;
            receipt.Diagnostic = Diagnostic(diagnostic);
            if (disposed || !owner.IsCurrent || !ReferenceEquals(target, Draft)) {
                return;
            }
            reconcile();
            Rejection = null;
            defaultsError = null;
            CurrentSnapshot = null;
            receipt.IsReconciling = true;
            Notify();
            if (refresh is { IsCompleted: false } prior) {
                await prior;
            }
            await RefreshAsync();
            if (Error is not null && receipt.Effect == SettingsEffect.Committed) {
                receipt.Effect = SettingsEffect.CommittedWarning;
                receipt.Diagnostic = SettingsDiagnostic.ReadBackPending;
            }
        }

        void Reject(WorkspaceSettingsRejectionReason reason) {
            receipt.Effect = SettingsEffect.Refused;
            receipt.Diagnostic = reason == WorkspaceSettingsRejectionReason.OwnerRetired ? SettingsDiagnostic.Retired : SettingsDiagnostic.Validation;
            if (!disposed && owner.IsCurrent && ReferenceEquals(target, Draft)) {
                Rejection = reason;
                defaultsError = RejectionMessage(reason);
            }
        }

        void Unknown() {
            receipt.Effect = SettingsEffect.Unknown;
            receipt.Diagnostic = SettingsDiagnostic.Unknown;
        }
    }

    public async Task ReviewAsync(SettingsReceipt receipt) {
        if (disposed || !owner.IsCurrent || receipt.IsReviewing || !Receipts.Contains(receipt) || receipt.Effect == SettingsEffect.Pending) {
            return;
        }
        receipt.IsReviewing = true;
        receipt.ObservationFailed = false;
        Notify();
        try {
            await ReadCurrentAsync();
            receipt.ObservationFailed = CurrentSnapshot is null;
            receipt.ObservedExists = CurrentSnapshot?.Expectation.State == WorkspaceSettingsHeadState.Existing;
        } catch (Exception) {
            receipt.ObservationFailed = true;
        } finally {
            receipt.IsReviewing = false;
            Notify();
        }
    }

    public Task ReadCurrentAsync() {
        if (disposed || !owner.IsCurrent) {
            return Task.CompletedTask;
        }
        return currentRead is { IsCompleted: false } pending ? pending : currentRead = ReadCurrentCoreAsync();
    }

    private async Task ReadCurrentCoreAsync() {
        var target = Draft;
        readingCurrent = true;
        CurrentSnapshot = null;
        Notify();
        try {
            var current = await owner.ReadV2Async(lifetime.Token);
            if (!disposed && owner.IsCurrent && ReferenceEquals(target, Draft)) {
                CurrentSnapshot = current;
                defaultsError = null;
            }
        } catch (Exception) {
            if (!disposed && ReferenceEquals(target, Draft)) {
                defaultsError = "Current defaults could not be read. Your draft and original expectation are retained.";
            }
        } finally {
            readingCurrent = false;
            Notify();
        }
    }

    public void ReloadCurrent() => StartCurrentDraft(preservePrevious: false);
    public void BeginReconciliation() => StartCurrentDraft(preservePrevious: true);

    private void StartCurrentDraft(bool preservePrevious) {
        if (!CanRecover || preservePrevious && PreviousDraft is not null) {
            return;
        }
        var next = new WorkspaceDefaultsDraft();
        next.Acquire(CurrentSnapshot!, next.CaptureVersions());
        if (preservePrevious) {
            PreviousDraft = Draft;
        }
        Draft = next;
        Rejection = null;
        requiresReconciliation = false;
        defaultsError = null;
        CurrentSnapshot = null;
        Notify();
    }

    public void DiscardPreviousDraft() {
        PreviousDraft = null;
        Notify();
    }

    private static SettingsDiagnostic Diagnostic(WorkspaceSettingsSecondaryDiagnostic? diagnostic) => diagnostic switch {
        null => SettingsDiagnostic.None,
        WorkspaceSettingsSecondaryDiagnostic.ActivityPending => SettingsDiagnostic.ActivityPending,
        WorkspaceSettingsSecondaryDiagnostic.LoggingPending => SettingsDiagnostic.LoggingPending,
        WorkspaceSettingsSecondaryDiagnostic.CleanupPending => SettingsDiagnostic.CleanupPending,
        WorkspaceSettingsSecondaryDiagnostic.CurrencyPublicationPending => SettingsDiagnostic.ReadBackPending,
        _ => throw new ArgumentOutOfRangeException(nameof(diagnostic))
    };

    private static string RejectionMessage(WorkspaceSettingsRejectionReason reason) => reason switch {
        WorkspaceSettingsRejectionReason.HeadChanged => "Workspace defaults changed. Your draft is retained; read current values to compare and explicitly reload or reconcile.",
        WorkspaceSettingsRejectionReason.DatasetChanged => "The dataset changed. Your original draft cannot be saved in the new dataset; read current values before explicitly reloading or reconciling.",
        WorkspaceSettingsRejectionReason.OwnerRetired => "This settings owner has retired. Reopen settings before making another change.",
        WorkspaceSettingsRejectionReason.TransactionConflict => "This attempt did not commit because another transaction prevented it. Your original draft is retained.",
        _ => "Workspace defaults contain invalid values. Correct them before saving again."
    };

    private void Notify() {
        if (!disposed) {
            try {
                Changed?.Invoke();
            } catch (Exception) {
                defaultsError = "The settings display could not refresh. Your draft and recorded outcomes are retained.";
            }
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
