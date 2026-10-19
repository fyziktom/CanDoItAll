using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Receipt_failure_rolls_back_deletion_retirement_completion_and_preserves_the_original_project() {
        var faults = new OwnerFaults { Failure = FailurePoint.ReceiptInsert };
        return WithFixtureAsync(async fixture => {
            var target = await fixture.SeedAsync("Atomic deletion receipt");
            var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.Delete(target));
            var before = await fixture.StateAsync();
            faults.Armed = true;
            var refused = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.Equal(ProjectLifecycleRefusal.TransactionConflict, refused.Reason);
            Assert.True(faults.Triggered);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
            Assert.IsType<ProjectLifecycleLookup.NotObserved>(await fixture.LookupAsync(submission));
        }, faults);
    }

    [Fact]
    public Task Deleted_target_replay_returns_the_exact_retained_receipt_and_changed_intent_cannot_reuse_its_address() =>
        WithFixtureAsync(async fixture => {
            var target = await fixture.SeedAsync("Retained deletion replay");
            var other = await fixture.SeedAsync("Other intent");
            var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.Delete(target));
            fixture.Faults.Armed = true;
            var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.False(accepted.Replayed);
            Assert.Null(accepted.CompletionIssue);
            var deletion = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(accepted.Result.Acknowledgement);
            Assert.Equal(target.Project, deletion.Completion.Original.Project);
            Assert.Equal(1, fixture.Participant.Prepared);
            Assert.Equal(1, fixture.Participant.Completed);
            var beforeReplay = await fixture.StateAsync();
            Assert.DoesNotContain(target.Project.ProjectId, beforeReplay.Projects);
            Assert.Contains(target.Project.ProjectId, beforeReplay.Retired);
            Assert.Single(beforeReplay.Receipts);
            Assert.Equal(1, beforeReplay.Completions);
            var replay = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.True(replay.Replayed);
            Assert.Equal(ProjectLifecycleOperationCodec.EncodeAccepted(accepted.Result), ProjectLifecycleOperationCodec.EncodeAccepted(replay.Result));
            Assert.Equal(1, fixture.Participant.Completed);
            var changed = new ProjectLifecycleSubmission(submission.Operation, submission.Interpretation, new ProjectLifecycleCommand.Delete(other));
            var refusal = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(changed, fixture.Invocation));
            Assert.Equal(ProjectLifecycleRefusal.IntentMismatch, refusal.Reason);
            beforeReplay.RequireEqual(await fixture.StateAsync());
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Original_deletion_revision_and_lifetime_are_not_rebased_at_admission(bool staleLifetime) =>
        WithFixtureAsync(async fixture => {
            var target = await fixture.SeedAsync("Original target expectation");
            var expected = staleLifetime
                ? new ProjectDefinitionExpectation(new(target.Project.DatabaseProfileId, target.Project.ProjectId, Guid.NewGuid()), target.Revision)
                : new ProjectDefinitionExpectation(target.Project, Guid.NewGuid());
            var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.Delete(expected));
            var before = await fixture.StateAsync();
            Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.Equal(0, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
            Assert.IsType<ProjectLifecycleLookup.NotObserved>(await fixture.LookupAsync(submission));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Current_write_revocation_at_the_final_commit_boundary_rolls_back_the_staged_effect_and_receipt(bool deletion) =>
        WithFixtureAsync(async fixture => {
            var first = await fixture.SeedAsync("Final authority first");
            var second = await fixture.SeedAsync("Final authority second");
            var command = deletion ? (ProjectLifecycleCommand)new ProjectLifecycleCommand.Delete(first)
                : new ProjectLifecycleCommand.EnsureLink(first.Project, second.Project);
            var submission = await fixture.SubmitIntentAsync(command);
            var before = await fixture.StateAsync();
            fixture.Write.DenyAt = 2;
            fixture.Faults.Armed = true;
            var refusal = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.Equal(ProjectLifecycleRefusal.WriteDenied, refusal.Reason);
            Assert.Equal(2, fixture.Write.Observations);
            Assert.True(fixture.Faults.ReceiptReached.Task.IsCompleted);
            Assert.False(fixture.Faults.Committed);
            Assert.Equal(deletion ? 1 : 0, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
            before.RequireEqual(await fixture.StateAsync());
        });

    [Fact]
    public Task Acknowledged_deletion_keeps_acceptance_when_owner_disposal_fails_and_cannot_dispatch_a_participant() {
        var faults = new OwnerFaults { Failure = FailurePoint.OwnerDisposal };
        return WithFixtureAsync(async fixture => {
            var target = await fixture.SeedAsync("Known commit cleanup failure");
            var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.Delete(target));
            faults.Armed = true;
            var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, accepted.CompletionIssue);
            Assert.True(faults.Committed);
            Assert.True(faults.Triggered);
            Assert.Equal(0, fixture.Participant.Completed);
            var binding = Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(accepted.Result.Acknowledgement).Completion;
            Assert.IsType<ProjectDeletionCompletionObservation.AwaitingOwnerRelease>(await fixture.Services.GetRequiredService<IProjectDeletionCompletionQuery>()
                .ObserveAsync(binding.Original, Assert.Single(binding.Obligations)));
            var found = Assert.IsType<ProjectLifecycleLookup.Found>(await fixture.LookupAsync(submission));
            Assert.Equal(ProjectLifecycleOperationCodec.EncodeAccepted(accepted.Result), ProjectLifecycleOperationCodec.EncodeAccepted(found.Accepted));
            var state = await fixture.StateAsync();
            Assert.Empty(state.Projects);
            Assert.Single(state.Retired);
            Assert.Single(state.Receipts);
            Assert.Equal(1, state.Completions);
        }, faults);
    }

    [Fact]
    public Task Hierarchy_cycle_and_exact_edge_expectations_refuse_stale_intent_without_adopting_a_new_edge() =>
        WithFixtureAsync(async fixture => {
            var first = await fixture.SeedAsync("Hierarchy first");
            var second = await fixture.SeedAsync("Hierarchy second");
            var third = await fixture.SeedAsync("Hierarchy third");
            var initial = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(
                await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.EnsureLink(first.Project, second.Project)), fixture.Invocation));
            var edge = Assert.IsType<ProjectLifecycleAcknowledgement.LinkEnsured>(initial.Result.Acknowledgement);
            Assert.True(edge.Created);
            var beforeCycle = await fixture.StateAsync();
            var cycle = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(
                await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.EnsureLink(second.Project, first.Project)), fixture.Invocation));
            Assert.Equal(ProjectLifecycleRefusal.Cycle, cycle.Reason);
            beforeCycle.RequireEqual(await fixture.StateAsync());
            var expected = new ProjectHierarchyEdgeExpectation(edge.EdgeId, first.Project, second.Project);
            var moved = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(
                await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.ReconnectLink(expected, third.Project)), fixture.Invocation));
            var destination = Assert.IsType<ProjectLifecycleAcknowledgement.LinkReconnected>(moved.Result.Acknowledgement);
            var beforeStale = await fixture.StateAsync();
            var actualEdge = Assert.Single(beforeStale.Edges);
            Assert.Equal(destination.DestinationEdgeId, actualEdge.Id);
            Assert.Equal(third.Project.ProjectId, actualEdge.Parent);
            Assert.Equal(second.Project.ProjectId, actualEdge.Child);
            var stale = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(
                await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.RemoveLink(expected)), fixture.Invocation));
            Assert.Equal(ProjectLifecycleRefusal.EdgeChanged, stale.Reason);
            beforeStale.RequireEqual(await fixture.StateAsync());
            Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(
                await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.RemoveLink(new(destination.DestinationEdgeId, third.Project, second.Project))), fixture.Invocation));
            Assert.Empty((await fixture.StateAsync()).Edges);
        });

    [Fact]
    public Task Actual_left_join_observes_an_orphan_future_receipt_instead_of_reporting_genuine_future_absence() =>
        WithFixtureAsync(async fixture => {
            var first = await fixture.SeedAsync("Orphan receipt first");
            var second = await fixture.SeedAsync("Orphan receipt second");
            var submission = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.EnsureLink(first.Project, second.Project));
            Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(submission, fixture.Invocation));
            var future = new ProjectLifecycleOperation(submission.Operation.DatabaseProfileId, submission.Operation.Namespace,
                new(submission.Operation.Epoch.Value + 1), submission.Operation.CallerBinding, Guid.NewGuid());
            Assert.IsType<ProjectLifecycleLookup.UnknownFutureEpoch>(await fixture.Reader.LookupAsync(future,
                new(fixture.Invocation.Caller, fixture.Invocation.ReceiptAccess)));
            await fixture.MutateAsync(async context => {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Projects_LifecycleReceipts" ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId",
                        "DatabaseProfileId", "SchemaVersion", "NormalizationVersion", "ResultVersion", "CommandKind",
                        "SemanticDigest", "ProjectId", "LifetimeId", "AcceptedAtUtc", "AcceptedPayload")
                    SELECT "NamespaceId", {future.Epoch.Value}, "CallerKind", "CallerKey", {future.Id}, "DatabaseProfileId",
                        "SchemaVersion", "NormalizationVersion", "ResultVersion", "CommandKind", "SemanticDigest", "ProjectId",
                        "LifetimeId", "AcceptedAtUtc", "AcceptedPayload"
                    FROM "Projects_LifecycleReceipts" WHERE "NamespaceId" = {submission.Operation.Namespace.Value}
                        AND "Epoch" = {submission.Operation.Epoch.Value} AND "OperationId" = {submission.Operation.Id}
                    """);
            });
            var orphan = Assert.IsType<ProjectLifecycleLookup.Unavailable>(await fixture.Reader.LookupAsync(future,
                new(fixture.Invocation.Caller, fixture.Invocation.ReceiptAccess)));
            Assert.Equal(ProjectLifecycleUnavailableReason.InconsistentMetadata, orphan.Reason);
            var beforeRetry = await fixture.StateAsync();
            var futureSubmission = new ProjectLifecycleSubmission(future, submission.Interpretation, submission.Command);
            Assert.IsNotType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(futureSubmission, fixture.Invocation));
            beforeRetry.RequireEqual(await fixture.StateAsync());
            await fixture.MutateAsync(async context => {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM "Projects_LifecycleReceipts" WHERE "NamespaceId" = {future.Namespace.Value}
                        AND "Epoch" = {future.Epoch.Value} AND "OperationId" = {future.Id}
                    """);
            });
            Assert.IsType<ProjectLifecycleLookup.UnknownFutureEpoch>(await fixture.Reader.LookupAsync(future,
                new(fixture.Invocation.Caller, fixture.Invocation.ReceiptAccess)));
        });
}
