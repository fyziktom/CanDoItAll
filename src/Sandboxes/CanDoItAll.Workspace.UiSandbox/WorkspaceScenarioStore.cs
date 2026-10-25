using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Workspace.UiSandbox;

public enum WorkspaceScenario { Populated, Empty, MissingProvider, MissingSecret, PartialReferences, RebindRequired, DeniedHistory, OversizePreview }
public enum WorkspaceOperation { DefaultsRead, ProvidersRead, DefaultsSave, SecretList, SecretGet, SecretSave, SecretDelete, FileList, FileSave, FileDelete, PolicyLoad, PolicyPreview, PolicyUpdate }
public enum WorkspaceFault { None, Refused, ReadFailure, CommittedWarning, Unknown, HeadChanged }

public sealed class WorkspaceScenarioGate {
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Entered => entered.Task;
    public void Release() => release.TrySetResult();
    internal Task WaitAsync() {
        entered.TrySetResult();
        return release.Task;
    }
}

public sealed class WorkspaceScenarioStore : IWorkspaceDefaultsOwner, IWorkspaceSecretsOwner, IWorkspaceFilesOwner, IProviderHistoryPolicyService {
    public static readonly Guid ProviderId = Guid.Parse("bb040000-0000-0000-0000-000000000001");
    public static readonly Guid SecretId = Guid.Parse("bb040000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<WorkspaceOperation, WorkspaceScenarioGate> gates = [];
    private readonly Dictionary<WorkspaceOperation, WorkspaceFault> faults = [];
    private readonly Dictionary<Guid, SecretEditorModel> secrets = [];
    private readonly Dictionary<WorkspaceFileExtension, WorkspaceFilePreference> files = [];
    private readonly Dictionary<WorkspaceOperation, int> calls = [];
    private readonly WorkspaceScenario scenario;
    private WorkspaceSettingsModel defaults = new() { WorkspaceName = "Scenario workspace", DefaultProviderProfileId = ProviderId };
    private WorkspaceSettingsExpectation defaultsExpected = new(new DatasetNamespaceId(Guid.Parse("bb040000-0000-0000-0000-000000000003")),
        WorkspaceSettingsHeadState.Existing, new(Guid.Parse("bb040000-0000-0000-0000-000000000004"), Guid.NewGuid()));
    private DecisionDataPolicy? decisionPolicy;
    private HistoryPolicySnapshot policy = new(new(), 1);
    public bool IsCurrent { get; set; } = true;
    public IReadOnlyDictionary<WorkspaceOperation, int> Calls => calls;
    public int DurableWrites { get; private set; }
    public event Action? WriteCompleted;

    public WorkspaceScenarioStore(WorkspaceScenario scenario = WorkspaceScenario.Populated) {
        this.scenario = scenario;
        if (scenario != WorkspaceScenario.Empty) {
            secrets[SecretId] = new() { Id = SecretId, Name = "Synthetic secret", SecretValue = "sandbox-only-value", Kind = SecretKind.ApiKey };
            var extension = new WorkspaceFileExtension(".sample");
            files[extension] = new(extension, "/synthetic/viewer", scenario == WorkspaceScenario.RebindRequired);
        }
        if (scenario == WorkspaceScenario.Empty) {
            defaults.DefaultProviderProfileId = null;
            defaultsExpected = new(defaultsExpected.Namespace, WorkspaceSettingsHeadState.Empty, null);
        }
    }

    public WorkspaceScenarioGate HoldNext(WorkspaceOperation operation) {
        var gate = new WorkspaceScenarioGate();
        gates.Add(operation, gate);
        return gate;
    }

    public void FaultNext(WorkspaceOperation operation, WorkspaceFault fault) => faults[operation] = fault;

    private async Task<WorkspaceFault> EnterAsync(WorkspaceOperation operation) {
        calls[operation] = calls.GetValueOrDefault(operation) + 1;
        var fault = faults.Remove(operation, out var queued) ? queued : WorkspaceFault.None;
        if (gates.Remove(operation, out var gate)) {
            await gate.WaitAsync();
        }
        if (fault == WorkspaceFault.ReadFailure) {
            throw new IOException("Scenario read failure.");
        }
        return fault;
    }

    public async Task<WorkspaceSettingsSnapshot> ReadAsync(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.DefaultsRead);
        return new(defaultsExpected, WorkspaceSettingsValues.Capture(defaults));
    }

