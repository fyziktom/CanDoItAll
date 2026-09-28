using CanDoItAll.Modules.TestLab;
using CanDoItAll.TestLab.UI;

namespace CanDoItAll.TestLab.UiSandbox;

public enum TestLabScenario {
    Representative, Empty, FilteredEmpty, Large, MissingPlan, MissingReferences,
    ReadFailure, StaleRefresh, InvalidEditor, DelayedRead, DelayedReferences,
    DelayedSave, DelayedReadback, CommittedWarning, RefreshFailure, UnknownOutcome, AdmissionRefused
}

public sealed class TestLabScenarioWorkspace : ITestLabWorkspaceView, IDisposable {
    private readonly Action changed;
    private readonly List<Pending> pending = [];
    private long generation;
    private bool disposed;
    public TestLabScenario Scenario { get; }
    public TestLabScenarioStore Store { get; }
    public TestLabWorkspaceState State { get; } = new();
    public int PendingCount => pending.Count(item => !item.Completion.Task.IsCompleted);

    public TestLabScenarioWorkspace(TestLabScenario scenario, Action changed) {
        Scenario = scenario;
        this.changed = changed;
        Store = new(scenario == TestLabScenario.Empty ? 0 : scenario == TestLabScenario.Large ? 250 : 3);
        if (scenario == TestLabScenario.MissingReferences) {
            Store.MakeReferencesUnavailable();
        }
        State.Projects = Store.Projects;
        State.ProjectsRead = TestLabReadState.Ready;
        State.Plans = scenario == TestLabScenario.ReadFailure ? [] : Store.List();
        State.PlansRead = scenario switch {
            TestLabScenario.ReadFailure => TestLabReadState.Unavailable,
            TestLabScenario.StaleRefresh => TestLabReadState.Stale,
            TestLabScenario.Empty => TestLabReadState.Empty,
            _ => TestLabReadState.Ready
        };
        State.Search = scenario == TestLabScenario.FilteredEmpty ? "no matching plan" : string.Empty;
        State.EditorRead = scenario == TestLabScenario.MissingPlan ? TestLabReadState.Missing : TestLabReadState.Ready;
        if (scenario != TestLabScenario.MissingPlan) {
            State.Draft = new(State.Plans.FirstOrDefault() is { } first ? Store.Read(first.Id)! : new());
            if (scenario == TestLabScenario.InvalidEditor) {
                State.Draft.Model.Title = string.Empty;
                State.Draft.Context.Validate();
            }
            SetReferences();
        }
    }

    public async Task SelectAsync(Guid id) {
        if (disposed) {
            return;
        }
        RetireReads();
        var version = generation;
        State.Draft = null;
        State.Section = TestLabSection.Overview;
        State.EditorRead = TestLabReadState.Loading;
        Changed();
        if (Scenario == TestLabScenario.DelayedRead && !await WaitAsync(write: false)) {
            return;
        }
        if (disposed || version != generation) {
            return;
        }
        var plan = Store.Read(id);
        State.Draft = plan is null ? null : new(plan);
        State.EditorRead = plan is null ? TestLabReadState.Missing : TestLabReadState.Ready;
        SetReferences();
        Changed();
    }

    public Task NewAsync() {
        if (!disposed) {
            RetireReads();
            State.Draft = new(new());
            State.Section = TestLabSection.Overview;
            State.EditorRead = TestLabReadState.Ready;
            SetReferences();
            Changed();
        }
        return Task.CompletedTask;
    }

