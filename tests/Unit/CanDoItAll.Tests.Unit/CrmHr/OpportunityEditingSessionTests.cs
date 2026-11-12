using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class OpportunityEditingSessionTests {
    [Fact]
    public async Task A_known_commit_and_failed_continuation_keep_exact_acceptance_and_later_input() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        editor.Draft.Notes = "  Submitted notes  ";
        owner.Write = (submitted, committed) => {
            var accepted = submitted.Snapshot();
            accepted.Notes = submitted.Notes.Trim();
            accepted.ExpectedUpdatedAtUtc = submitted.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
            committed!(new(accepted.Id!.Value, accepted));
            editor.Draft.Notes = "  Later raw notes  ";
            editor.InputChanged(new(OpportunityField.Notes));
            throw new InjectedFailure();
        };

        var result = await editor.SaveAsync();

        Assert.True(result.IsSuccess);
        Assert.False(editor.State.IsUnknown);
        Assert.Equal("  Later raw notes  ", editor.Draft.Notes);
        Assert.Equal(owner.Saved.ExpectedUpdatedAtUtc!.Value.AddSeconds(1), editor.Draft.ExpectedUpdatedAtUtc);
        Assert.Equal("  Submitted notes  ", owner.Submitted!.Notes);
        Assert.Equal("explicit-editing-test-principal", owner.Submitted.LastChangedBy);
        Assert.Contains("was saved", editor.Message, StringComparison.Ordinal);
        Assert.Equal(1, owner.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_or_wrong_target_receipts_cannot_authorize_a_retry(bool wrongTarget) {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        editor.Draft.Title = "My uncertain submission";
        owner.Write = (submitted, _) => wrongTarget ? Task.FromResult(Result<CrmOpportunitySaveReceipt>.Success(new(Guid.NewGuid(), submitted))) : throw new InjectedFailure();

        var result = await editor.SaveAsync();
        owner.Saved = editor.Draft.Snapshot();
        owner.Saved.ExpectedUpdatedAtUtc = owner.Saved.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
        await editor.ReviewAsync();
        var repeated = await editor.SaveAsync();

        Assert.True(result.IsFailure);
        Assert.True(repeated.IsFailure);
        Assert.True(editor.State.IsUnknown);
        Assert.Equal("My uncertain submission", editor.State.UnknownSubmission!.Title);
        Assert.False(editor.State.CanApply);
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public async Task A_submission_freezes_before_authorization_and_a_retired_result_cannot_change_the_next_editor() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        editor.Draft.Title = "Frozen submission";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<Result<CrmOpportunitySaveReceipt>>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Write = (_, _) => {
            entered.SetResult();
            return completion.Task;
        };
        var saving = editor.SaveAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        editor.Draft.Title = "Later input";
        Assert.Equal("Frozen submission", owner.Submitted!.Title);
        var old = owner.Submitted.Snapshot();
        var next = OpportunityReconciliationTests.Model();
        owner.Saved = next;
        await editor.OpenAsync(next);
        editor.Draft.Notes = "New target draft";
        completion.SetResult(Result<CrmOpportunitySaveReceipt>.Success(new(old.Id!.Value, old)));
        await saving;

        Assert.Equal(next.Id, editor.Draft.Id);
        Assert.Equal("New target draft", editor.Draft.Notes);
        Assert.Equal(next.Title, editor.Draft.Title);
        Assert.False(editor.State.IsSaving);
        Assert.False(editor.State.IsUnknown);
    }

    [Fact]
    public async Task A_late_comparison_cannot_replace_a_reopened_same_target() {
        var (editor, owner, _, _) = Create();
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        var completion = new TaskCompletionSource<CrmOpportunityDetailModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = _ => completion.Task;
        var review = editor.ReviewAsync();
        editor.CancelReview();
        owner.Read = null;
        await editor.OpenAsync(owner.Saved);
        editor.Draft.Title = "New opening draft";
        completion.SetResult(Detail(owner.Saved));
        await review;

        Assert.Null(editor.State.Review);
        Assert.Equal("New opening draft", editor.Draft.Title);
        Assert.False(editor.ReadingReview);
    }

    [Fact]
    public async Task An_automatic_hint_refuses_a_partial_link_set_and_manual_comparison_reads_the_complete_record() {
        var (editor, owner, _, changes) = Create();
        await using var lifetime = editor;
        owner.Saved.Parties.Add(new() { Id = Guid.NewGuid(), PartyId = Guid.NewGuid() });
        await editor.OpenAsync(owner.Saved);
        var row = editor.Draft.Parties[0];
        owner.Read = maximum => maximum is not null ? throw new OpportunityReadLimitExceededException(maximum.Value) : Task.FromResult<CrmOpportunityDetailModel?>(Detail(owner.Saved));
        changes.PublishOpportunity(owner.Saved.Id!.Value);
        await WaitAsync(() => editor.Message is not null);

        Assert.Equal(PartyMergeReview.DetailedRowLimit, owner.Maximum);
        Assert.Same(row, Assert.Single(editor.Draft.Parties));
        Assert.True(editor.State.IsOutdated);
        await editor.ReviewAsync();
        Assert.Null(owner.Maximum);
        Assert.NotNull(editor.State.Review);
    }

    [Fact]
    public async Task Create_acceptance_retargets_the_observer_and_keeps_later_input_for_an_update() {
        var (editor, owner, feed, _) = Create();
        await using var lifetime = editor;
        var initial = owner.Saved.Snapshot();
        initial.Id = null;
        initial.ExpectedUpdatedAtUtc = null;
        var createdId = Guid.NewGuid();
        await editor.OpenAsync(initial);
        owner.Write = (submitted, committed) => {
            var accepted = submitted.Snapshot();
            accepted.Id = createdId;
            accepted.ExpectedUpdatedAtUtc = new(2030, 1, 2, 3, 4, 6, TimeSpan.Zero);
            owner.Saved = accepted;
            committed!(new(createdId, accepted));
            editor.Draft.Notes = "Typed while creating";
            editor.InputChanged(new(OpportunityField.Notes));
            return Task.FromResult(Result<CrmOpportunitySaveReceipt>.Success(new(createdId, accepted)));
        };
        Assert.True((await editor.SaveAsync()).IsSuccess);
        Assert.Equal(createdId, editor.Draft.Id);
        Assert.Equal("Typed while creating", editor.Draft.Notes);
        Assert.True(editor.State.IsDirty);
        Assert.Equal(1, feed.ActiveSubscriptions);
        owner.Write = (submitted, _) => {
            Assert.Equal(createdId, submitted.Id);
            return Task.FromResult(Result<CrmOpportunitySaveReceipt>.Failure(Error.Validation("Controlled validation refusal")));
        };
        await editor.SaveAsync();
        Assert.Equal(2, owner.Writes);
    }

    [Fact]
    public async Task One_hundred_open_review_clear_cycles_leave_no_observer_or_topic() {
        var (editor, owner, feed, _) = Create();
        await using var lifetime = editor;
        for (var index = 0; index < 100; index++) {
            await editor.OpenAsync(owner.Saved);
            await editor.ReviewAsync();
            Assert.Equal(1, feed.ActiveSubscriptions);
            editor.Clear();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
            Assert.Null(editor.State.Review);
            Assert.Empty(editor.PartyNames);
            Assert.Empty(editor.ProjectNames);
        }
    }

    private static async Task WaitAsync(Func<bool> completed) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!completed()) {
            await Task.Delay(10, timeout.Token);
        }
    }

    [Fact]
    public async Task A_project_observation_from_a_retired_opening_cannot_change_the_next_opportunity() {
        var completion = new TaskCompletionSource<ProjectWriteAdmission?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (editor, owner, _, _) = Create((_, _) => {
            entered.SetResult();
            return completion.Task;
        });
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        var requested = Guid.NewGuid();
        var selection = editor.SelectProjectAsync(requested, editor.Opening);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var previous = editor.Draft;
        owner.Saved = OpportunityReconciliationTests.Model();
        await editor.OpenAsync(owner.Saved);
        completion.SetResult(new(Guid.NewGuid(), requested, Guid.NewGuid()));
        await selection;
        Assert.Null(editor.Draft.LinkedProjectId);
        Assert.Null(previous.LinkedProjectId);
        Assert.Equal(owner.Saved.Id, editor.Draft.Id);
        Assert.NotSame(previous, editor.Draft);
    }

    [Fact]
    public async Task Repeated_clean_metadata_refreshes_keep_only_references_used_by_retained_snapshots() {
        var (editor, owner, _, changes) = Create();
        await using var lifetime = editor;
        await editor.OpenAsync(owner.Saved);
        for (var index = 0; index < 25; index++) {
            var previousOwner = owner.Saved.OwnerPartyId;
            owner.Saved = owner.Saved.Snapshot();
            owner.Saved.OwnerPartyId = Guid.NewGuid();
            owner.Saved.ExpectedUpdatedAtUtc = owner.Saved.ExpectedUpdatedAtUtc!.Value.AddSeconds(1);
            changes.PublishOpportunity(owner.Saved.Id!.Value);
            await WaitAsync(() => editor.Draft.OwnerPartyId == owner.Saved.OwnerPartyId && editor.PartyNames.Count == 2 && !editor.PartyNames.ContainsKey(previousOwner));
            Assert.Equal(2, editor.PartyNames.Count);
            Assert.False(editor.PartyNames.ContainsKey(previousOwner));
        }
    }

    private static (OpportunityEditingSession Editor, StubOwner Owner, EditingChangeFeed Feed, CrmHrChanges Changes) Create(
        Func<Guid, CancellationToken, Task<ProjectWriteAdmission?>>? observeProject = null) {
        var owner = new StubOwner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var editor = new OpportunityEditingSession(owner, observeProject ?? ((_, _) => throw new InvalidOperationException("No project read expected.")), changes, feed,
            new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance, NullLogger<OpportunityEditingSession>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        return (editor, owner, feed, changes);
    }

    private static CrmOpportunityDetailModel Detail(CrmOpportunityEditorModel model) => new(model.Id!.Value, model.AccountPartyId, "Account", model.Title,
        model.Stage, model.RelationshipStage, model.OpportunitySource, model.OwnerPartyId, "Owner", model.DeliveryUnitPartyId, "", model.CurrencyCode, model.Amount,
        model.ProbabilityPercent, model.ExpectedCloseOn, model.LostReason, model.CompetitorName, model.PartnerContributionSummary, model.Summary, model.Notes,
        model.LinkedProjectId, "", model.Parties.Select(row => new CrmOpportunityPartyLinkItemModel(row.Id!.Value, row.PartyId, "Linked party", PartyType.Person, row.Role)).ToArray(),
        [], model.ExpectedUpdatedAtUtc!.Value) { LinkedProjectAdmission = model.LinkedProjectAdmission };
    private sealed class InjectedFailure : Exception;
    private sealed class StubOwner : ICrmOpportunityEditorService {
        public CrmOpportunityEditorModel Saved { get; set; } = OpportunityReconciliationTests.Model();
        public CrmOpportunityEditorModel? Submitted { get; private set; }
        public int Writes { get; private set; }
        public int? Maximum { get; private set; }
        public Func<int?, Task<CrmOpportunityDetailModel?>>? Read { get; set; }
        public Func<CrmOpportunityEditorModel, Action<CrmOpportunitySaveReceipt>?, Task<Result<CrmOpportunitySaveReceipt>>>? Write { get; set; }
        public Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid id, CancellationToken cancellationToken = default, int? maximumPartyLinks = null) {
            Maximum = maximumPartyLinks;
            return Read?.Invoke(maximumPartyLinks) ?? Task.FromResult<CrmOpportunityDetailModel?>(Detail(Saved));
        }
        public Task<Result<CrmOpportunitySaveReceipt>> SaveOpportunityWithReceiptAsync(CrmOpportunityEditorModel model, CancellationToken cancellationToken = default,
            Action<CrmOpportunitySaveReceipt>? onCommitted = null) {
            Writes++;
            Submitted = model.Snapshot();
            return (Write ?? throw new InvalidOperationException("Configure the write."))(model, onCommitted);
        }
    }
}