    public async Task<WorkspaceSettingsSnapshotV2> ReadV2Async(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.DefaultsRead);
        return new(defaultsExpected, WorkspaceSettingsValues.Capture(defaults), decisionPolicy);
    }

    public async Task<IReadOnlyList<WorkspaceProviderOption>> ProvidersAsync(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.ProvidersRead);
        if (scenario == WorkspaceScenario.PartialReferences) {
            throw new IOException("Scenario provider list unavailable.");
        }
        return scenario is WorkspaceScenario.MissingProvider or WorkspaceScenario.Empty ? [] : [new(ProviderId, "Synthetic provider", true)];
    }

    public async Task<WorkspaceSettingsSaveOutcome> SaveAsync(ReplaceWorkspaceSettingsCommand command, CancellationToken cancellationToken) {
        var captured = command.Values.ToEditorModel();
        if (!IsCurrent) {
            return new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.OwnerRetired);
        }
        var fault = await EnterAsync(WorkspaceOperation.DefaultsSave);
        if (fault == WorkspaceFault.Refused) {
            return new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.InvalidValues);
        }
        if (fault == WorkspaceFault.HeadChanged || defaultsExpected != command.Expected) {
            return new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.HeadChanged);
        }
        captured.WorkspaceName = captured.WorkspaceName.Trim();
        captured.Notes = captured.Notes.Trim();
        captured.CurrencyCode = captured.CurrencyCode.ToUpperInvariant();
        defaults = captured;
        defaultsExpected = new(defaultsExpected.Namespace, WorkspaceSettingsHeadState.Existing,
            new(defaultsExpected.Head?.Id ?? Guid.NewGuid(), Guid.NewGuid()));
        DurableWrites++;
        var accepted = new WorkspaceSettingsSnapshot(defaultsExpected, WorkspaceSettingsValues.Capture(defaults));
        WriteCompleted?.Invoke();
        return fault == WorkspaceFault.Unknown ? new WorkspaceSettingsSaveOutcome.Unconfirmed()
            : new WorkspaceSettingsSaveOutcome.Accepted(accepted,
                fault == WorkspaceFault.CommittedWarning ? WorkspaceSettingsSecondaryDiagnostic.CleanupPending : null);
    }

    public async Task<WorkspaceSettingsSaveOutcomeV2> SaveV2Async(ReplaceWorkspaceSettingsV2Command command, CancellationToken cancellationToken) {
        var captured = command.Values.ToEditorModel();
        if (!IsCurrent) {
            return new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.OwnerRetired);
        }
        var fault = await EnterAsync(WorkspaceOperation.DefaultsSave);
        if (fault == WorkspaceFault.Refused) {
            return new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.InvalidValues);
        }
        if (fault == WorkspaceFault.HeadChanged || defaultsExpected != command.Expected) {
            return new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.HeadChanged);
        }
        captured.WorkspaceName = captured.WorkspaceName.Trim();
        captured.Notes = captured.Notes.Trim();
        captured.CurrencyCode = captured.CurrencyCode.ToUpperInvariant();
        defaults = captured;
        decisionPolicy = command.DecisionDataPolicy;
        defaultsExpected = new(defaultsExpected.Namespace, WorkspaceSettingsHeadState.Existing,
            new(defaultsExpected.Head?.Id ?? Guid.NewGuid(), Guid.NewGuid()));
        DurableWrites++;
        var accepted = new WorkspaceSettingsSnapshotV2(defaultsExpected, WorkspaceSettingsValues.Capture(defaults), decisionPolicy);
        WriteCompleted?.Invoke();
        return fault == WorkspaceFault.Unknown ? new WorkspaceSettingsSaveOutcomeV2.Unconfirmed()
            : new WorkspaceSettingsSaveOutcomeV2.Accepted(accepted,
                fault == WorkspaceFault.CommittedWarning ? WorkspaceSettingsSecondaryDiagnostic.CleanupPending : null);
    }

    async Task<IReadOnlyList<SecretListItem>> IWorkspaceSecretsOwner.ListAsync(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.SecretList);
        return secrets.Values.Select(item => new SecretListItem(item.Id!.Value, item.Name, item.Kind, item.Scope, Timestamp)).ToArray();
    }

    public async Task<SecretEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.SecretGet);
        return scenario == WorkspaceScenario.MissingSecret ? null : secrets.GetValueOrDefault(id)?.Copy();
    }

    public async Task<SettingsWriteResult<Guid>> SaveAsync(SecretEditorModel command, CancellationToken cancellationToken) {
        var captured = command.Copy();
        try {
            var fault = await EnterAsync(WorkspaceOperation.SecretSave);
            if (fault == WorkspaceFault.Refused || string.IsNullOrWhiteSpace(captured.Name) || string.IsNullOrWhiteSpace(captured.SecretValue)) {
                return new(SettingsWriteState.Refused, captured.Id ?? Guid.Empty, SettingsDiagnostic.Validation);
            }
            if (captured.Id is { } existing && !secrets.ContainsKey(existing)) {
                return new(SettingsWriteState.Refused, existing, SettingsDiagnostic.Missing);
            }
            var id = captured.Id ?? Guid.NewGuid();
            captured.Id = id;
            secrets[id] = captured.Copy();
            return Finish(fault, id);
        } finally {
            captured.SecretValue = string.Empty;
            captured.MetadataJson = string.Empty;
        }
    }

    public async Task<SettingsWriteResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken) {
        var fault = await EnterAsync(WorkspaceOperation.SecretDelete);
        if (fault == WorkspaceFault.Refused) {
            return new(SettingsWriteState.Refused, id, SettingsDiagnostic.ReferenceBlocked);
        }
        secrets.Remove(id);
        return Finish(fault, id);
    }

    public WorkspaceFileCommand Capture(string extension, string executablePath) {
        var value = extension.Trim().ToLowerInvariant();
        if (!value.StartsWith('.')) {
            value = "." + value;
        }
        if (value.Length is < 2 or > 32 || value[1..].Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_')) {
            throw new ArgumentException("A single extension is required.", nameof(extension));
        }
        return new(new(value), executablePath);
    }

    async Task<IReadOnlyList<WorkspaceFilePreference>> IWorkspaceFilesOwner.ListAsync(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.FileList);
        return files.Values.OrderBy(item => item.Extension.Value).ToArray();
    }

    public async Task<SettingsWriteResult<WorkspaceFileExtension>> SaveAsync(WorkspaceFileCommand command, CancellationToken cancellationToken) {
        var fault = await EnterAsync(WorkspaceOperation.FileSave);
        if (fault == WorkspaceFault.Refused || string.IsNullOrWhiteSpace(command.ExecutablePath)) {
            return new(SettingsWriteState.Refused, command.Extension, SettingsDiagnostic.Validation);
        }
        files[command.Extension] = new(command.Extension, command.ExecutablePath, false);
        return Finish(fault, command.Extension);
    }

    public async Task<SettingsWriteResult<WorkspaceFileExtension>> DeleteAsync(WorkspaceFileExtension extension, CancellationToken cancellationToken) {
        var fault = await EnterAsync(WorkspaceOperation.FileDelete);
        if (fault == WorkspaceFault.Refused) {
            return new(SettingsWriteState.Refused, extension, SettingsDiagnostic.Validation);
        }
        files.Remove(extension);
        return Finish(fault, extension);
    }

    public async Task<HistoryPolicySnapshot> GetAsync(CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.PolicyLoad);
        CheckHistory();
        return policy;
    }

    public async Task<HistoryRetentionPreview> PreviewShorterRetentionAsync(HistoryPolicy proposed, CancellationToken cancellationToken) {
        await EnterAsync(WorkspaceOperation.PolicyPreview);
        CheckHistory();
        return new(3, 2, 1000, scenario == WorkspaceScenario.OversizePreview);
    }

    public async Task<HistoryPolicySnapshot> UpdateAsync(HistoryPolicyUpdate update, CancellationToken cancellationToken) {
        var fault = await EnterAsync(WorkspaceOperation.PolicyUpdate);
        CheckHistory();
        if (fault == WorkspaceFault.Refused || update.ExpectedVersion != policy.Version ||
            update.ApplyShorterRetention && scenario == WorkspaceScenario.OversizePreview) {
            throw new ProviderHistoryException(HistoryFailure.Conflict, "Scenario policy refused.");
        }
        policy = new(update.Policy, policy.Version + 1);
        Finish(fault, policy);
        return policy;
    }

    private void CheckHistory() {
        if (scenario == WorkspaceScenario.DeniedHistory) {
            throw new ProviderHistoryException(HistoryFailure.Denied, "Scenario management access denied.");
        }
    }

    private SettingsWriteResult<T> Finish<T>(WorkspaceFault fault, T value) {
        DurableWrites++;
        WriteCompleted?.Invoke();
        if (fault == WorkspaceFault.Unknown) {
            throw new IOException("Scenario acknowledgement unavailable.");
        }
        return fault == WorkspaceFault.CommittedWarning
            ? new(SettingsWriteState.CommittedWarning, value, SettingsDiagnostic.CleanupPending)
            : new(SettingsWriteState.Committed, value);
    }
}
