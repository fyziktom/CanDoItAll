using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RecruitmentApplicationSessionTests {
    [Fact]
    public async Task A_peer_hint_keeps_a_clean_application_until_explicit_pull_without_an_automatic_query() {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        var noticed = Signal();
        await using var session = Session(owner, changes, feed, notice: () => noticed.TrySetResult());
        Assert.True(await session.OpenAsync(model));
        var draft = session.State.Draft;
        var reads = owner.Reads;
        owner.Models[model.Id!.Value].DesiredRole = "Peer role";
        Stamp(owner.Models[model.Id.Value]);

        changes.PublishRecruitment(model.Id.Value);
        await noticed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(reads, owner.Reads);
        Assert.Equal("Original role", draft.DesiredRole);
        Assert.False(session.CanSave);
        await session.RefreshApplicationAsync();
        Assert.Same(draft, session.State.Draft);
        Assert.Equal("Peer role", draft.DesiredRole);
        Assert.True(session.CanSave);
        Assert.Null(session.Message);
    }

    [Fact]
    public async Task Known_acceptance_keeps_later_input_and_uses_the_exact_native_original() {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        var entered = Signal();
        var release = Signal();
        RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>? accepted = null;
        owner.Save = async (submitted, acknowledge) => {
            entered.TrySetResult();
            await release.Task;
            var saved = submitted.Snapshot();
            saved.DesiredRole = "Canonical role";
            Stamp(saved);
            owner.Models[saved.Id!.Value] = saved.Snapshot();
            accepted = new(saved.Id.Value, saved);
            acknowledge!(accepted);
            changes.PublishRecruitment(saved.Id.Value);
            return Result<Guid>.Success(saved.Id.Value);
        };
        await using var session = Session(owner, changes, feed);
        Assert.True(await session.OpenAsync(model));
        session.State.Draft.Notes = "Submitted notes";
        var save = session.SaveAsync(session.State.Draft);
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            session.State.Draft.Notes = "Later notes";
            session.State.Input(new(CanDoItAll.CrmHr.UI.Recruiting.RecruitmentApplicationField.Notes));
        } finally {
            release.TrySetResult();
        }

        Assert.True(await save);
        Assert.Equal("Later notes", session.State.Draft.Notes);
        Assert.Equal("Canonical role", session.State.Draft.DesiredRole);
        Assert.Equal(accepted!.Accepted.Original, session.State.Draft.Original);
        Assert.False(session.State.IsOutdated);
        Assert.True(session.State.IsDirty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failed_continuation_is_unknown_without_a_receipt_and_known_after_acceptance(bool acknowledge) {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        owner.Save = (submitted, callback) => {
            if (acknowledge) {
                var saved = submitted.Snapshot();
                Stamp(saved);
                callback!(new(saved.Id!.Value, saved));
            }
            return Task.FromException<Result<Guid>>(new IOException("Native continuation failed."));
        };
        await using var session = Session(owner, changes, feed);
        Assert.True(await session.OpenAsync(model));
        session.State.Draft.Notes = "Retained notes";

        Assert.Equal(acknowledge, await session.SaveAsync(session.State.Draft));
        await session.RefreshApplicationAsync();

        Assert.Equal(!acknowledge, session.State.IsUnknown);
        Assert.Equal("Retained notes", session.State.Draft.Notes);
        if (!acknowledge) {
            Assert.Equal("Retained notes", session.State.UnknownSubmission!.Model.Notes);
            Assert.False(session.CanSave);
        }
    }

    [Fact]
    public async Task A_created_application_retargets_its_observations_without_replacing_the_draft() {
        var owner = new Owner();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        owner.Save = (submitted, callback) => {
            var accepted = submitted.Snapshot();
            accepted.Id = Guid.NewGuid();
            accepted.PartyId = Guid.NewGuid();
            Stamp(accepted);
            owner.Models[accepted.Id.Value] = accepted.Snapshot();
            callback!(new(accepted.Id.Value, accepted));
            return Task.FromResult(Result<Guid>.Success(accepted.Id.Value));
        };
        await using var session = Session(owner, changes, feed);
        Assert.True(await session.OpenAsync(new() { CandidateName = "Created candidate", DesiredRole = "Created role" }));
        Assert.Equal(0, feed.ActiveSubscriptions);
        var draft = session.State.Draft;

        Assert.True(await session.SaveAsync(draft));

        Assert.Same(draft, session.State.Draft);
        Assert.NotNull(draft.Id);
        Assert.Equal(5, feed.ActiveSubscriptions);
        Assert.True(session.CanSave);
        await session.CloseAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Fact]
    public async Task A_delayed_comparison_from_a_retired_record_cannot_enter_the_next_opening() {
        var owner = new Owner();
        var first = owner.Add();
        var next = owner.Add();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        await using var session = Session(owner, changes, feed);
        Assert.True(await session.OpenAsync(first));
        var entered = Signal();
        var release = Signal();
        owner.Read = async id => {
            if (id == first.Id) {
                entered.TrySetResult();
                await release.Task;
            }
            return new(owner.Models[id].Snapshot(), new Dictionary<Guid, string>());
        };
        var reviewing = session.ReviewAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await session.OpenAsync(next));
        } finally {
            release.TrySetResult();
        }
        await reviewing;
        Assert.Equal(next.Id, session.State.Draft.Id);
        Assert.Null(session.State.Review);
    }

    [Fact]
    public async Task Admission_rechecks_the_opening_after_its_async_access_validation() {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        var access = new WaitingAccess();
        await using var session = Session(owner, Changes(feed), feed, access: access);
        Assert.True(await session.OpenAsync(model));
        var admission = session.CaptureAdmission(RecruitmentWriteArea.Application, write: true);
        access.Hold = true;
        var checking = admission(default).AsTask();
        try {
            await access.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await session.CloseAsync();
        } finally {
            access.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => checking);
    }

    [Fact]
    public async Task Repeated_openings_release_all_five_targeted_subscriptions() {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        await using var session = Session(owner, Changes(feed), feed);
        for (var index = 0; index < 100; index++) {
            Assert.True(await session.OpenAsync(model));
            Assert.Equal(5, feed.ActiveSubscriptions);
            await session.CloseAsync();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }
    }

    [Fact]
    public async Task A_child_row_hint_signals_its_context_without_changing_the_application_basis() {
        var owner = new Owner();
        var model = owner.Add();
        var feed = new EditingChangeFeed();
        var changes = Changes(feed);
        var received = new TaskCompletionSource<RecruitmentWriteArea>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = Session(owner, changes, feed, context: area => received.TrySetResult(area));
        Assert.True(await session.OpenAsync(model));
        var reads = owner.Reads;
        var original = session.State.Draft.Original;

        changes.PublishRecruitmentRecords(model.Id!.Value);

        Assert.Equal(RecruitmentWriteArea.Interviews, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(reads, owner.Reads);
        Assert.Equal(original, session.State.Draft.Original);
        Assert.False(session.State.IsOutdated);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static CrmHrChanges Changes(EditingChangeFeed feed) => new(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
    private static RecruitmentApplicationSession Session(Owner owner, CrmHrChanges changes, EditingChangeFeed feed,
        Action? notice = null, Action<RecruitmentWriteArea>? context = null, IEditingUiAccess? access = null) =>
        new(owner, changes, feed, access ?? new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance, NullLogger<RecruitmentApplicationSession>.Instance) {
            Dispatch = action => action(), Changed = () => { }, Retired = () => { },
            Notice = notice ?? (() => { }), ContextChanged = context ?? (_ => { })
        };
    private static void Stamp(RecruitmentApplicationEditorModel model) => model.Original = new(model.Id!.Value, model.PartyId!.Value,
        model.TargetUnitPartyId, model.RecruiterPartyId, model.HiringManagerPartyId, model.DesiredRole, model.Source, model.Stage,
        null, model.Decision, model.Notes, (model.Original?.UpdatedAtUtc ?? DateTimeOffset.UtcNow).AddTicks(10));
    private sealed class Owner : IRecruitmentApplicationEditing {
        public Dictionary<Guid, RecruitmentApplicationEditorModel> Models { get; } = [];
        public int Reads { get; private set; }
        public Func<Guid, Task<RecruitmentApplicationRead?>>? Read { get; set; }
        public Func<RecruitmentApplicationEditorModel, Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>>?, Task<Result<Guid>>>? Save { get; set; }
        public RecruitmentApplicationEditorModel Add() {
            var model = new RecruitmentApplicationEditorModel { Id = Guid.NewGuid(), PartyId = Guid.NewGuid(), CandidateName = "Candidate", DesiredRole = "Original role" };
            Stamp(model);
            Models.Add(model.Id.Value, model.Snapshot());
            return model;
        }
        public Task<RecruitmentApplicationRead?> ReadApplicationAsync(Guid applicationId, IReadOnlyList<Guid> referenceIds, CancellationToken cancellationToken) {
            Reads++;
            return Read?.Invoke(applicationId) ?? Task.FromResult<RecruitmentApplicationRead?>(new(Models[applicationId].Snapshot(), new Dictionary<Guid, string>()));
        }
        public Task<Result<Guid>> SaveRecruitmentApplicationAsync(RecruitmentApplicationEditorModel model, CancellationToken cancellationToken = default,
            Action<RecruitmentSaveReceipt<RecruitmentApplicationEditorModel>>? onCommitted = null) =>
            Save?.Invoke(model, onCommitted) ?? throw new InvalidOperationException("This test has no native command.");
    }
    private sealed class WaitingAccess : IEditingUiAccess {
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = Signal();
        public TaskCompletionSource Release { get; } = Signal();
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) => Task.FromResult(new EditingUiGrant(topic, "test-principal", "test-actor"));
        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            if (write && Hold) {
                Entered.TrySetResult();
                await Release.Task;
            }
        }
    }
}