    public async Task ChangeProjectAsync(TestLabDraft origin, Guid? projectId, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion)) {
            return;
        }
        RetireReads();
        origin.ChangeProject(projectId, Store.Projects.FirstOrDefault(item => item.Id == projectId)?.Admission);
        var version = generation;
        State.Parties = [];
        State.PartiesRead = TestLabReadState.Loading;
        Changed();
        if (Scenario == TestLabScenario.DelayedReferences && !await WaitAsync(write: false)) {
            return;
        }
        if (!disposed && version == generation && ReferenceEquals(State.Draft, origin)) {
            SetReferences();
            Changed();
        }
    }

    public async Task SaveAsync(TestLabDraft origin, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion) || origin.BeginSave() is not { } submission) {
            return;
        }
        Changed();
        if (Scenario == TestLabScenario.DelayedSave) {
            await WaitAsync(write: true);
        }
        var admitted = submission.Model.ProjectId is null && submission.Model.ExpectedProjectAdmission is null ||
            Store.Projects.Any(item => item.Id == submission.Model.ProjectId && item.Admission == submission.Model.ExpectedProjectAdmission);
        if (!admitted || Scenario is TestLabScenario.UnknownOutcome or TestLabScenario.AdmissionRefused) {
            if (!disposed && submission.BelongsTo(State.Draft)) {
                origin.Finish(submission, Scenario == TestLabScenario.UnknownOutcome ? TestLabSaveState.Unknown : TestLabSaveState.Refused,
                    Scenario == TestLabScenario.UnknownOutcome
                        ? "Save outcome is unknown. Submission is locked; refresh and review stored plans before explicitly choosing another draft."
                        : "Project admission was refused. Select a current project explicitly.");
                Changed();
            }
            return;
        }
        var id = Store.Commit(submission.Model);
        if (disposed || !submission.BelongsTo(State.Draft)) {
            return;
        }
        submission.RetainIdentities(origin, id);
        origin.LastCommitted = submission;
        if (Scenario == TestLabScenario.DelayedReadback && !await WaitAsync(write: false)) {
            return;
        }
        if (disposed || !submission.BelongsTo(State.Draft)) {
            return;
        }
        var warning = Scenario is TestLabScenario.CommittedWarning or TestLabScenario.RefreshFailure;
        if (!warning) {
            submission.Reconcile(origin, Store.Read(id)!);
            RefreshList();
        }
        origin.Finish(submission, warning ? TestLabSaveState.SavedWithWarning : TestLabSaveState.Saved,
            warning ? $"Test plan '{id:D}' saved; refresh incomplete. Retry refresh." : $"Test plan '{id:D}' was saved.");
        State.Receipt = new(origin.SaveState, id, origin.Message);
        Changed();
    }

    public Task ChangePartyAsync(TestLabDraft origin, Guid? partyId, long? targetVersion = null) {
        if (AcceptIntent(origin, targetVersion)) {
            origin.Model.ResponsiblePartyId = partyId;
            RetireReads();
            SetReferences();
            Changed();
        }
        return Task.CompletedTask;
    }

    public Task RetryAsync() {
        if (!disposed) {
            RefreshList();
            if (State.Draft is { LastCommitted: { } submission, Pending: null } draft && submission.BelongsTo(draft) &&
                submission.CommittedId is { } id && Store.Read(id) is { } saved) {
                submission.Reconcile(draft, saved);
                draft.SaveState = TestLabSaveState.Saved;
                draft.Message = $"Test plan '{id:D}' refreshed. Newer edits are retained.";
            }
            SetReferences();
            Changed();
        }
        return Task.CompletedTask;
    }

    public void CompletePending() {
        pending.FirstOrDefault(item => !item.Completion.Task.IsCompleted)?.Completion.TrySetResult(true);
        Changed();
    }

    private async Task<bool> WaitAsync(bool write) {
        var wait = new Pending(write);
        pending.Add(wait);
        Changed();
        try {
            return await wait.Completion.Task;
        } finally {
            pending.Remove(wait);
        }
    }

    private void RetireReads() {
        generation++;
        foreach (var wait in pending.Where(item => !item.Write).ToArray()) {
            wait.Completion.TrySetResult(false);
        }
    }

    private void SetReferences() {
        State.Parties = Scenario == TestLabScenario.MissingReferences || State.Draft?.Model.ProjectId is null
            ? [] : [new(Store.PartyId, "Delivery reviewer")];
        State.PartiesRead = State.Parties.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
    }

    private void RefreshList() {
        State.Plans = Store.List();
        State.PlansRead = State.Plans.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
    }

    private void Changed() {
        if (!disposed) {
            changed();
        }
    }

    private bool AcceptIntent(TestLabDraft origin, long? version) => !disposed && ReferenceEquals(State.Draft, origin) &&
        (version is null || version == origin.TargetVersion);

    public void Dispose() {
        disposed = true;
        RetireReads();
        foreach (var wait in pending.ToArray()) {
            wait.Completion.TrySetResult(wait.Write);
        }
    }

    private sealed class Pending(bool write) {
        public bool Write { get; } = write;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
