using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public enum DefaultsField {
    Name, Provider, OutputFormat, Currency, Culture, Notes,
    PolicyReadBasis, PolicyFreshness, PolicyOnChange, PolicyReplans, PolicyWorkDuration, PolicyBasisAge
}

public sealed class WorkspaceDefaultsDraft {
    private readonly Dictionary<DefaultsField, long> versions = [];
    public Guid Origin { get; } = Guid.NewGuid();
    public WorkspaceSettingsModel Model { get; } = new();
    public WorkspaceDecisionPolicyDraft Policy { get; } = new();
    public bool PolicyEditing { get; private set; }
    public bool HasPolicyBaseline { get; private set; }
    public DecisionDataPolicy? PolicyBaseline { get; private set; }
    public WorkspaceSettingsExpectation? PolicyBaselineExpectation { get; private set; }
    public EditContext EditContext { get; }
    public WorkspaceSettingsExpectation? Expectation { get; private set; }
    public WorkspaceDefaultsDraft() => EditContext = new(Model);
    public void Edited(DefaultsField field) {
        versions[field] = versions.GetValueOrDefault(field) + 1;
        if (field >= DefaultsField.PolicyReadBasis) {
            PolicyEditing = true;
        }
    }
    public IReadOnlyDictionary<DefaultsField, long> CaptureVersions() => new Dictionary<DefaultsField, long>(versions);

    public ReplaceWorkspaceSettingsCommand Capture()
        => new(Expectation ?? throw new InvalidOperationException("The original Workspace settings snapshot has not been acquired."),
            WorkspaceSettingsValues.Capture(Model));

    public ReplaceWorkspaceSettingsV2Command CapturePolicy()
        => new(Expectation ?? throw new InvalidOperationException("The original Workspace settings snapshot has not been acquired."),
            WorkspaceSettingsValues.Capture(Model), Policy.Capture());

    public void Acquire(WorkspaceSettingsSnapshotV2 snapshot, IReadOnlyDictionary<DefaultsField, long> captured) {
        ArgumentNullException.ThrowIfNull(snapshot);
        Acquire(new WorkspaceSettingsSnapshot(snapshot.Expectation, snapshot.Values), captured);
        SetPolicyBaseline(snapshot.Expectation, snapshot.DecisionDataPolicy);
        Policy.Reconcile(snapshot.DecisionDataPolicy, (field, assign) => Apply(field, captured, assign));
    }

    public void Acquire(WorkspaceSettingsSnapshot snapshot, IReadOnlyDictionary<DefaultsField, long> captured) {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Expectation is not null) {
            throw new InvalidOperationException("An acquired Workspace draft cannot silently adopt another expectation.");
        }
        Expectation = snapshot.Expectation;
        Reconcile(snapshot.Values.ToEditorModel(), captured);
    }

    public void Acknowledge(ReplaceWorkspaceSettingsCommand submitted, WorkspaceSettingsSnapshot accepted,
        IReadOnlyDictionary<DefaultsField, long> captured) {
        ArgumentNullException.ThrowIfNull(submitted);
        ArgumentNullException.ThrowIfNull(accepted);
        if (Expectation != submitted.Expected || accepted.Expectation.Namespace != submitted.Expected.Namespace ||
                accepted.Expectation.State != WorkspaceSettingsHeadState.Existing ||
                submitted.Expected.Head is { } original &&
                    (accepted.Expectation.Head!.Id != original.Id || accepted.Expectation.Head.Revision == original.Revision)) {
            throw new InvalidOperationException("The accepted Workspace snapshot does not match the original submission.");
        }
        if (HasPolicyBaseline && PolicyBaselineExpectation == submitted.Expected) {
            PolicyBaselineExpectation = accepted.Expectation;
        } else {
            HasPolicyBaseline = false;
            PolicyBaseline = null;
            PolicyBaselineExpectation = null;
        }
        Expectation = accepted.Expectation;
        Reconcile(accepted.Values.ToEditorModel(), captured);
    }

    public void Acknowledge(ReplaceWorkspaceSettingsV2Command submitted, WorkspaceSettingsSnapshotV2 accepted,
        IReadOnlyDictionary<DefaultsField, long> captured) {
        ArgumentNullException.ThrowIfNull(submitted);
        ArgumentNullException.ThrowIfNull(accepted);
        if (accepted.DecisionDataPolicy != submitted.DecisionDataPolicy) {
            throw new InvalidOperationException("The accepted Workspace policy differs from the submitted policy.");
        }
        Acknowledge(new ReplaceWorkspaceSettingsCommand(submitted.Expected, submitted.Values),
            new WorkspaceSettingsSnapshot(accepted.Expectation, accepted.Values), captured);
        SetPolicyBaseline(accepted.Expectation, accepted.DecisionDataPolicy);
        Policy.Reconcile(accepted.DecisionDataPolicy, (field, assign) => Apply(field, captured, assign));
        PolicyEditing = Enum.GetValues<DefaultsField>().Where(field => field >= DefaultsField.PolicyReadBasis)
            .Any(field => versions.GetValueOrDefault(field) != captured.GetValueOrDefault(field));
    }

    private void SetPolicyBaseline(WorkspaceSettingsExpectation expected, DecisionDataPolicy? policy) {
        HasPolicyBaseline = true;
        PolicyBaselineExpectation = expected;
        PolicyBaseline = policy;
    }

    private void Apply(DefaultsField field, IReadOnlyDictionary<DefaultsField, long> captured, Action assign) {
        if (versions.GetValueOrDefault(field) == captured.GetValueOrDefault(field)) {
            assign();
        }
    }

    public void Reconcile(WorkspaceSettingsModel saved, IReadOnlyDictionary<DefaultsField, long> captured) {
        Apply(DefaultsField.Name, () => Model.WorkspaceName = saved.WorkspaceName);
        Apply(DefaultsField.Provider, () => Model.DefaultProviderProfileId = saved.DefaultProviderProfileId);
        Apply(DefaultsField.OutputFormat, () => Model.DefaultPromptOutputFormat = saved.DefaultPromptOutputFormat);
        Apply(DefaultsField.Currency, () => Model.CurrencyCode = saved.CurrencyCode);
        Apply(DefaultsField.Culture, () => Model.CurrencyCultureName = saved.CurrencyCultureName);
        Apply(DefaultsField.Notes, () => Model.Notes = saved.Notes);

        void Apply(DefaultsField field, Action assign) {
            if (versions.GetValueOrDefault(field) == captured.GetValueOrDefault(field)) {
                assign();
            }
        }
    }
}

public interface IWorkspaceDefaultsState {
    event Action? Changed;
    WorkspaceDefaultsDraft Draft { get; }
    IReadOnlyList<WorkspaceProviderOption> Providers { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    bool CanSave { get; }
    bool IsLoading { get; }
    string? Error { get; }
    WorkspaceSettingsSnapshotV2? CurrentSnapshot { get; }
    WorkspaceDefaultsDraft? PreviousDraft { get; }
    WorkspaceSettingsRejectionReason? Rejection { get; }
    bool CanRecover { get; }
    Task RefreshAsync();
    Task SaveAsync();
    Task ReviewAsync(SettingsReceipt receipt);
    Task ReadCurrentAsync();
    void ReloadCurrent();
    void BeginReconciliation();
    void DiscardPreviousDraft();
}
