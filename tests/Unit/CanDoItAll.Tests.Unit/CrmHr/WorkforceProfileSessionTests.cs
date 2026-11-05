using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class WorkforceProfileSessionTests {
    [Fact]
    public async Task A_clean_profile_hint_requires_explicit_pull_and_does_not_read_or_replace_the_draft() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var noticed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = Session(owner, changes, feed, notice: () => noticed.TrySetResult());
        var party = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        var draft = session.State.Draft;
        var current = owner.Models[party];
        current.JobTitle = "Peer title";
        current.Original = new(party, PartyType.Person, current.Id, WorkforceProfileValues.From(current));

        changes.PublishParty(party);
        await noticed.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, owner.CurrentReads);
        Assert.Same(draft, session.State.Draft);
        Assert.Equal("Original title", draft.JobTitle);
        Assert.True(session.State.IsOutdated);
        Assert.False(session.State.CanSave);
        Assert.Contains("Another editor", session.Message);
        session.Observe(await session.ReadWorkspaceAsync(party, default), refreshDraft: true);
        Assert.Same(draft, session.State.Draft);
        Assert.Equal("Peer title", draft.JobTitle);
        Assert.False(session.State.IsOutdated);
        Assert.Null(session.Message);
    }

    [Fact]
    public async Task A_context_read_cannot_silently_replace_a_clean_profile_without_explicit_refresh() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var party = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        var draft = session.State.Draft;
        var current = owner.Models[party];
        current.JobTitle = "Peer title during another command's follow-up";
        current.Original = new(party, PartyType.Person, current.Id, WorkforceProfileValues.From(current));

        session.Observe(await session.ReadWorkspaceAsync(party, default));

        Assert.Same(draft, session.State.Draft);
        Assert.Equal("Original title", draft.JobTitle);
        Assert.True(session.State.IsOutdated);
        Assert.False(session.State.CanSave);
    }

    [Fact]
    public async Task Three_scoped_observations_release_one_hundred_openings_and_filter_own_and_other_worker_records() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var records = 0;
        var catalog = 0;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = Session(owner, changes, feed, records: () => { records++; received.TrySetResult(); }, catalog: () => catalog++);
        var party = owner.Add();
        for (var index = 0; index < 100; index++) {
            session.Observe(await session.ReadWorkspaceAsync(party, default));
            Assert.Equal(3, feed.ActiveSubscriptions);
            await session.CloseAsync();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        using (session.EnterWrite(party)) {
            changes.PublishWorkforceRecords(party, EditingChangeKind.Created);
            changes.PublishSkillCatalog(Guid.NewGuid(), EditingChangeKind.Created);
        }
        changes.PublishWorkforceRecords(Guid.NewGuid(), EditingChangeKind.Created);
        changes.PublishWorkforceRecords(party, EditingChangeKind.Created);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, records);
        Assert.Equal(0, catalog);
        Assert.Equal(0, owner.CurrentReads);
    }

    [Fact]
    public async Task A_late_current_read_cannot_replace_the_next_opening_or_leave_its_subscriptions() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var first = owner.Add();
        var second = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(first, default));
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = (_, _) => {
            reached.TrySetResult();
            return release.Task;
        };
        var pending = session.ReviewAsync();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        session.Observe(await session.ReadWorkspaceAsync(second, default));
        var next = session.State.Draft;
        release.TrySetResult((owner.Models[first].Snapshot(), new Dictionary<Guid, string>()));
        await pending.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Same(next, session.State.Draft);
        Assert.Equal(second, next.PartyId);
        Assert.Null(session.State.Review);
        Assert.False(session.Reading);
        Assert.Equal(3, feed.ActiveSubscriptions);
        await session.CloseAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task Known_receipt_survives_failed_follow_up_and_preserves_later_input_without_replaying() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var party = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = 0;
        owner.Save = async (model, committed) => {
            writes++;
            entered.TrySetResult();
            await release.Task;
            var accepted = model.Snapshot();
            accepted.JobTitle = accepted.JobTitle.Trim();
            accepted.Original = new(party, PartyType.Person, accepted.Id, WorkforceProfileValues.From(accepted));
            owner.Models[party] = accepted;
            committed?.Invoke(new(accepted.Id!.Value, accepted));
            throw new InvalidOperationException("Failed post-commit projection");
        };
        session.State.Draft.JobTitle = "  Submitted title  ";
        session.State.InputChanged(new(WorkforceProfileField.JobTitle));
        var save = session.SaveAsync(session.State.Draft);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        session.State.Draft.Notes = "  Later raw notes  ";
        session.State.InputChanged(new(WorkforceProfileField.Notes));
        release.TrySetResult();

        Assert.True(await save.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, writes);
        Assert.False(session.State.IsUnknown);
        Assert.Equal("Submitted title", session.State.Draft.JobTitle);
        Assert.Equal("  Later raw notes  ", session.State.Draft.Notes);
        Assert.Contains("committed", session.Message);
        Assert.NotNull(session.State.Accepted);
    }

    [Fact]
    public async Task Unknown_outcome_and_submission_survive_successful_and_missing_current_reads() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var party = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        session.State.Draft.Notes = "  Unknown raw draft  ";
        owner.Save = (_, _) => Task.FromException<Result<Guid>>(new IOException("Interrupted owner continuation"));

        Assert.False(await session.SaveAsync(session.State.Draft));
        await session.ReviewAsync();
        Assert.True(session.State.IsUnknown);
        Assert.False(session.State.CanApply);
        owner.Models.Remove(party);
        await session.ReviewAsync();

        Assert.True(session.State.IsUnknown);
        Assert.False(session.State.CanSave);
        Assert.Contains("outcome is unknown", session.Message);
        Assert.Equal("  Unknown raw draft  ", session.State.Draft.Notes);
        Assert.NotNull(session.State.UnknownSubmission);
    }

    [Fact]
    public async Task A_mismatched_owner_read_cannot_install_another_profile() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var party = owner.Add();
        owner.Models[party].PartyId = Guid.NewGuid();

        var failure = await Record.ExceptionAsync(() => session.ReadWorkspaceAsync(party, default));

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Equal(Guid.Empty, session.State.Draft.PartyId);
        Assert.Null(session.CurrentProfile);
    }

    [Fact]
    public async Task An_explicit_native_authority_rejection_does_not_become_an_unknown_commit() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        await using var session = Session(owner, changes, feed);
        var party = owner.Add();
        session.Observe(await session.ReadWorkspaceAsync(party, default));
        session.State.Draft.Notes = "  Retained draft  ";
        owner.Save = (_, _) => Task.FromException<Result<Guid>>(new UnauthorizedAccessException("Rejected before commit"));

        Assert.False(await session.SaveAsync(session.State.Draft));

        Assert.False(session.State.IsUnknown);
        Assert.Null(session.State.Accepted);
        Assert.Equal("  Retained draft  ", session.State.Draft.Notes);
        Assert.Contains("access is required", session.Message);
    }

    private static WorkforceProfileSession Session(Owner owner, CrmHrChanges changes, EditingChangeFeed feed,
        Action? records = null, Action? catalog = null, Action? notice = null) => new(owner, changes, feed, new EditingUiTestAccess(),
            NullLogger<EditingObservation>.Instance, NullLogger<WorkforceProfileSession>.Instance) {
        Dispatch = action => action(), Changed = () => { }, Retired = () => { }, Notice = notice ?? (() => { }),
        RecordsChanged = records ?? (() => { }), CatalogChanged = catalog ?? (() => { })
    };

    private sealed class Owner : IWorkforceProfileEditing {
        public Dictionary<Guid, WorkforceProfileEditorModel> Models { get; } = [];
        public int CurrentReads { get; private set; }
        public Func<Guid, CancellationToken, Task<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?>>? Read { get; set; }
        public Func<WorkforceProfileEditorModel, Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>?, Task<Result<Guid>>>? Save { get; set; }
        public Guid Add() {
            var model = new WorkforceProfileEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), JobTitle = "Original title" };
            model.Original = new(model.PartyId, PartyType.Person, model.Id, WorkforceProfileValues.From(model));
            Models.Add(model.PartyId, model);
            return model.PartyId;
        }
        public Task<WorkforceProfileWorkspaceModel?> GetWorkforceProfileWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Models.TryGetValue(partyId, out var model) ? new WorkforceProfileWorkspaceModel(partyId, "Worker", "", PartyType.Person,
                PartyLifecycleStatus.Active, false, "test", DateTimeOffset.UtcNow, [], "", "", "", "", model.Snapshot(), [], []) : null);
        public Task<(WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)?> ReadWorkforceProfileEditorAsync(Guid partyId, CancellationToken cancellationToken) {
            CurrentReads++;
            if (Read is not null) {
                return Read(partyId, cancellationToken);
            }
            (WorkforceProfileEditorModel Profile, IReadOnlyDictionary<Guid, string> Names)? current = Models.TryGetValue(partyId, out var model)
                ? (model.Snapshot(), new Dictionary<Guid, string>()) : null;
            return Task.FromResult(current);
        }
        public Task<Result<Guid>> SaveWorkforceProfileAsync(WorkforceProfileEditorModel model, CancellationToken cancellationToken = default,
            Action<WorkforceSaveReceipt<WorkforceProfileEditorModel>>? onCommitted = null) =>
            Save?.Invoke(model, onCommitted) ?? throw new InvalidOperationException("This test has no owner command.");
    }
}
