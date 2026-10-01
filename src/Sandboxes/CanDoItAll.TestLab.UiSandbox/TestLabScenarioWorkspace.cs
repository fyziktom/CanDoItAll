using System.Collections.Immutable;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.TestLab.UI;

namespace CanDoItAll.TestLab.UiSandbox;

public enum TestLabScenario {
    Representative, Empty, FilteredEmpty, Large, MissingPlan, MissingReferences,
    ReadFailure, StaleRefresh, InvalidEditor, DelayedRead, DelayedReferences,
    DelayedSave, DelayedReadback, CommittedWarning, RefreshFailure, UnknownOutcome, AdmissionRefused,
    DelayedPartyLookup, ReferenceFailure
}

public sealed class TestLabScenarioWorkspace : ITestLabWorkspaceView, IDisposable {
    private readonly Action changed;
    private ImmutableList<Pending> pending = [];
    private long generation;
    private long referenceGeneration;
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
        Store.FailPartyLookup = scenario == TestLabScenario.ReferenceFailure;
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
            if (scenario == TestLabScenario.ReferenceFailure) {
                State.Draft.ChangeProject(null, null);
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
        if (Scenario == TestLabScenario.DelayedRead && !await WaitAsync(Operation.PlanRead)) {
            return;
        }
        if (disposed || version != generation) {
            return;
        }
        var plan = Store.Read(id);
        State.Draft = plan is null ? null : new(plan);
        State.EditorRead = plan is null ? TestLabReadState.Missing : TestLabReadState.Ready;
        Changed();
        if (State.Draft is { } draft) {
            await LoadReferencesAsync(draft);
        }
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

    public Task ChangeProjectAsync(TestLabDraft origin, Guid? projectId, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion)) {
            return Task.CompletedTask;
        }
        RetireReads();
        origin.ChangeProject(projectId, Store.Projects.FirstOrDefault(item => item.Id == projectId)?.Admission);
        return LoadReferencesAsync(origin);
    }

    public async Task SaveAsync(TestLabDraft origin, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion) || origin.BeginSave() is not { } submission) {
            return;
        }
        Changed();
        if (Scenario == TestLabScenario.DelayedSave) {
            await WaitAsync(Operation.Write);
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
        if (Scenario == TestLabScenario.DelayedReadback && !await WaitAsync(Operation.Readback)) {
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
            return LoadReferencesAsync(origin);
        }
        return Task.CompletedTask;
    }

    public async Task RetryAsync() {
        if (!disposed) {
            RefreshList();
            if (State.Draft is { LastCommitted: { } submission, Pending: null } draft && submission.BelongsTo(draft) &&
                submission.CommittedId is { } id && Store.Read(id) is { } saved) {
                submission.Reconcile(draft, saved);
                draft.SaveState = TestLabSaveState.Saved;
                draft.Message = $"Test plan '{id:D}' refreshed. Newer edits are retained.";
            }
            Changed();
            if (State.Draft is { } origin) {
                await LoadReferencesAsync(origin);
            }
        }
    }

    public void CompletePending() {
        pending.FirstOrDefault(item => !item.Completion.Task.IsCompleted)?.Completion.TrySetResult(true);
        Changed();
    }

    public void FailPendingReferenceRead() {
        pending.First(item => item.Kind == Operation.References && !item.Completion.Task.IsCompleted)
            .Completion.TrySetException(new InvalidOperationException("The controlled scenario read failed."));
    }

    private async Task<bool> WaitAsync(Operation kind) {
        var wait = new Pending(kind);
        ImmutableInterlocked.Update(ref pending, items => items.Add(wait));
        Changed();
        try {
            return await wait.Completion.Task;
        } finally {
            ImmutableInterlocked.Update(ref pending, items => items.Remove(wait));
        }
    }

    private void RetireReads() {
        generation++;
        RetireReferences();
        foreach (var wait in pending.Where(item => item.Kind != Operation.Write).ToArray()) {
            wait.Completion.TrySetResult(false);
        }
    }

    private void RetireReferences() {
        referenceGeneration++;
        foreach (var wait in pending.Where(item => item.Kind == Operation.References).ToArray()) {
            wait.Completion.TrySetResult(false);
        }
    }

    private async Task LoadReferencesAsync(TestLabDraft origin) {
        RetireReferences();
        var version = referenceGeneration;
        var target = origin.TargetVersion;
        var partyId = origin.Model.ResponsiblePartyId;
        State.Parties = [];
        State.PartiesRead = TestLabReadState.Loading;
        Changed();
        bool Current() => !disposed && version == referenceGeneration && ReferenceEquals(State.Draft, origin) &&
            origin.TargetVersion == target && origin.Model.ResponsiblePartyId == partyId;
        try {
            var options = Store.ListParties(origin.Model.ProjectId);
            var needsFallback = partyId is { } id && options.All(item => item.PartyId != id);
            var delay = Scenario == TestLabScenario.DelayedReferences || Scenario == TestLabScenario.DelayedPartyLookup && needsFallback;
            if (delay && !await WaitAsync(Operation.References)) {
                return;
            }
            if (Current()) {
                SetReferences();
                Changed();
            }
        } catch (InvalidOperationException) {
            if (Current()) {
                State.PartiesRead = TestLabReadState.Unavailable;
                Changed();
            }
        }
    }

    private void SetReferences() {
        State.Parties = [];
        try {
            var draft = State.Draft?.Model;
            var options = Store.ListParties(draft?.ProjectId);
            if (draft?.ResponsiblePartyId is { } id && options.All(item => item.PartyId != id) && Store.ReadParty(id) is { } saved) {
                options = options.Append(saved).OrderBy(item => item.DisplayName).ToArray();
            }
            State.Parties = options;
            State.PartiesRead = options.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
        } catch (InvalidOperationException) {
            State.PartiesRead = TestLabReadState.Unavailable;
        }
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
            wait.Completion.TrySetResult(wait.Kind == Operation.Write);
        }
    }

    private enum Operation { PlanRead, References, Readback, Write }

    private sealed class Pending(Operation kind) {
        public Operation Kind { get; } = kind;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
