using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class RelationshipEditorTests {
    [Fact]
    public async Task Cancelling_a_comparison_fences_its_late_result_without_cancelling_a_later_submission() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyList<PartyRelationshipListItemModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = _ => {
            entered.TrySetResult();
            return response.Task;
        };
        var comparison = editor.ReviewAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(editor.ReadingReview);
        Assert.Throws<InvalidOperationException>(() => editor.PrepareSave());
        editor.CancelReview();
        var submitted = editor.PrepareSave();
        response.SetResult([]);
        await comparison;

        Assert.Null(editor.State.Review);
        Assert.True(editor.State.IsSaving);
        Assert.Equal("  Local title  ", Assert.Single(editor.State.Draft).Notes);
        Assert.Equal(0, owner.Writes);
        editor.CancelSubmission(submitted);
    }

    [Fact]
    public async Task A_retired_submission_cannot_write_or_change_the_saving_state_of_a_new_target() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        var first = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(first));
        editor.State.Draft.Add(NewRow(first));
        var oldSubmission = editor.PrepareSave();
        editor.Clear();
        var second = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(second));
        editor.State.Draft.Add(NewRow(second));
        var currentSubmission = editor.PrepareSave();

        var result = await editor.CommitAsync(oldSubmission);
        editor.CancelSubmission(oldSubmission);

        Assert.True(result.IsFailure);
        Assert.True(editor.State.IsSaving);
        Assert.False(editor.State.IsOutdated);
        Assert.Equal(second, editor.State.PartyId);
        Assert.Equal(0, owner.Writes);
        editor.CancelSubmission(currentSubmission);
    }

    [Fact]
    public async Task A_known_commit_followed_by_a_failure_retains_exact_acceptance_and_later_raw_input_without_replay() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var submitted = editor.PrepareSave();
        owner.Write = (rows, committed) => {
            committed!(new(id, [Accepted(Assert.Single(rows))]));
            var row = Assert.Single(editor.State.Draft);
            row.Notes = "  Typed after submission  ";
            editor.State.InputChanged(new(row.Id!.Value, RelationshipField.Notes));
            throw new InjectedFailure();
        };

        var result = await editor.CommitAsync(submitted);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, owner.Writes);
        Assert.Equal("explicit-editing-test-principal", owner.Actor);
        Assert.Contains("were saved", editor.Message, StringComparison.Ordinal);
        Assert.False(editor.State.IsUnknown);
        var current = Assert.Single(editor.State.Draft);
        Assert.Equal(submitted.Rows[0].Id, current.Id);
        Assert.Equal(17u, current.ExpectedVersion);
        Assert.Equal("  Typed after submission  ", current.Notes);
        Assert.Equal("Local title", Assert.Single(editor.Saved).Notes);
    }

    [Fact]
    public async Task An_unknown_commit_retains_the_submission_and_blocks_an_old_command_from_being_replayed() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var submitted = editor.PrepareSave();
        owner.Write = (_, _) => throw new InjectedFailure();

        var result = await editor.CommitAsync(submitted);
        submitted.Rows[0].Notes = "Mutated outside";
        var replay = await editor.CommitAsync(submitted);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.RelationshipOutcomeUnknown);
        Assert.True(replay.IsFailure);
        Assert.True(editor.State.IsUnknown);
        Assert.Equal("  Local title  ", Assert.Single(editor.State.UnknownSubmission!).Notes);
        Assert.Throws<InvalidOperationException>(() => editor.PrepareSave());
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public async Task One_hundred_open_review_clear_cycles_release_every_exact_person_subscription() {
        var (editor, _, feed, _) = Create();
        await using var lifetime = editor;
        for (var index = 0; index < 100; index++) {
            editor.Observe(await editor.ReadAsync(Guid.NewGuid()));
            Assert.Equal(1, feed.ActiveSubscriptions);
            await editor.ReviewAsync();
            editor.Clear();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
            Assert.Empty(editor.State.Draft);
            Assert.Empty(editor.Saved);
            Assert.Null(editor.State.Review);
        }
    }

    [Fact]
    public async Task A_clean_hint_uses_a_bounded_read_and_keeps_the_complete_previous_set_on_overflow() {
        var (editor, owner, _, changes) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        var row = Accepted(NewRow(id));
        owner.Read = _ => Task.FromResult<IReadOnlyList<PartyRelationshipListItemModel>>([row]);
        editor.Observe(await editor.ReadAsync(id));
        var draft = Assert.Single(editor.State.Draft);
        var overflow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = _ => {
            overflow.TrySetResult();
            throw new RelationshipReadLimitExceededException(PartyMergeReview.DetailedRowLimit);
        };

        changes.PublishRelationships([id]);
        await overflow.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitAsync(() => editor.Message is not null);

        Assert.Equal(PartyMergeReview.DetailedRowLimit, owner.ReadMaximum);
        Assert.Same(draft, Assert.Single(editor.State.Draft));
        Assert.Equal(row.Version, Assert.Single(editor.State.Original.Rows).Version);
        Assert.True(editor.State.IsOutdated);
        Assert.Contains("complete comparison", editor.Message, StringComparison.Ordinal);
        owner.Read = _ => Task.FromResult<IReadOnlyList<PartyRelationshipListItemModel>>([row]);
        await editor.ReviewAsync();
        Assert.Null(owner.ReadMaximum);
        Assert.NotNull(editor.State.Review);
    }

    [Fact]
    public async Task A_receipt_for_another_party_cannot_become_this_editors_accepted_state() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var submitted = editor.PrepareSave();
        owner.Write = (rows, _) => Result<PartyRelationshipSaveReceipt>.Success(new PartyRelationshipSaveReceipt(Guid.NewGuid(), [Accepted(Assert.Single(rows))]));

        var result = await editor.CommitAsync(submitted);

        Assert.True(result.IsFailure);
        Assert.True(editor.State.IsUnknown);
        Assert.Empty(editor.Saved);
        Assert.Equal("  Local title  ", Assert.Single(editor.State.Draft).Notes);
        Assert.Equal(1, owner.Writes);
    }

    private static async Task WaitAsync(Func<bool> completed) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!completed()) {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static (RelationshipEditor Editor, StubOwner Owner, EditingChangeFeed Feed, CrmHrChanges Changes) Create() {
        var feed = new EditingChangeFeed();
        var owner = new StubOwner();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var editor = new RelationshipEditor(owner, changes, feed, new EditingUiTestAccess(),
            NullLogger<EditingObservation>.Instance, NullLogger<RelationshipEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        return (editor, owner, feed, changes);
    }

    private static PartyRelationshipEditorModel NewRow(Guid party) => new() {
        Id = Guid.NewGuid(), RelatedPartyId = Guid.NewGuid(), Notes = "  Local title  "
    };

    private static PartyRelationshipListItemModel Accepted(PartyRelationshipEditorModel row) => new(
        row.Id!.Value, row.RelatedPartyId, "Peer", PartyType.Organization, row.RelationshipKind, row.IsOutgoing, row.IsPrimary,
        row.StartDateUtc, row.EndDateUtc, row.Notes.Trim()) { Version = 17 };

    private sealed class InjectedFailure : Exception;

    private sealed class StubOwner : IPartyRelationshipService {
        public int Writes { get; private set; }
        public string? Actor { get; private set; }
        public int? ReadMaximum { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<PartyRelationshipListItemModel>>>? Read { get; set; }
        public Func<IReadOnlyList<PartyRelationshipEditorModel>, Action<PartyRelationshipSaveReceipt>?, Result<PartyRelationshipSaveReceipt>>? Write { get; set; }
        public Task<IReadOnlyList<PartyRelationshipListItemModel>> ListAsync(Guid partyId, CancellationToken cancellationToken = default, int? maximumRows = null) {
            ReadMaximum = maximumRows;
            return Read?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<PartyRelationshipListItemModel>>([]);
        }
        public Task<Result<PartyRelationshipSaveReceipt>> ReplaceAsync(Guid partyId, IReadOnlyList<PartyRelationshipEditorModel> rows,
            PartyRelationshipObservation? original, string actor, CancellationToken cancellationToken = default, Action<PartyRelationshipSaveReceipt>? onCommitted = null) {
            Writes++;
            Actor = actor;
            return Task.FromResult((Write ?? throw new InvalidOperationException("Configure the owner write for this test."))(rows, onCommitted));
        }
    }
}
