using CanDoItAll.TestLab.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.TestLab;

public sealed class TestLabWorkspaceSession(
    ITestLabWorkspaceOwner owner,
    Action changed,
    Action<TestLabReceipt> notify,
    ILogger<TestLabWorkspaceSession> logger) : ITestLabWorkspaceView, IDisposable {
    private readonly ReadLane planReads = new();
    private readonly ReadLane listReads = new();
    private readonly ReadLane projectReads = new();
    private readonly ReadLane partyReads = new();
    private (Guid? PlanId, Guid? ProjectId)? route;
    private long routeVersion;
    private Guid? requestedPlan;
    private bool disposed;
    public TestLabWorkspaceState State { get; } = new();

    public async Task ApplyRouteAsync(Guid? planId, Guid? projectId) {
        if (disposed || route == (planId, projectId)) {
            return;
        }
        route = (planId, projectId);
        var version = ++routeVersion;
        RetireEditor();
        await Task.WhenAll(LoadProjectsAsync(), LoadListAsync());
        if (disposed || routeVersion != version) {
            return;
        }
        if (planId is { } id) {
            await SelectAsync(id);
        } else {
            await NewAsync();
        }
    }

    public Task NewAsync() {
        if (disposed) {
            return Task.CompletedTask;
        }
        routeVersion++;
        RetireEditor();
        requestedPlan = null;
        var projectId = route?.ProjectId;
        State.Draft = new(new() {
            ProjectId = projectId,
            ExpectedProjectAdmission = State.Projects.FirstOrDefault(item => item.Id == projectId)?.Admission
        });
        State.EditorRead = TestLabReadState.Ready;
        Changed();
        return LoadPartiesAsync(State.Draft);
    }

    public async Task SelectAsync(Guid id) {
        if (disposed) {
            return;
        }
        routeVersion++;
        RetireEditor();
        requestedPlan = id;
        using var read = planReads.Begin();
        State.EditorRead = TestLabReadState.Loading;
        Changed();
        try {
            var model = await owner.GetAsync(id, read.Token);
            if (!planReads.Accept(read) || disposed) {
                return;
            }
            State.EditorRead = model is null ? TestLabReadState.Missing : TestLabReadState.Ready;
            State.Draft = model is null ? null : new(model);
            Changed();
            if (State.Draft is { } draft) {
                await LoadPartiesAsync(draft);
            }
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (planReads.Accept(read) && !disposed) {
                State.EditorRead = TestLabReadState.Unavailable;
                ReadFailed(exception, "plan", id);
            }
        }
    }

    public Task ChangeProjectAsync(TestLabDraft origin, Guid? projectId, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion)) {
            return Task.CompletedTask;
        }
        planReads.Retire();
        origin.ChangeProject(projectId, State.Projects.FirstOrDefault(item => item.Id == projectId)?.Admission);
        Changed();
        return LoadPartiesAsync(origin);
    }

    public Task ChangePartyAsync(TestLabDraft origin, Guid? partyId, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion)) {
            return Task.CompletedTask;
        }
        origin.Model.ResponsiblePartyId = partyId;
        return LoadPartiesAsync(origin);
    }

    public async Task SaveAsync(TestLabDraft origin, long? targetVersion = null) {
        if (!AcceptIntent(origin, targetVersion) || origin.BeginSave() is not { } submission) {
            return;
        }
        planReads.Retire();
        Changed();
        TestLabWriteResult result;
        try {
            result = await owner.SaveAsync(submission.Model);
        } catch (Exception exception) {
            logger.LogError("TestLab owner port failed. FailureType={FailureType}", exception.GetType().Name);
            result = new(TestLabWriteOutcome.Unknown, null, "Save outcome is unknown. Review stored plans before starting another submission.");
        }
        if (disposed) {
            return;
        }
        var current = submission.BelongsTo(State.Draft);
        if (result.Outcome is TestLabWriteOutcome.Refused or TestLabWriteOutcome.Unknown) {
            if (current) {
                var status = result.Outcome == TestLabWriteOutcome.Refused ? TestLabSaveState.Refused : TestLabSaveState.Unknown;
                origin.Finish(submission, status, result.Message);
                Receipt(new(status, null, result.Message));
            }
            return;
        }
        if (result.PlanId is not { } id || id == Guid.Empty) {
            if (current) {
                origin.Finish(submission, TestLabSaveState.Unknown, "The owner returned no committed identity. Review stored plans before starting another submission.");
                Changed();
            }
            return;
        }
        if (current) {
            submission.RetainIdentities(origin, id);
            origin.LastCommitted = submission;
        }
        var statusAfterSave = TestLabSaveState.SavedWithWarning;
        if (result.Outcome == TestLabWriteOutcome.Committed && current) {
            statusAfterSave = await RefreshCommittedAsync(origin, submission) ? TestLabSaveState.Saved : TestLabSaveState.SavedWithWarning;
        } else if (result.Outcome == TestLabWriteOutcome.Committed) {
            statusAfterSave = TestLabSaveState.Saved;
        }
        if (disposed) {
            return;
        }
        var message = statusAfterSave == TestLabSaveState.Saved
            ? $"Test plan '{id:D}' was saved."
            : $"Test plan '{id:D}' was saved; refresh incomplete. Retry refresh to review the committed state.";
        if (submission.BelongsTo(State.Draft)) {
            origin.Finish(submission, statusAfterSave, message);
        }
        Receipt(new(statusAfterSave, id, message));
    }

    public async Task RetryAsync() {
        if (disposed) {
            return;
        }
        var draft = State.Draft;
        var submission = draft?.LastCommitted;
        await Task.WhenAll(LoadProjectsAsync(), LoadListAsync());
        if (disposed || !ReferenceEquals(draft, State.Draft)) {
            return;
        }
        if (draft is null && requestedPlan is { } id) {
            await SelectAsync(id);
        } else if (draft is not null) {
            if (submission is not null && draft.Pending is null && await RefreshCommittedAsync(draft, submission, refreshList: false) && submission.BelongsTo(State.Draft)) {
                draft.SaveState = TestLabSaveState.Saved;
                draft.Message = $"Test plan '{submission.CommittedId:D}' refreshed. Newer edits are retained.";
            }
            if (!disposed && ReferenceEquals(draft, State.Draft)) {
                await LoadPartiesAsync(draft);
            }
        }
        Changed();
    }

    private async Task<bool> RefreshCommittedAsync(TestLabDraft origin, TestLabSubmission submission, bool refreshList = true) {
        if (!IsCurrentCommit(origin, submission) || submission.CommittedId is not { } id) {
            return false;
        }
        using var read = planReads.Begin();
        try {
            if (refreshList) {
                await LoadListAsync();
            }
            if (!planReads.Accept(read) || !IsCurrentCommit(origin, submission)) {
                return false;
            }
            var accepted = await owner.GetAsync(id, read.Token);
            if (!planReads.Accept(read) || !IsCurrentCommit(origin, submission)) {
                return false;
            }
            if (accepted?.Id != id) {
                State.EditorRead = TestLabReadState.Stale;
                return false;
            }
            submission.Reconcile(origin, accepted);
            State.EditorRead = TestLabReadState.Ready;
            return State.PlansRead is TestLabReadState.Ready or TestLabReadState.Empty;
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
            return false;
        } catch (Exception exception) {
            if (planReads.Accept(read) && IsCurrentCommit(origin, submission)) {
                State.EditorRead = TestLabReadState.Stale;
                ReadFailed(exception, "committed plan", id);
            }
            return false;
        }
    }

    private async Task LoadListAsync() {
        using var read = listReads.Begin();
        State.PlansRead = TestLabReadState.Loading;
        Changed();
        try {
            var plans = await owner.ListAsync(read.Token);
            if (!disposed && listReads.Accept(read)) {
                State.Plans = plans;
                State.PlansRead = plans.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
                Changed();
            }
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!disposed && listReads.Accept(read)) {
                State.PlansRead = State.Plans.Count == 0 ? TestLabReadState.Unavailable : TestLabReadState.Stale;
                ReadFailed(exception, "list", null);
            }
        }
    }

    private async Task LoadProjectsAsync() {
        using var read = projectReads.Begin();
        State.ProjectsRead = TestLabReadState.Loading;
        try {
            var projects = await owner.ProjectsAsync(read.Token);
            if (!disposed && projectReads.Accept(read)) {
                State.Projects = projects;
                State.ProjectsRead = projects.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
                Changed();
            }
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (!disposed && projectReads.Accept(read)) {
                State.ProjectsRead = State.Projects.Count == 0 ? TestLabReadState.Unavailable : TestLabReadState.Stale;
                ReadFailed(exception, "projects", null);
            }
        }
    }

    private async Task LoadPartiesAsync(TestLabDraft origin) {
        using var read = partyReads.Begin();
        var version = origin.TargetVersion;
        var projectId = origin.Model.ProjectId;
        var partyId = origin.Model.ResponsiblePartyId;
        State.Parties = [];
        State.PartiesRead = TestLabReadState.Loading;
        Changed();
        bool Current() => !disposed && partyReads.Accept(read) && ReferenceEquals(State.Draft, origin) &&
            origin.TargetVersion == version && origin.Model.ResponsiblePartyId == partyId;
        try {
            var options = projectId is { } id ? await owner.PartiesAsync(id, read.Token) : [];
            if (!Current()) {
                return;
            }
            if (partyId is { } savedId && options.All(item => item.PartyId != savedId)) {
                var saved = await owner.PartyAsync(savedId, read.Token);
                if (!Current()) {
                    return;
                }
                if (saved is not null) {
                    options = options.Append(saved).OrderBy(item => item.DisplayName).ToArray();
                }
            }
            State.Parties = options;
            State.PartiesRead = options.Count == 0 ? TestLabReadState.Empty : TestLabReadState.Ready;
            Changed();
        } catch (OperationCanceledException) when (read.Token.IsCancellationRequested) {
        } catch (Exception exception) {
            if (Current()) {
                State.PartiesRead = TestLabReadState.Unavailable;
                ReadFailed(exception, "parties", projectId);
            }
        }
    }

    private void RetireEditor() {
        planReads.Retire();
        partyReads.Retire();
        State.Draft = null;
        State.Parties = [];
        State.Section = TestLabSection.Overview;
    }

    private void Receipt(TestLabReceipt receipt) {
        State.Receipt = receipt;
        notify(receipt);
        Changed();
    }

    private void ReadFailed(Exception exception, string scope, Guid? id) {
        logger.LogWarning("TestLab read failed. Scope={Scope} Id={Id} FailureType={FailureType}", scope, id, exception.GetType().Name);
        Changed();
    }

    private void Changed() {
        if (!disposed) {
            changed();
        }
    }

    private bool AcceptIntent(TestLabDraft origin, long? version) => !disposed && ReferenceEquals(State.Draft, origin) &&
        (version is null || version == origin.TargetVersion);

    private bool IsCurrentCommit(TestLabDraft origin, TestLabSubmission submission) => !disposed &&
        submission.BelongsTo(State.Draft) && ReferenceEquals(origin.LastCommitted, submission);

    public void Dispose() {
        disposed = true;
        routeVersion++;
        planReads.Retire();
        listReads.Retire();
        projectReads.Retire();
        partyReads.Retire();
    }

    private sealed class ReadLane {
        private Request? current;
        public Request Begin() {
            Retire();
            current = new();
            return current;
        }
        public bool Accept(Request request) => ReferenceEquals(current, request);
        public void Retire() {
            current?.Cancel();
            current = null;
        }
    }

    private sealed class Request : IDisposable {
        private readonly CancellationTokenSource source = new();
        private bool finished;
        public CancellationToken Token { get; }
        public Request() => Token = source.Token;
        public void Cancel() {
            if (!finished) {
                source.Cancel();
            }
        }
        public void Dispose() {
            finished = true;
            source.Dispose();
        }
    }
}
