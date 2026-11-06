using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.CrmHr;

public sealed class OpportunityConversionSessionTests {
    [Fact]
    public async Task A_failed_refresh_retains_the_previous_comparison_as_stale_instead_of_authorizing_it() {
        var (session, owner, _, _) = Create();
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        await session.ReviewAsync();
        var comparison = session.State.Review;
        Assert.True(session.State.CanApplyReview);
        owner.Read = (_, _) => throw new IOException("Current basis read failed");

        await session.ReviewAsync();

        Assert.Same(comparison, session.State.Review);
        Assert.True(session.State.ReviewStale);
        Assert.False(session.State.CanApplyReview);
        Assert.NotNull(session.Message);
        Assert.Equal(0, owner.Writes);
    }

    [Fact]
    public async Task An_incomplete_receipt_is_unknown_and_is_never_reported_as_a_known_commit() {
        var notices = new List<OpportunityConversionNotice>();
        var (session, owner, _, _) = Create(notice: notices.Add);
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        owner.Write = (_, _) => Task.FromResult(Result<CrmOpportunityConversionResult>.Success(new(owner.Saved.Id, Guid.NewGuid(), true)));

        await session.SaveAsync(session.State.Draft);

        Assert.Null(session.State.Accepted);
        Assert.True(session.State.IsUnknown);
        Assert.Equal(OpportunityConversionNoticeKind.Error, Assert.Single(notices).Kind);
        Assert.Contains("unknown", session.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Known_acceptance_precedes_a_failed_continuation_and_preserves_later_input_without_replay() {
        var (session, owner, _, _) = Create();
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        var draft = session.State.Draft;
        owner.Write = (submitted, committed) => {
            committed!(Receipt(owner.Saved));
            draft.ProjectName = "  Later target request  ";
            session.State.Input(session.State.Opening);
            throw new IOException("Failure after actual owner acknowledgement");
        };

        var attempt = await session.SaveAsync(draft);

        Assert.NotNull(attempt);
        Assert.NotNull(session.State.Accepted);
        Assert.False(session.State.IsUnknown);
        Assert.Same(draft, session.State.Draft);
        Assert.Equal("  Later target request  ", draft.ProjectName);
        Assert.True(session.State.HasLaterInput(attempt));
        Assert.Equal("Original project target", owner.Submitted!.ProjectName);
        Assert.Equal("explicit-editing-test-principal", owner.Submitted.LastChangedBy);
        Assert.Null(await session.SaveAsync(draft));
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public async Task Unknown_conversion_keeps_its_recovery_project_and_current_review_cannot_authorize_replay() {
        TaskCompletionSource? hintProcessed = null;
        var (session, owner, feed, changes) = Create(changed: () => hintProcessed?.TrySetResult());
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        var project = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        owner.Write = (_, _) => throw new CrmOpportunityConversionRequiresObservationException(project, new IOException("Unknown physical commit"));

        await session.SaveAsync(session.State.Draft);
        var unknownMessage = session.Message;
        hintProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        feed.Publish(new(changes.Scope, EditingOwner.CrmOpportunity, owner.Saved.Id), EditingChangeKind.EditableContentChanged);
        await hintProcessed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(unknownMessage, session.Message);
        await session.ReviewAsync();
        await session.ApplyReviewAsync();

        Assert.True(session.State.IsUnknown);
        Assert.Equal(project, session.RecoveryProject);
        Assert.NotNull(session.State.Review);
        Assert.False(session.CanSave);
        Assert.False(session.State.CanApplyReview);
        Assert.Null(await session.SaveAsync(session.State.Draft));
        Assert.Equal(1, owner.Writes);
    }

    [Fact]
    public async Task Peer_change_freezes_the_command_and_explicit_basis_acceptance_is_local() {
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (session, owner, feed, changes) = Create(notice: _ => notified.TrySetResult());
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        var draft = session.State.Draft;
        draft.ProjectDescription = "  My raw target description  ";
        session.State.Input(session.State.Opening);
        var original = draft.ExpectedUpdatedAtUtc;
        owner.Saved = owner.Saved with { Title = "Current peer source", UpdatedAtUtc = owner.Saved.UpdatedAtUtc.AddSeconds(1) };

        feed.Publish(new(changes.Scope, EditingOwner.CrmOpportunity, owner.Saved.Id), EditingChangeKind.EditableContentChanged);
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(session.CanSave);
        Assert.Equal(original, draft.ExpectedUpdatedAtUtc);
        await session.ReviewAsync();
        Assert.Equal("Current peer source", session.State.Review!.Current.Title);
        await session.ApplyReviewAsync();

        Assert.Same(draft, session.State.Draft);
        Assert.Equal("  My raw target description  ", draft.ProjectDescription);
        Assert.Equal(owner.Saved.UpdatedAtUtc, draft.ExpectedUpdatedAtUtc);
        Assert.Equal(0, owner.Writes);
        Assert.True(session.CanSave);
    }

    [Fact]
    public async Task A_canceled_review_cannot_reopen_a_new_same_target_conversion() {
        var (session, owner, _, _) = Create();
        await using var lifetime = session;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        var held = new TaskCompletionSource<CrmOpportunityDetailModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.Read = (_, _) => {
            reached.TrySetResult();
            return held.Task;
        };
        var pending = session.ReviewAsync();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(session.Reading);
        Assert.False(session.CanSave);
        owner.Read = null;
        await session.OpenAsync(owner.Saved, Draft(owner.Saved));
        var current = session.State.Draft;
        held.TrySetResult(owner.Saved with { Title = "Retired comparison" });
        await pending;

        Assert.Same(current, session.State.Draft);
        Assert.Null(session.State.Review);
        Assert.False(session.Reading);
        Assert.True(session.CanSave);
    }

    [Fact]
    public async Task One_hundred_command_open_close_cycles_release_the_exact_observation_and_draft() {
        var (session, owner, feed, _) = Create();
        await using var lifetime = session;
        for (var index = 0; index < 100; index++) {
            await session.OpenAsync(owner.Saved, Draft(owner.Saved));
            Assert.Equal(1, feed.ActiveSubscriptions);
            session.Clear();
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
            Assert.Null(session.State.Basis);
            Assert.Null(session.State.Review);
            Assert.Null(session.RecoveryProject);
        }
    }

    private static (OpportunityConversionSession Session, StubOwner Owner, EditingChangeFeed Feed, CrmHrChanges Changes) Create(
        Action<OpportunityConversionNotice>? notice = null, Action? changed = null) {
        var owner = new StubOwner();
        var feed = new EditingChangeFeed();
        var changes = new CrmHrChanges(feed, new(EditingScopeKind.Dataset, Guid.NewGuid()), new());
        var session = new OpportunityConversionSession(owner, changes, feed, new EditingUiTestAccess(),
            NullLogger<EditingObservation>.Instance, NullLogger<OpportunityConversionSession>.Instance) {
                Dispatch = work => work(), Changed = () => changed?.Invoke(), Retired = () => { }, Notice = value => notice?.Invoke(value)
            };
        return (session, owner, feed, changes);
    }

    private static CrmOpportunityConversionEditorModel Draft(CrmOpportunityDetailModel basis) => new() {
        OpportunityId = basis.Id, ExpectedUpdatedAtUtc = basis.UpdatedAtUtc, ProjectName = "Original project target"
    };

    private static CrmOpportunityConversionResult Receipt(CrmOpportunityDetailModel basis) {
        var project = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        return new(basis.Id, project.ProjectId, true) {
            ProjectAdmission = project, ProjectName = "Original project target", OpportunityUpdatedAtUtc = basis.UpdatedAtUtc.AddSeconds(1)
        };
    }

    private sealed class StubOwner : ICrmOpportunityConversionService {
        public CrmOpportunityDetailModel Saved { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), "Account", "Original source",
            OpportunityStage.Won, "ActiveCustomer", OpportunitySource.Direct, Guid.NewGuid(), "Owner", null, "", "USD", 10,
            100, null, "", "", "", "Summary", "", null, "", [], [], DateTimeOffset.UtcNow);
        public Func<int?, CancellationToken, Task<CrmOpportunityDetailModel?>>? Read { get; set; }
        public Func<CrmOpportunityConversionEditorModel, Action<CrmOpportunityConversionResult>?, Task<Result<CrmOpportunityConversionResult>>>? Write { get; set; }
        public int Writes { get; private set; }
        public CrmOpportunityConversionEditorModel? Submitted { get; private set; }
        public Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid opportunityId, CancellationToken cancellationToken = default, int? maximumPartyLinks = null) =>
            Read?.Invoke(maximumPartyLinks, cancellationToken) ?? Task.FromResult<CrmOpportunityDetailModel?>(Saved);
        public Task<Result<CrmOpportunityConversionProjectBasis>> ObserveOpportunityProjectBasisAsync(Guid opportunityId,
            DateTimeOffset expectedOpportunityUpdatedAtUtc, ProjectWriteAdmission project, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CrmOpportunityConversionProjectBasis>.Success(new(opportunityId, expectedOpportunityUpdatedAtUtc, project, [])));
        public Task<Result<CrmOpportunityConversionResult>> ConvertOpportunityToProjectAsync(CrmOpportunityConversionEditorModel model,
            CancellationToken cancellationToken = default, Action<CrmOpportunityConversionResult>? onCommitted = null) {
            Writes++;
            Submitted = model.Snapshot();
            return (Write ?? throw new InvalidOperationException("Configure the owner write."))(model, onCommitted);
        }
    }
}
