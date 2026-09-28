using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.TestLab.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.TestLab;

public sealed class TestLabSessionTests {
    [Fact]
    public void Extracted_contracts_preserve_defaults_identity_and_json_shape() {
        Assert.Equal("CanDoItAll.Modules.TestLab.Contracts", typeof(TestPlanEditorModel).Assembly.GetName().Name);
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<TestCaseStatus>().Select(value => (int)value));
        Assert.Equal("Screenshot", new TestEvidenceEditorModel().EvidenceKind);
        Assert.Equal("Playwright", new TestRunEditorModel().Runner);
        Assert.Equal(TestCaseStatus.Planned, new TestCaseEditorModel().Status);
        var summary = new TestPlanSummary(Guid.NewGuid(), Guid.NewGuid(), "Plan", "Phase", 1, 1, null, DateTimeOffset.UtcNow) { ProjectLifetimeId = Guid.NewGuid() };
        Assert.DoesNotContain(nameof(summary.ProjectLifetimeId), JsonSerializer.Serialize(summary));
    }

    [Fact]
    public async Task Repeated_route_preserves_draft_context_section_raw_input_and_admission() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        var draft = fixture.Draft;
        draft.Model.Title = "Unsaved";
        draft.Model.Runs.Add(new());
        draft.InputTimestamp(draft.Model.Runs[0], "unfinished");
        fixture.Session.State.Section = TestLabSection.Evidence;
        fixture.Session.State.Search = "filter";
        var reads = fixture.Owner.Reads;
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        Assert.Same(draft, fixture.Draft);
        Assert.Equal(reads, fixture.Owner.Reads);
        Assert.Same(fixture.Owner.Project.Admission, draft.Model.ExpectedProjectAdmission);
        Assert.Equal("unfinished", draft.Timestamp(draft.Model.Runs[0]));
        Assert.False(draft.Context.Validate());
        Assert.Equal(TestLabSection.Evidence, fixture.Session.State.Section);
        Assert.Equal("filter", fixture.Session.State.Search);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Plan_A_B_A_ignores_late_success_error_and_cancellation(int completion) {
        using var fixture = new Fixture();
        var old = new TaskCompletionSource<TestPlanEditorModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var calls = 0;
        fixture.Owner.ReadPlan = (id, _) => ++calls == 1 ? old.Task : Task.FromResult<TestPlanEditorModel?>(new() { Id = id, Title = $"Latest {calls}" });
        var pending = fixture.Session.SelectAsync(a);
        await fixture.Session.SelectAsync(b);
        await fixture.Session.SelectAsync(a);
        var current = fixture.Draft;
        switch (completion) {
            case 0:
                old.SetResult(new() { Id = a, Title = "Stale" });
                break;
            case 1:
                old.SetException(new InvalidOperationException("Controlled old failure"));
                break;
            default:
                old.SetCanceled();
                break;
        }
        await pending;
        Assert.Same(current, fixture.Draft);
        Assert.Equal("Latest 3", current.Model.Title);
        Assert.Equal(TestLabReadState.Ready, fixture.Session.State.EditorRead);
    }

    [Fact]
    public async Task Reset_retires_pending_plan_and_initial_route_requests() {
        using var fixture = new Fixture();
        var gate = new TaskCompletionSource<IReadOnlyList<TestLabProjectOption>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Owner.ReadProjects = _ => gate.Task;
        var pending = fixture.Session.ApplyRouteAsync(Guid.NewGuid(), null);
        await fixture.Session.NewAsync();
        var draft = fixture.Draft;
        draft.Model.Title = "Explicit reset";
        gate.SetResult([fixture.Owner.Project]);
        await pending;
        Assert.Same(draft, fixture.Draft);
        Assert.Equal("Explicit reset", draft.Model.Title);
    }

    [Fact]
    public async Task Party_A_B_A_fences_list_and_saved_fallback_independently_of_current_values() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        var oldList = new TaskCompletionSource<IReadOnlyList<TestLabPartyOption>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldFallback = new TaskCompletionSource<TestLabPartyOption?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saved = Guid.NewGuid();
        fixture.Draft.Model.ResponsiblePartyId = saved;
        fixture.Owner.ReadParties = (_, _) => oldList.Task;
        fixture.Owner.ReadParty = (_, _) => oldFallback.Task;
        var first = fixture.Session.ChangeProjectAsync(fixture.Draft, fixture.Owner.Project.Id);
        oldList.SetResult([]);
        await fixture.Owner.FallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Owner.ReadParties = (_, _) => Task.FromResult<IReadOnlyList<TestLabPartyOption>>([new(saved, "Current")]);
        await fixture.Session.ChangeProjectAsync(fixture.Draft, Guid.NewGuid());
        await fixture.Session.ChangeProjectAsync(fixture.Draft, fixture.Owner.Project.Id);
        oldFallback.SetResult(new(saved, "Old fallback"));
        await first;
        Assert.Equal("Current", Assert.Single(fixture.Session.State.Parties).DisplayName);
        Assert.Equal(saved, fixture.Draft.Model.ResponsiblePartyId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_party_missing_or_failed_lookup_retains_id_and_explicit_read_state(bool fail) {
        using var fixture = new Fixture();
        var saved = Guid.NewGuid();
        fixture.Owner.ReadPlan = (id, _) => Task.FromResult<TestPlanEditorModel?>(new() { Id = id, ResponsiblePartyId = saved });
        fixture.Owner.ReadParty = (_, _) => fail ? Task.FromException<TestLabPartyOption?>(new InvalidOperationException()) : Task.FromResult<TestLabPartyOption?>(null);
        await fixture.Session.SelectAsync(Guid.NewGuid());
        Assert.Equal(saved, fixture.Draft.Model.ResponsiblePartyId);
        Assert.Empty(fixture.Session.State.Parties);
        Assert.Equal(fail ? TestLabReadState.Unavailable : TestLabReadState.Empty, fixture.Session.State.PartiesRead);
    }

    [Fact]
    public async Task Party_change_retires_a_pending_saved_lookup_without_leaving_loading_state() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        var old = new TaskCompletionSource<TestLabPartyOption?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Owner.ReadParty = (_, _) => old.Task;
        var pending = fixture.Session.ChangePartyAsync(fixture.Draft, Guid.NewGuid());
        await fixture.Owner.FallbackEntered.Task;
        await fixture.Session.ChangePartyAsync(fixture.Draft, null);
        old.SetResult(new(Guid.NewGuid(), "Retired party"));
        await pending;
        Assert.Null(fixture.Draft.Model.ResponsiblePartyId);
        Assert.Empty(fixture.Session.State.Parties);
        Assert.Equal(TestLabReadState.Empty, fixture.Session.State.PartiesRead);
    }

    [Fact]
    public async Task Callbacks_from_an_old_render_cannot_save_or_change_the_current_project_target() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        var draft = fixture.Draft;
        draft.Model.Title = "Draft";
        var oldVersion = draft.TargetVersion;
        await fixture.Session.ChangeProjectAsync(draft, fixture.Owner.Project.Id);
        await fixture.Session.SaveAsync(draft, oldVersion);
        await fixture.Session.ChangeProjectAsync(draft, null, oldVersion);
        await fixture.Session.ChangePartyAsync(draft, Guid.NewGuid(), oldVersion);
        Assert.Equal(0, fixture.Owner.Writes);
        Assert.Equal(fixture.Owner.Project.Id, draft.Model.ProjectId);
        Assert.Null(draft.Model.ResponsiblePartyId);
    }

    [Fact]
    public async Task Failed_empty_filtered_missing_and_stale_reads_are_distinct() {
        using var fixture = new Fixture();
        fixture.Owner.FailList = true;
        await fixture.Session.ApplyRouteAsync(null, null);
        Assert.Equal(TestLabReadState.Unavailable, fixture.Session.State.PlansRead);
        fixture.Owner.FailList = false;
        await fixture.Session.RetryAsync();
        Assert.Equal(TestLabReadState.Empty, fixture.Session.State.PlansRead);
        fixture.Draft.Model.Title = "Saved";
        await fixture.Session.SaveAsync(fixture.Draft);
        fixture.Session.State.Search = "no match";
        Assert.Empty(fixture.Session.State.VisiblePlans);
        Assert.Equal(TestLabReadState.Ready, fixture.Session.State.PlansRead);
        fixture.Owner.FailList = true;
        await fixture.Session.RetryAsync();
        Assert.Equal(TestLabReadState.Stale, fixture.Session.State.PlansRead);
        Assert.Single(fixture.Session.State.Plans);
        await fixture.Session.SelectAsync(Guid.NewGuid());
        Assert.Null(fixture.Session.State.Draft);
        Assert.Equal(TestLabReadState.Missing, fixture.Session.State.EditorRead);
    }

    [Fact]
    public async Task Snapshot_and_single_submission_are_bound_to_origin_while_successor_can_save() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        var first = fixture.Draft;
        first.Model.Title = "First";
        first.Model.Cases.Add(new() { Name = "Original case" });
        var gate = new Gate();
        fixture.Owner.BeforeWrite = gate.WaitAsync;
        var pending = fixture.Session.SaveAsync(first);
        await gate.Entered.Task;
        await fixture.Session.SaveAsync(first);
        first.Model.Title = "Changed after dispatch";
        first.Model.Cases[0].Name = "Changed child";
        Assert.Equal(1, fixture.Owner.Writes);
        await fixture.Session.NewAsync();
        var successor = fixture.Draft;
        successor.Model.Title = "Successor";
        fixture.Owner.BeforeWrite = null;
        await fixture.Session.SaveAsync(successor);
        gate.Release();
        await pending;
        Assert.Same(successor, fixture.Draft);
        Assert.Equal(2, fixture.Owner.Store.Count);
        var stored = Assert.Single(fixture.Owner.Store.Values, item => item.Title == "First");
        Assert.Equal("Original case", Assert.Single(stored.Cases).Name);
        Assert.Null(first.Model.Id);
        Assert.Equal("Successor", successor.Model.Title);
    }

    [Fact]
    public async Task Readback_reconciles_fields_and_original_rows_without_changing_context_tabs_or_filters() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        var draft = fixture.Draft;
        draft.Model.Title = "  Submitted  ";
        draft.Model.Phase = "  Phase  ";
        draft.Model.Cases = [new() { Name = "  First  " }, new() { Name = "Second" }];
        draft.Model.Evidence = [new() { EvidenceLabel = "Evidence" }];
        draft.Model.Runs = [new() { Runner = "Runner" }];
        var first = draft.Model.Cases[0];
        var removed = draft.Model.Cases[1];
        var gate = new TaskCompletionSource<TestPlanEditorModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Owner.ReadPlan = (_, _) => gate.Task;
        var pending = fixture.Session.SaveAsync(draft);
        draft.Model.Title = "Newer title";
        draft.Model.Cases.Remove(removed);
        var replacement = new TestCaseEditorModel { Name = "Second" };
        draft.Model.Cases.Insert(0, replacement);
        first.Notes = "Newer notes";
        draft.InputTimestamp(draft.Model.Runs[0], "invalid raw time");
        fixture.Session.State.Section = TestLabSection.Runs;
        fixture.Session.State.Search = "keep filter";
        var accepted = TestLabSubmission.Clone(Assert.Single(fixture.Owner.Store.Values));
        accepted.Cases.Reverse();
        gate.SetResult(accepted);
        await pending;
        Assert.Same(draft, fixture.Draft);
        Assert.Same(draft.Model, draft.Context.Model);
        Assert.Equal("Newer title", draft.Model.Title);
        Assert.Equal("Phase", draft.Model.Phase);
        Assert.Equal("First", first.Name);
        Assert.Equal("Newer notes", first.Notes);
        Assert.NotNull(first.Id);
        Assert.Null(replacement.Id);
        Assert.Equal("invalid raw time", draft.Timestamp(draft.Model.Runs[0]));
        Assert.False(draft.Context.Validate());
        Assert.Equal(TestLabSection.Runs, fixture.Session.State.Section);
        Assert.Equal("keep filter", fixture.Session.State.Search);
    }

    [Fact]
    public async Task Project_A_B_A_does_not_patch_or_unlock_a_newer_submission() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        var draft = fixture.Draft;
        draft.Model.Title = "Original";
        var firstGate = new Gate();
        fixture.Owner.BeforeWrite = firstGate.WaitAsync;
        var first = fixture.Session.SaveAsync(draft);
        await firstGate.Entered.Task;
        await fixture.Session.ChangeProjectAsync(draft, null);
        await fixture.Session.ChangeProjectAsync(draft, fixture.Owner.Project.Id);
        var secondGate = new Gate();
        fixture.Owner.BeforeWrite = secondGate.WaitAsync;
        draft.Model.Title = "Second";
        var second = fixture.Session.SaveAsync(draft);
        await secondGate.Entered.Task;
        firstGate.Release();
        await first;
        Assert.Null(draft.Model.Id);
        Assert.NotNull(draft.Pending);
        Assert.Equal(TestLabSaveState.Pending, draft.SaveState);
        secondGate.Release();
        await second;
        Assert.Equal("Second", fixture.Owner.Store[draft.Model.Id!.Value].Title);
    }

    [Theory]
    [InlineData(TestLabWriteOutcome.Refused)]
    [InlineData(TestLabWriteOutcome.Unknown)]
    public async Task Refusal_is_editable_but_unknown_is_locked_even_after_refresh(TestLabWriteOutcome outcome) {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        fixture.Draft.Model.Title = "Submission";
        fixture.Owner.Outcome = outcome;
        await fixture.Session.SaveAsync(fixture.Draft);
        await fixture.Session.RetryAsync();
        await fixture.Session.SaveAsync(fixture.Draft);
        Assert.Equal(outcome == TestLabWriteOutcome.Refused ? 2 : 1, fixture.Owner.Writes);
        Assert.Empty(fixture.Owner.Store);
        Assert.Equal(outcome == TestLabWriteOutcome.Refused, fixture.Draft.CanSave);
        Assert.Equal(outcome == TestLabWriteOutcome.Unknown, fixture.Draft.UncertainSubmission is not null);
        if (outcome == TestLabWriteOutcome.Unknown) {
            await fixture.Session.ChangeProjectAsync(fixture.Draft, fixture.Owner.Project.Id);
            await fixture.Session.ChangeProjectAsync(fixture.Draft, null);
            await fixture.Session.SaveAsync(fixture.Draft);
            Assert.Equal(1, fixture.Owner.Writes);
            Assert.NotNull(fixture.Draft.UncertainSubmission);
            Assert.False(fixture.Draft.CanSave);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_commit_warning_retains_all_ids_and_retry_only_reads(bool missingReadback) {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        fixture.Draft.Model.Title = "Saved";
        fixture.Draft.Model.Cases.Add(new());
        fixture.Draft.Model.Evidence.Add(new());
        fixture.Draft.Model.Runs.Add(new());
        fixture.Owner.Outcome = missingReadback ? TestLabWriteOutcome.Committed : TestLabWriteOutcome.CommittedWithWarning;
        if (missingReadback) {
            fixture.Owner.ReadPlan = (_, _) => Task.FromResult<TestPlanEditorModel?>(null);
        }
        await fixture.Session.SaveAsync(fixture.Draft);
        var draft = fixture.Draft;
        Assert.Equal(TestLabSaveState.SavedWithWarning, draft.SaveState);
        Assert.NotNull(draft.Model.Id);
        Assert.NotNull(draft.Model.Cases[0].Id);
        Assert.NotNull(draft.Model.Evidence[0].Id);
        Assert.NotNull(draft.Model.Runs[0].Id);
        fixture.Owner.ReadPlan = null;
        await fixture.Session.RetryAsync();
        Assert.Equal(1, fixture.Owner.Writes);
        Assert.Single(fixture.Owner.Store);
        Assert.Same(draft, fixture.Draft);
        Assert.Equal(TestLabSaveState.Saved, draft.SaveState);
    }

    [Theory]
    [InlineData(TestLabWriteOutcome.Refused)]
    [InlineData(TestLabWriteOutcome.Unknown)]
    public async Task Refresh_cannot_use_an_earlier_receipt_to_resolve_a_later_unsuccessful_write(TestLabWriteOutcome outcome) {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        fixture.Draft.Model.Title = "First accepted edit";
        await fixture.Session.SaveAsync(fixture.Draft);
        Assert.NotNull(fixture.Draft.LastCommitted);
        fixture.Owner.Outcome = outcome;
        fixture.Draft.Model.Title = "Later edit";
        await fixture.Session.SaveAsync(fixture.Draft);
        await fixture.Session.RetryAsync();
        Assert.Equal(outcome == TestLabWriteOutcome.Unknown ? TestLabSaveState.Unknown : TestLabSaveState.Refused, fixture.Draft.SaveState);
        Assert.Null(fixture.Draft.LastCommitted);
        Assert.Equal(2, fixture.Owner.Writes);
        Assert.Equal("Later edit", fixture.Draft.Model.Title);
    }

    [Theory]
    [InlineData(TestLabWriteOutcome.Refused, false)]
    [InlineData(TestLabWriteOutcome.Refused, true)]
    [InlineData(TestLabWriteOutcome.Unknown, false)]
    [InlineData(TestLabWriteOutcome.Unknown, true)]
    public async Task Earlier_refresh_cannot_complete_over_a_later_unsuccessful_write(TestLabWriteOutcome outcome, bool holdReadback) {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        var draft = fixture.Draft;
        draft.Model.Title = "First accepted edit";
        await fixture.Session.SaveAsync(draft);
        var accepted = TestLabSubmission.Clone(Assert.Single(fixture.Owner.Store.Values));
        var gate = new Gate();
        if (holdReadback) {
            fixture.Owner.ReadPlan = async (_, _) => {
                await gate.WaitAsync();
                return accepted;
            };
        } else {
            fixture.Owner.ReadProjects = async _ => {
                await gate.WaitAsync();
                return [fixture.Owner.Project];
            };
        }
        var refresh = fixture.Session.RetryAsync();
        await gate.Entered.Task;
        fixture.Owner.Outcome = outcome;
        draft.Model.Title = "Later edit";
        await fixture.Session.SaveAsync(draft);
        gate.Release();
        await refresh;
        Assert.Equal(outcome == TestLabWriteOutcome.Unknown ? TestLabSaveState.Unknown : TestLabSaveState.Refused, draft.SaveState);
        Assert.Equal(outcome == TestLabWriteOutcome.Refused, draft.CanSave);
        Assert.Equal(outcome == TestLabWriteOutcome.Unknown, draft.UncertainSubmission is not null);
        Assert.Null(draft.LastCommitted);
        Assert.Equal(2, fixture.Owner.Writes);
        Assert.Equal("Later edit", draft.Model.Title);
    }

    [Fact]
    public async Task Project_refresh_never_recaptures_admission_and_filter_excludes_old_lifetime() {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, fixture.Owner.Project.Id);
        var admission = fixture.Draft.Model.ExpectedProjectAdmission;
        fixture.Draft.Model.Title = "Historical";
        await fixture.Session.SaveAsync(fixture.Draft);
        fixture.Owner.Project = fixture.Owner.Project with { Admission = new(Guid.NewGuid(), fixture.Owner.Project.Id, Guid.NewGuid()) };
        await fixture.Session.RetryAsync();
        Assert.Same(admission, fixture.Draft.Model.ExpectedProjectAdmission);
        fixture.Session.State.ProjectFilter = fixture.Owner.Project.Id;
        Assert.Empty(fixture.Session.State.VisiblePlans);
        Assert.Equal("Unavailable project", fixture.Session.State.ProjectName(Assert.Single(fixture.Session.State.Plans)));
        await fixture.Session.ChangeProjectAsync(fixture.Draft, fixture.Owner.Project.Id);
        Assert.Same(fixture.Owner.Project.Admission, fixture.Draft.Model.ExpectedProjectAdmission);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_ignores_noncooperative_completion_and_starts_no_followup_services(bool write) {
        using var fixture = new Fixture();
        await fixture.Session.ApplyRouteAsync(null, null);
        var gate = new Gate();
        Task pending;
        if (write) {
            fixture.Draft.Model.Title = "Dispatched";
            fixture.Owner.BeforeWrite = gate.WaitAsync;
            pending = fixture.Session.SaveAsync(fixture.Draft);
        } else {
            fixture.Owner.ReadPlan = async (id, _) => {
                await gate.WaitAsync();
                return new() { Id = id };
            };
            pending = fixture.Session.SelectAsync(Guid.NewGuid());
        }
        await gate.Entered.Task;
        fixture.Session.Dispose();
        var reads = fixture.Owner.Reads;
        var changes = fixture.Changes;
        var receipts = fixture.Receipts.Count;
        gate.Release();
        await pending;
        Assert.Equal(reads, fixture.Owner.Reads);
        Assert.Equal(changes, fixture.Changes);
        Assert.Equal(receipts, fixture.Receipts.Count);
        Assert.Equal(write ? 1 : 0, fixture.Owner.Store.Count);
    }

    private sealed class Fixture : IDisposable {
        public Owner Owner { get; } = new();
        public List<TestLabReceipt> Receipts { get; } = [];
        public int Changes { get; private set; }
        public TestLabWorkspaceSession Session { get; }
        public TestLabDraft Draft => Assert.IsType<TestLabDraft>(Session.State.Draft);
        public Fixture() => Session = new(Owner, () => Changes++, Receipts.Add, NullLogger<TestLabWorkspaceSession>.Instance);
        public void Dispose() => Session.Dispose();
    }

    private sealed class Gate {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync() {
            Entered.TrySetResult();
            await released.Task;
        }
        public void Release() => released.TrySetResult();
    }

    private sealed class Owner : ITestLabWorkspaceOwner {
        public TestLabProjectOption Project { get; set; } = CreateProject();
        public Dictionary<Guid, TestPlanEditorModel> Store { get; } = [];
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public bool FailList { get; set; }
        public TestLabWriteOutcome Outcome { get; set; } = TestLabWriteOutcome.Committed;
        public Func<Guid, CancellationToken, Task<TestPlanEditorModel?>>? ReadPlan { get; set; }
        public Func<CancellationToken, Task<IReadOnlyList<TestLabProjectOption>>>? ReadProjects { get; set; }
        public Func<Guid, CancellationToken, Task<IReadOnlyList<TestLabPartyOption>>>? ReadParties { get; set; }
        public Func<Guid, CancellationToken, Task<TestLabPartyOption?>>? ReadParty { get; set; }
        public Func<Task>? BeforeWrite { get; set; }
        public TaskCompletionSource FallbackEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken) {
            Reads++;
            return FailList ? Task.FromException<IReadOnlyList<TestPlanSummary>>(new InvalidOperationException("Controlled read failure")) :
                Task.FromResult<IReadOnlyList<TestPlanSummary>>(Store.Values.Select(item => new TestPlanSummary(item.Id!.Value, item.ProjectId,
                    item.Title, item.Phase, item.Cases.Count, item.Evidence.Count, item.Runs.LastOrDefault()?.Result, DateTimeOffset.UtcNow) {
                    ProjectLifetimeId = item.ExpectedProjectAdmission?.LifetimeId
                }).ToArray());
        }
        public Task<TestPlanEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) {
            Reads++;
            return ReadPlan?.Invoke(id, cancellationToken) ?? Task.FromResult(Store.TryGetValue(id, out var item) ? TestLabSubmission.Clone(item) : null);
        }
        public Task<IReadOnlyList<TestLabProjectOption>> ProjectsAsync(CancellationToken cancellationToken) {
            Reads++;
            return ReadProjects?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<TestLabProjectOption>>([Project]);
        }
        public Task<IReadOnlyList<TestLabPartyOption>> PartiesAsync(Guid projectId, CancellationToken cancellationToken) {
            Reads++;
            return ReadParties?.Invoke(projectId, cancellationToken) ?? Task.FromResult<IReadOnlyList<TestLabPartyOption>>([]);
        }
        public Task<TestLabPartyOption?> PartyAsync(Guid partyId, CancellationToken cancellationToken) {
            Reads++;
            FallbackEntered.TrySetResult();
            return ReadParty?.Invoke(partyId, cancellationToken) ?? Task.FromResult<TestLabPartyOption?>(null);
        }
        public async Task<TestLabWriteResult> SaveAsync(TestPlanEditorModel submission) {
            Writes++;
            if (BeforeWrite is { } wait) {
                await wait();
            }
            if (Outcome is TestLabWriteOutcome.Refused or TestLabWriteOutcome.Unknown) {
                return new(Outcome, null, "Controlled refusal or uncertainty");
            }
            submission.Id ??= Guid.NewGuid();
            foreach (var child in submission.Cases.Cast<ITestPlanChildEditor>().Concat(submission.Evidence).Concat(submission.Runs)) {
                child.Id ??= Guid.NewGuid();
            }
            var stored = TestLabSubmission.Clone(submission);
            stored.Title = stored.Title.Trim();
            stored.Phase = stored.Phase.Trim();
            foreach (var child in stored.Cases) {
                child.Name = child.Name.Trim();
            }
            Store[stored.Id!.Value] = stored;
            return new(Outcome, stored.Id, "Controlled commit");
        }
        private static TestLabProjectOption CreateProject() {
            var id = Guid.NewGuid();
            return new(id, "Project", new(Guid.NewGuid(), id, Guid.NewGuid()));
        }
    }
}
