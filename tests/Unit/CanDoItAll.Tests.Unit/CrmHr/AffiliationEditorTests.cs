using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class AffiliationEditorTests {
    [Fact]
    public async Task Cancelling_a_comparison_fences_its_late_result_without_cancelling_a_later_submission() {
        var (editor, owner, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        Assert.Equal("  Local title  ", Assert.Single(editor.State.Draft).JobTitle);
        Assert.Equal(0, owner.Writes);
        editor.CancelSubmission(submitted);
    }

    [Fact]
    public async Task A_retired_submission_cannot_write_or_change_the_saving_state_of_a_new_target() {
        var (editor, owner, _) = Create();
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
        Assert.Equal(second, Assert.Single(editor.State.Draft).PersonPartyId);
        Assert.Equal(0, owner.Writes);
        editor.CancelSubmission(currentSubmission);
    }

    [Fact]
    public async Task A_known_commit_followed_by_a_failure_retains_exact_acceptance_and_later_raw_input_without_replay() {
        var (editor, owner, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var submitted = editor.PrepareSave();
        owner.Write = (rows, committed) => {
            committed!([Accepted(Assert.Single(rows))]);
            var row = Assert.Single(editor.State.Draft);
            row.JobTitle = "  Typed after submission  ";
            editor.State.InputChanged(new(row.Id!.Value, AffiliationField.Title));
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
        Assert.False(current.IsNew);
        Assert.NotNull(current.ExpectedUpdatedAtUtc);
        Assert.Equal("  Typed after submission  ", current.JobTitle);
        Assert.Equal("Local title", Assert.Single(editor.Saved).JobTitle);
    }

    [Fact]
    public async Task An_unknown_commit_retains_the_submission_and_blocks_an_old_command_from_being_replayed() {
        var (editor, owner, _) = Create();
        await using var lifetime = editor;
        var id = Guid.NewGuid();
        editor.Observe(await editor.ReadAsync(id));
        editor.State.Draft.Add(NewRow(id));
        var submitted = editor.PrepareSave();
        owner.Write = (_, _) => throw new InjectedFailure();

        var result = await editor.CommitAsync(submitted);
        submitted.Rows[0].JobTitle = "Mutated outside";
        var replay = await editor.CommitAsync(submitted);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.AffiliationOutcomeUnknown);
        Assert.True(replay.IsFailure);
        Assert.True(editor.State.IsUnknown);
        Assert.Equal("  Local title  ", Assert.Single(editor.State.UnknownSubmission!).JobTitle);
        Assert.Throws<InvalidOperationException>(() => editor.PrepareSave());
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public async Task One_hundred_open_review_clear_cycles_release_every_exact_person_subscription() {
        var (editor, _, feed) = Create();
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

    private static (AffiliationEditor Editor, StubOwner Owner, EditingChangeFeed Feed) Create() {
        var feed = new EditingChangeFeed();
        var owner = new StubOwner();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var editor = new AffiliationEditor(owner, changes, feed, new EditingUiTestAccess(), new FixedClock(),
            NullLogger<EditingObservation>.Instance, NullLogger<AffiliationEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        return (editor, owner, feed);
    }

    private static PartyOrganizationAffiliationEditorModel NewRow(Guid person) => new() {
        Id = Guid.NewGuid(), IsNew = true, PersonPartyId = person, OrganizationPartyId = Guid.NewGuid(), JobTitle = "  Local title  "
    };

    private static PartyOrganizationAffiliationListItemModel Accepted(PartyOrganizationAffiliationEditorModel row) => new(
        row.Id!.Value, row.PersonPartyId, "Person", row.OrganizationPartyId, "Organization", row.AffiliationKind, row.IsPrimary,
        row.JobTitle.Trim(), row.EmployeeCode.Trim(), row.OrganizationUnitPartyId, string.Empty, row.ManagerPartyId, string.Empty,
        row.ValidFrom, row.ValidTo, row.Notes.Trim(), "explicit-editing-test-principal", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true);

    private sealed class InjectedFailure : Exception;

    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => new(2030, 1, 2, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubOwner : IPartyOrganizationAffiliationService {
        public int Writes { get; private set; }
        public string? Actor { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>>? Read { get; set; }
        public Func<IReadOnlyCollection<PartyOrganizationAffiliationEditorModel>, Action<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>?, Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>>? Write { get; set; }
        public Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAsync(Guid personPartyId, CancellationToken cancellationToken = default) =>
            Read?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>([]);
        public Task<Result<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>> ReplaceAsync(Guid personPartyId,
            IReadOnlyCollection<PartyOrganizationAffiliationEditorModel> affiliations, string actor, CancellationToken cancellationToken = default,
            Action<IReadOnlyList<PartyOrganizationAffiliationListItemModel>>? onCommitted = null) {
            Writes++;
            Actor = actor;
            return Task.FromResult((Write ?? throw new InvalidOperationException("Configure the owner write for this test."))(affiliations, onCommitted));
        }
        public Task<Result<PartyOrganizationAffiliationListItemModel>> UpsertAsync(PartyOrganizationAffiliationEditorModel affiliation, string actor,
            DateTimeOffset? expectedUpdatedAtUtc = null, CancellationToken cancellationToken = default, Action<PartyOrganizationAffiliationListItemModel>? onCommitted = null) =>
            throw new NotSupportedException();
    }
}
