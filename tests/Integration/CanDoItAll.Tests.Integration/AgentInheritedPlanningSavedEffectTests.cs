using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentInheritedPlanningSavedEffectTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Parent_continuation_requires_real_receipt_claim_and_original_held_snapshot_without_charging_human_wait(
        bool review, bool projectContext) {
        var contextScope = projectContext ? WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) : null;
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync(contextScope);
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        fixture.Policy.DispatchAllowed = !review;
        await using var lease = await ResumeAsync(fixture, waiting, access, review: review);
        using var bound = lease.Bind();
        var work = lease.PlanningWork!;
        var binding = Binding(waiting);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Journal.ClaimSavedInvocationAsync(
            lease, binding, Payload(waiting), default));
        await using (var held = await SnapshotAsync(fixture)) {
            Assert.Throws<AgentPlanningAdmissionException>(() => work.RequirePublication(held, binding));
        }
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        var publication = await SnapshotAsync(fixture);
        await using (publication) {
            Assert.Null(publication.OriginalChatSession);
            Assert.Equal(AgentPlanningPublicationOrigin.Inherited, publication.Original.Origin);
            Assert.Equal(fixture.Files.StorageScope, publication.Original.Scope);
            Assert.Equal(contextScope ?? fixture.Files.StorageScope, AgentPlanningRunMetadata.ReadScope(publication.OriginalRun));
            Assert.Equal(TimeSpan.FromSeconds(29), work.RequirePublication(publication, binding));
            Assert.Equal(review ? AgentInheritedContinuationPurpose.ManagerReview : AgentInheritedContinuationPurpose.Dispatch,
                work.RequireInheritedPublicationPurpose(publication, binding));
            Assert.Throws<AgentPlanningAdmissionException>(() => work.RequirePublication(publication,
                binding with { IntentId = new(Guid.NewGuid()) }));
            fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(TimeSpan.FromSeconds(26), work.RequirePublication(publication, binding));
        }
        Assert.Throws<ObjectDisposedException>(() => work.RequirePublication(publication, binding));
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope(),
            AgentToolEffectState.NotCommitted, default);
        await fixture.Journal.CheckpointSavedEffectAsync(lease, default);
        var settled = await fixture.Owner.SettleTerminalAsync(work, AgentPlanningGenerationState.Completed, default);
        Assert.Equal(TimeSpan.FromSeconds(4).Ticks, settled.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(settled.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Equal(Origin(waiting), Origin(settled));
        Assert.Equal(RunOutcome.Succeeded, settled.Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_context_cannot_change_after_the_saved_publication_claim(bool review) {
        var originalScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        await using var fixture = await AgentInheritedPlanningFixture.CreateForPublicationAsync(originalScope);
        var waiting = await WaitAsync(fixture);
        await using var lease = await ResumeAsync(fixture, waiting, new(fixture), review: review);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        var detail = (await fixture.Files.Store.GetExecutionRunDetailAsync(waiting.Id))!;
        var changed = detail.Run with {
            MetadataJson = AgentPlanningRunMetadata.ApplyInherited("{}", AgentPlanningOwner.ProjectTaskPlan,
                AgentPlanningRunMetadata.Read(detail.Run), WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))),
            Revision = checked(detail.Run.Revision + 1)
        };
        await fixture.Files.Store.SaveExecutionRunDetailAsync(detail with { Run = changed });
        await using var held = await SnapshotAsync(fixture);
        Assert.NotEqual(originalScope, AgentPlanningRunMetadata.ReadScope(held.OriginalRun));
        var refusal = Assert.Throws<AgentPlanningAdmissionException>(() =>
            lease.PlanningWork!.RequirePublication(held, Binding(waiting)));
        Assert.Equal(AgentPlanningAdmissionRefusal.OriginalStepChanged, refusal.Reason);
        Assert.Equal(AgentToolProposalState.Executing,
            AgentToolJournalTransitions.RequireProposal(held.OriginalRun.ToolAdmission!, Binding(waiting)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generic_and_interactive_saved_entries_cannot_resume_an_inherited_proposal(bool interactive) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => interactive
            ? fixture.Owner.ResumeSavedEffectAsync(Request(waiting), AgentPlanningDecisionExpectation.From(waiting), AllowRead, default)
            : fixture.Journal.AcquireSavedEffectDispatchAsync(Request(waiting), AllowRead, default));
        await RequireUnchangedAsync(fixture, waiting);
    }

    [Theory]
    [InlineData(ChangedOrigin.Profile)]
    [InlineData(ChangedOrigin.Agent)]
    [InlineData(ChangedOrigin.Namespace)]
    [InlineData(ChangedOrigin.Source)]
    [InlineData(ChangedOrigin.ParentBinding)]
    [InlineData(ChangedOrigin.Basis)]
    [InlineData(ChangedOrigin.CaptureTime)]
    [InlineData(ChangedOrigin.Digest)]
    [InlineData(ChangedOrigin.Policy)]
    [InlineData(ChangedOrigin.Grant)]
    public async Task A_substituted_parent_origin_cannot_approve_or_open_work(ChangedOrigin changed) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var original = Origin(waiting);
        var alternate = AgentPlanningDecisionTestData.Envelope("{\"substituted\":true}");
        var replacement = changed switch {
            ChangedOrigin.Profile => original with { Profile = new(Guid.NewGuid(), "substituted-profile", new(1)) },
            ChangedOrigin.Agent => original with { AgentId = Guid.NewGuid() },
            ChangedOrigin.Namespace => original with { WorkspaceNamespaceId = Guid.NewGuid() },
            ChangedOrigin.Source => original with { Source = new(original.Source.SourceKind, original.Source.SourceId,
                alternate.Digest, original.Source.ExecutionFingerprint) },
            ChangedOrigin.ParentBinding => original with { ParentBinding = alternate },
            ChangedOrigin.Basis => original with { BasisId = Guid.NewGuid() },
            ChangedOrigin.CaptureTime => original with { CapturedAtUtc = original.CapturedAtUtc.AddSeconds(-1) },
            ChangedOrigin.Digest => original with { BasisDigest = alternate.Digest },
            ChangedOrigin.Policy => original with { ParentPolicy = new(original.ParentPolicy.ReadBasisMode,
                original.ParentPolicy.PublicationFreshness, original.ParentPolicy.OnBasisChange,
                new(original.ParentPolicy.Budget.MaximumReplans, original.ParentPolicy.Budget.MaximumWorkDuration,
                    original.ParentPolicy.Budget.MaximumBasisAge.Add(TimeSpan.FromSeconds(1)))) },
            ChangedOrigin.Grant => original with { Allocation = original.Allocation with {
                Grant = original.Allocation.Grant with { WorkTicks = original.Allocation.Grant.WorkTicks - 1 } } },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        var access = new ParentAccess(fixture);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.ResumeInheritedSavedEffectAsync(
            Request(waiting), AgentPlanningDecisionExpectation.From(waiting), new(replacement, access.RequireAsync), AllowRead, default));
        Assert.Equal(0, access.Checks);
        await RequireUnchangedAsync(fixture, waiting);
    }

    [Theory]
    [InlineData(Denial.Parent, false, false)]
    [InlineData(Denial.SourceRead, false, false)]
    [InlineData(Denial.SourceDispatch, false, false)]
    [InlineData(Denial.SourceFingerprint, false, false)]
    [InlineData(Denial.Caller, false, false)]
    [InlineData(Denial.Parent, true, false)]
    [InlineData(Denial.SourceRead, true, false)]
    [InlineData(Denial.SourceDispatch, true, false)]
    [InlineData(Denial.SourceFingerprint, true, false)]
    [InlineData(Denial.Caller, true, false)]
    [InlineData(Denial.Parent, false, true)]
    [InlineData(Denial.SourceRead, false, true)]
    [InlineData(Denial.SourceFingerprint, false, true)]
    [InlineData(Denial.Caller, false, true)]
    [InlineData(Denial.Parent, true, true)]
    [InlineData(Denial.SourceRead, true, true)]
    [InlineData(Denial.SourceFingerprint, true, true)]
    [InlineData(Denial.Caller, true, true)]
    public async Task Revocation_before_resume_or_before_claim_cannot_enter_an_effect(Denial denial, bool beforeClaim, bool review) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        if (!beforeClaim) {
            access.Deny(denial);
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => ResumeAsync(fixture, waiting, access, review: review));
            await RequireUnchangedAsync(fixture, waiting);
            return;
        }
        await using var lease = await ResumeAsync(fixture, waiting, access, review: review);
        using var bound = lease.Bind();
        await fixture.Journal.ObserveSavedEffectReceiptAsync(lease, new AbsentReceipt(), default);
        access.Deny(denial);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Journal.ClaimSavedInvocationAsync(
            lease, Binding(waiting), Payload(waiting), default));
        var proposal = AgentToolJournalTransitions.RequireProposal((await fixture.ReadAsync()).ToolAdmission!, Binding(waiting));
        Assert.Equal(AgentToolProposalState.Prepared, proposal.State);
        Assert.Null(proposal.DispatchClaimId);
        Assert.Null(proposal.Result);
    }

    [Theory]
    [InlineData(Denial.Parent, false)]
    [InlineData(Denial.SourceDispatch, false)]
    [InlineData(Denial.Caller, false)]
    [InlineData(Denial.Parent, true)]
    [InlineData(Denial.SourceRead, true)]
    [InlineData(Denial.Caller, true)]
    public async Task Authority_is_rechecked_after_waiting_for_the_original_dispatch_lease(Denial denial, bool review) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var holder = await fixture.Files.NewStore().AcquireToolDispatchLeaseAsync(waiting.Id, deadline.Token);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        access.Observed = count => {
            if (count == 2) {
                ready.TrySetResult();
            }
        };
        var pending = ResumeAsync(fixture, waiting, access, deadline.Token, review);
        Exception? check;
        try {
            check = await Record.ExceptionAsync(async () => {
                await ready.Task.WaitAsync(deadline.Token);
                Assert.False(pending.IsCompleted);
                access.Deny(denial);
            });
        } finally {
            await holder.DisposeAsync();
        }
        var refusal = await Record.ExceptionAsync(() => pending);
        Assert.Null(check);
        Assert.IsType<AgentToolReceiptAccessDeniedException>(refusal);
        await RequireUnchangedAsync(fixture, waiting);
    }

    [Theory]
    [InlineData(AgentToolEffectState.Committed, false)]
    [InlineData(AgentToolEffectState.Unknown, false)]
    [InlineData(AgentToolEffectState.Committed, true)]
    [InlineData(AgentToolEffectState.Unknown, true)]
    public async Task Actual_effect_outcome_is_retained_after_parent_and_source_revocation(AgentToolEffectState effect, bool review) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        await using var lease = await ResumeAsync(fixture, waiting, access, review: review);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        access.Deny(Denial.Parent);
        access.Deny(Denial.SourceDispatch);
        await fixture.Journal.CompleteInvocationAsync(claim, AgentPlanningDecisionTestData.Envelope("{\"retained\":true}"),
            effect, default, requiresOwnerReconciliation: effect == AgentToolEffectState.Unknown);
        var saved = await fixture.ReadAsync();
        var proposal = AgentToolJournalTransitions.RequireProposal(saved.ToolAdmission!, Binding(waiting));
        Assert.Equal(effect, proposal.EffectState);
        Assert.Equal(claim.Id, proposal.DispatchClaimId);
        Assert.NotNull(proposal.Result);
        Assert.Equal(Origin(waiting), Origin(saved));
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() =>
            fixture.Journal.ReadSavedEffectAsync(lease, Binding(waiting), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiration_is_checked_inside_held_publication_authority(bool review) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        await using var lease = await ResumeAsync(fixture, waiting, new(fixture), review: review);
        using var bound = lease.Bind();
        var claim = await ClaimAsync(fixture, lease, waiting);
        using var claimed = claim.Bind();
        await using var held = await SnapshotAsync(fixture);
        Assert.Equal(TimeSpan.FromSeconds(29), lease.PlanningWork!.RequirePublication(held, Binding(waiting)));
        fixture.Clock.Advance(TimeSpan.FromSeconds(29));
        var refusal = Assert.Throws<AgentPlanningAdmissionException>(() => lease.PlanningWork.RequirePublication(held, Binding(waiting)));
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, refusal.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_resume_acknowledgement_cannot_reconstruct_open_work_after_restart(bool review) {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        await Assert.ThrowsAsync<IOException>(() => ResumeAsync(fixture, waiting, access, review: review));
        var retained = await fixture.ReadAsync();
        Assert.NotNull(retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Empty(retained.PendingApprovals);
        var owner = new AgentPlanningDecisionOwner(fixture.NewJournal());
        var refusal = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => owner.ResumeInheritedSavedEffectAsync(
            Request(retained), AgentPlanningDecisionExpectation.From(retained), Continuation(retained, access, review), access.ReadAsync, default));
        Assert.Equal(AgentPlanningAdmissionRefusal.WorkAlreadyOpen, refusal.Reason);
        Assert.Null(AgentToolJournalTransitions.RequireProposal(retained.ToolAdmission, Binding(retained)).DispatchClaimId);
    }

    [Fact]
    public async Task Competing_reviews_of_one_displayed_proposal_admit_only_one_live_interval() {
        await using var fixture = await CreateAsync();
        var waiting = await WaitAsync(fixture);
        var access = new ParentAccess(fixture);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var holder = await fixture.Files.NewStore().AcquireToolDispatchLeaseAsync(waiting.Id, deadline.Token);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        access.Observed = count => {
            if (count == 4) {
                ready.TrySetResult();
            }
        };
        var first = ResumeAsync(fixture, waiting, access, deadline.Token, review: true);
        var second = ResumeAsync(fixture, waiting, access, deadline.Token, review: true);
        Exception? check;
        try {
            check = await Record.ExceptionAsync(() => ready.Task.WaitAsync(deadline.Token));
        } finally {
            await holder.DisposeAsync();
        }
        var outcomes = await Task.WhenAll(ObserveAsync(first), ObserveAsync(second));
        Assert.Null(check);
        Assert.Single(outcomes, item => item is null);
        Assert.True(Assert.Single(outcomes, item => item is not null) is AgentToolAdmissionException or AgentPlanningAdmissionException);
        var current = await fixture.ReadAsync();
        Assert.NotNull(current.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(waiting.ToolAdmission!.ProviderDispatches.Single().Id,
            Assert.Single(current.ToolAdmission.ProviderDispatches).Id);
        var proposal = AgentToolJournalTransitions.RequireProposal(current.ToolAdmission, Binding(waiting));
        Assert.Equal(AgentToolProposalState.Prepared, proposal.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, proposal.ApprovalStatus);
        Assert.Null(proposal.DispatchClaimId);

        static async Task<Exception?> ObserveAsync(Task<AgentToolRunLease> pending) {
            try {
                await using var lease = await pending;
                return null;
            } catch (Exception failure) {
                return failure;
            }
        }
    }

    private static Task<AgentInheritedPlanningFixture> CreateAsync() => AgentInheritedPlanningFixture.CreateForPublicationAsync();

    private static Task<ExecutionRunRecord> WaitAsync(AgentInheritedPlanningFixture fixture) => fixture.WaitForApprovalAsync();

    private static AgentPlanningInheritedOrigin Origin(ExecutionRunRecord run) => run.ToolAdmission!.PlanningDecision!.InheritedOrigin!;
    private static AgentToolApprovalBinding Binding(ExecutionRunRecord run) => run.ToolAdmission!.PlanningDecision!.Current.Steps[^1].Publications.Single().Proposal;
    private static AgentToolPreparedPayload Payload(ExecutionRunRecord run) => AgentToolJournalTransitions.RequireProposal(run.ToolAdmission!, Binding(run)).Payload;
    private static AgentSavedEffectContinuationRequest Request(ExecutionRunRecord run)
        => new(run.Id, run.ChatSessionId, run.Revision, run.ToolAdmission!.Revision, Binding(run),
            run.PendingApprovals.Select(item => new PendingToolApprovalDecision(item.ApprovalId, true) { ToolAdmission = item.ToolAdmission }).ToArray());
    private static Task<AgentToolRunLease> ResumeAsync(AgentInheritedPlanningFixture fixture, ExecutionRunRecord run,
        ParentAccess access, CancellationToken token = default, bool review = false)
        => fixture.Owner.ResumeInheritedSavedEffectAsync(Request(run), AgentPlanningDecisionExpectation.From(run),
            Continuation(run, access, review), access.ReadAsync, token);
    private static AgentInheritedSavedEffectContinuation Continuation(ExecutionRunRecord run, ParentAccess access, bool review)
        => review ? AgentInheritedSavedEffectContinuation.ForManagerReview(Origin(run), AgentPlanningDecisionExpectation.From(run),
            Binding(run), access.RequireAsync) : new(Origin(run), access.RequireAsync);
    private static ValueTask<bool> AllowRead(AgentToolSessionObservation _, CancellationToken token) => ValueTask.FromResult(true);

    private static async Task<AgentToolInvocationClaim> ClaimAsync(AgentInheritedPlanningFixture fixture,
        AgentToolRunLease lease, ExecutionRunRecord run) {
        await fixture.Journal.ObserveSavedEffectReceiptAsync(lease, new AbsentReceipt(), default);
        return await fixture.Journal.ClaimSavedInvocationAsync(lease, Binding(run), Payload(run), default);
    }

    private static async Task<IAgentPlanningPublicationSnapshotLease> SnapshotAsync(AgentInheritedPlanningFixture fixture) {
        var run = await fixture.ReadAsync();
        return await fixture.Files.Store.AcquirePlanningAuthoritySnapshotAsync(AgentPlanningPublicationExpectation.Inherited(
            fixture.Files.StorageScope, run.ToolAdmission!.Session, run.Revision, run.ToolAdmission.Revision));
    }

    private static async Task RequireUnchangedAsync(AgentInheritedPlanningFixture fixture, ExecutionRunRecord original) {
        var current = await fixture.ReadAsync();
        Assert.Equal(AgentPlanningDecisionExpectation.From(original), AgentPlanningDecisionExpectation.From(current));
        Assert.Equal(Origin(original), Origin(current));
        Assert.Null(current.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(ExecutionApprovalStatus.Pending, AgentToolJournalTransitions.RequireProposal(current.ToolAdmission, Binding(current)).ApprovalStatus);
    }

    private sealed class ParentAccess(AgentInheritedPlanningFixture fixture) {
        private bool parentAllowed = true;
        private bool callerAllowed = true;
        private int checks;
        internal int Checks => Volatile.Read(ref checks);
        internal Action<int>? Observed { get; set; }

        internal Task RequireAsync(ExecutionRunRecord run, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            Assert.Equal(Origin(fixture.Original), Origin(run));
            var observed = Interlocked.Increment(ref checks);
            Observed?.Invoke(observed);
            if (!parentAllowed) {
                throw new AgentToolReceiptAccessDeniedException();
            }
            return Task.CompletedTask;
        }

        internal ValueTask<bool> ReadAsync(AgentToolSessionObservation _, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(callerAllowed);
        }

        internal void Deny(Denial denial) {
            switch (denial) {
                case Denial.Parent:
                    parentAllowed = false;
                    break;
                case Denial.Caller:
                    callerAllowed = false;
                    break;
                case Denial.SourceRead:
                    fixture.Policy.ReadAllowed = false;
                    break;
                case Denial.SourceDispatch:
                    fixture.Policy.DispatchAllowed = false;
                    break;
                case Denial.SourceFingerprint:
                    fixture.Policy.Fingerprint = AgentPlanningDecisionTestData.Envelope("{\"changedOwner\":1}").Digest;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(denial));
            }
        }
    }

    private sealed class AbsentReceipt : IAgentToolReceiptReconciliationProvider {
        public bool Supports(string toolName) => toolName == AgentPlanningLiveOwnerFixture.Publication().Payload.ToolName;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            await claim.RequireAsync(cancellationToken);
            return AgentToolReceiptObservation.NotObserved;
        }
    }

    public enum ChangedOrigin { Profile, Agent, Namespace, Source, ParentBinding, Basis, CaptureTime, Digest, Policy, Grant }
    public enum Denial { Parent, SourceRead, SourceDispatch, SourceFingerprint, Caller }
}
