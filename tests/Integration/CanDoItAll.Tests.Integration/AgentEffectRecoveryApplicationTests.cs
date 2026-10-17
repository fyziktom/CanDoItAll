using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentEffectRecoveryApplicationTests {
    [Fact]
    public async Task Recovery_precedes_attachment_provider_and_checkpoint_access() {
        await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(run => run with {
            MetadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(run.MetadataJson, new string('a', 64))
        });
        var operation = AgentExecutionOperationId.New();
        var result = await fixture.RecoverAsync(operation);
        Assert.Equal(ExecutionState.WaitingOnTool, result.State);
        Assert.Null(result.Metric);
        Assert.Null(result.AssistantMessage);
        Assert.Null(result.StructuredOutput);
        Assert.Null(result.ContextCompletionNotification);
        Assert.Equal(string.Empty, result.ResponseText);
        Assert.Equal(1, result.EffectContinuation!.Counts.KnownCommitted);
        Assert.Equal(0, fixture.Runtime.Calls);
        var saved = (await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id))!;
        Assert.Empty(saved.Metrics);
        Assert.Empty(saved.UsageObservations);
        Assert.Empty(saved.ChatSession!.Messages);
        Assert.Null(saved.Run.SerializedSessionStateJson);
        Assert.Null(saved.Run.ToolAdmission!.Batches[0].Proposals[0].Result);
        await using var activity = fixture.Coordinator.OpenReader(fixture.Identity.CreateStreamId(operation), StreamSequence.Beginning);
        var observed = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await activity.ReadAsync());
        Assert.Equal(AgentExecutionActivityPhase.AwaitingContinuation, observed.Items[^1].Event.Phase);
        Assert.Equal(AgentExecutionActivityTerminalOutcome.Suspended, observed.Items[^1].Event.TerminalOutcome);
    }

    [Fact]
    public async Task Interactive_write_revocation_allows_current_read_without_replacing_original_authority() {
        await using var fixture = await AgentEffectRecoveryFixture.CreateAsync();
        fixture.Resolver.WriteAllowed = false;
        var result = await fixture.RecoverAsync();
        Assert.Equal(1, result.EffectContinuation!.Counts.KnownCommitted);
        Assert.True(fixture.Resolver.Calls > 0);
        Assert.All(fixture.Resolver.ObservedIds, id => Assert.NotEqual(fixture.Original.ToolAdmission!.Session.Reference.AuthorityId, id));
        var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!;
        Assert.Equal(fixture.Original.ToolAdmission!.Session, saved.ToolAdmission!.Session);
        Assert.Equal(fixture.Original.MetadataJson, saved.MetadataJson);
        Assert.Equal(0, fixture.Runtime.Calls);
    }

    [Fact]
    public async Task Current_read_revocation_before_or_during_lookup_blocks_disclosure() {
        foreach (var duringLookup in new[] { false, true }) {
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync();
            fixture.Resolver.ReadAllowed = duringLookup;
            fixture.Receipts.Observe = (_, _) => {
                fixture.Resolver.ReadAllowed = false;
                return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "private-source"), AgentToolAdmissionJournalFixture.Envelope("{\"secret\":true}")));
            };
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RecoverAsync());
            Assert.Equal(duringLookup ? 1 : 0, fixture.Receipts.Calls);
            var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.ToolAdmission!;
            Assert.Null(saved.ReceiptReconciliationCursor);
            Assert.Equal(fixture.Original.ToolAdmission!.Batches[0].Proposals[0], saved.Batches[0].Proposals[0]);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
    }

    [Fact]
    public async Task Changed_agent_profile_or_generation_cannot_rebind_original_intent() {
        foreach (var changedField in Enumerable.Range(0, 3)) {
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync();
            fixture.Resolver.Change = current => new(current.AuthorityId, changedField == 0 ? Guid.NewGuid() : current.AgentId,
                changedField == 1 ? Guid.NewGuid() : current.DatabaseProfileId,
                changedField == 2 ? new(current.DatabaseProfileGeneration.Value + 1) : current.DatabaseProfileGeneration,
                current.WorkspaceScope, true, false, current.PolicyVersion, current.PolicyFingerprint, current.ResolvedAtUtc);
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RecoverAsync());
            var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!;
            Assert.Equal(fixture.Original.MetadataJson, saved.MetadataJson);
            Assert.Equal(fixture.Original.ToolAdmission!.Session, saved.ToolAdmission!.Session);
            Assert.Equal(0, fixture.Receipts.Calls);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
    }

    [Fact]
    public async Task Confirmed_summary_stays_known_committed_without_querying_expired_detail() {
        await using var fixture = await AgentEffectRecoveryFixture.CreateAsync();
        var accepted = await fixture.RecoverAsync();
        fixture.Receipts.Observe = (_, _) => throw new IOException("The receipt horizon is no longer available.");
        var before = JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id));
        var detail = await fixture.Service.GetExecutionRunDetailAsync(fixture.Original.Id);
        Assert.Equal(1, detail.EffectContinuation!.Counts.KnownCommitted);
        Assert.Equal(AgentEffectReconciliationStop.SnapshotRead, detail.EffectContinuation.Stop);
        Assert.Equal(0, detail.EffectContinuation.ExaminedThisSlice);
        Assert.Empty(detail.EffectContinuation.Observations);
        Assert.Equal(1, fixture.Receipts.Calls);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id)));
        Assert.DoesNotContain("private-owner-result", JsonSerializer.Serialize(detail.EffectContinuation), StringComparison.Ordinal);
        fixture.Resolver.ReadAllowed = false;
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Service.GetExecutionRunDetailAsync(fixture.Original.Id));
        Assert.Equal(1, accepted.EffectContinuation!.Counts.KnownCommitted);
        Assert.Equal(0, fixture.Runtime.Calls);
    }

    [Fact]
    public async Task Checkpoint_failure_preserves_exact_intent_and_reports_no_durable_waiting_promotion() {
        foreach (var loseAcknowledgement in new[] { false, true }) {
            var armed = false;
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(fault: stage => {
                if (armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                    armed = false;
                    throw new IOException("Lost acknowledgement after the real checkpoint write.");
                }
            });
            fixture.Receipts.Observe = async (_, token) => {
                if (loseAcknowledgement) {
                    armed = true;
                } else {
                    await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.Fixture.NewStore()).UpdateExecutionRunDetailAsync(fixture.Original.Id,
                        detail => detail with { Run = detail.Run with {
                            State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled,
                            Revision = checked(detail.Run.Revision + 1), CompletedAtUtc = DateTimeOffset.UtcNow
                        } }, token);
                }
                return new(new("owner", "source"), AgentToolAdmissionJournalFixture.Envelope());
            };
            var operation = AgentExecutionOperationId.New();
            var failure = await Assert.ThrowsAsync<AgentEffectReconciliationIncompleteException>(() => fixture.RecoverAsync(operation));
            Assert.Equal(loseAcknowledgement ? AgentEffectCheckpointPersistence.Unconfirmed : AgentEffectCheckpointPersistence.NotAttempted,
                failure.Continuation.Persistence);
            Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(failure.Continuation.Observations).Knowledge);
            Assert.False(failure.Continuation.Observations[0].Persisted);
            var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!;
            Assert.Equal(fixture.Original.ToolAdmission!.Batches[0].Proposals[0].IntentId, saved.ToolAdmission!.Batches[0].Proposals[0].IntentId);
            Assert.Equal(loseAcknowledgement ? ExecutionState.WaitingOnTool : ExecutionState.Failed, saved.State);
            await using var activity = fixture.Coordinator.OpenReader(fixture.Identity.CreateStreamId(operation), StreamSequence.Beginning);
            var observed = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await activity.ReadAsync());
            Assert.Equal(AgentExecutionActivityPhase.Failed, observed.Items[^1].Event.Phase);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
        foreach (var denyRead in new[] { false, true }) {
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(failLeaseRelease: true);
            fixture.Resolver.ReadAllowed = !denyRead;
            if (denyRead) {
                var failure = await Assert.ThrowsAsync<AgentEffectReconciliationLeaseException>(() => fixture.RecoverAsync());
                Assert.Equal(AgentEffectReconciliationFailureKind.ReadDenied, failure.FailureKind);
                var aggregate = Assert.IsType<AggregateException>(failure.InnerException);
                Assert.IsType<AgentToolReceiptAccessDeniedException>(aggregate.InnerExceptions[0]);
                Assert.IsType<TimeoutException>(aggregate.InnerExceptions[1]);
                Assert.Equal(AgentToolProposalState.Executing,
                    (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.ToolAdmission!.Batches[0].Proposals[0].State);
            } else {
                var failure = await Assert.ThrowsAsync<AgentEffectReconciliationCleanupException>(() => fixture.RecoverAsync());
                Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, failure.Continuation.Persistence);
                Assert.Equal(1, failure.Continuation.Counts.KnownCommitted);
                Assert.True(Assert.Single(failure.Continuation.Observations).Persisted);
                Assert.IsType<TimeoutException>(failure.InnerException);
                Assert.Equal(ExecutionState.WaitingOnTool, (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.State);
            }
            Assert.Equal(1, fixture.LeaseReleaseFaults);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var reacquired = await fixture.Store.AcquireToolDispatchLeaseAsync(fixture.Original.Id, timeout.Token);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
        var checkpointArmed = false;
        await using var combined = await AgentEffectRecoveryFixture.CreateAsync(fault: stage => {
            if (checkpointArmed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                checkpointArmed = false;
                throw new IOException("Injected real checkpoint acknowledgement loss.");
            }
        }, failLeaseRelease: true);
        combined.Receipts.Observe = (_, _) => {
            checkpointArmed = true;
            return ValueTask.FromResult(new AgentToolReceiptObservation(new("owner", "source"), AgentToolAdmissionJournalFixture.Envelope()));
        };
        var both = await Assert.ThrowsAsync<AgentEffectReconciliationIncompleteException>(() => combined.RecoverAsync());
        Assert.True(both.LeaseReleaseUnconfirmed);
        Assert.Equal(AgentEffectCheckpointPersistence.Unconfirmed, both.Continuation.Persistence);
        Assert.Equal(combined.Original.Id, both.Continuation.ExecutionRunId);
        Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(both.Continuation.Observations).Knowledge);
        var diagnostics = Assert.IsType<AggregateException>(both.InnerException);
        Assert.IsType<AgentEffectReconciliationIncompleteException>(diagnostics.InnerExceptions[0]);
        Assert.IsType<TimeoutException>(diagnostics.InnerExceptions[1]);
        Assert.Equal(1, combined.LeaseReleaseFaults);
        Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed,
            (await combined.Store.GetExecutionRunAsync(combined.Original.Id))!.ToolAdmission!.Batches[0].Proposals[0].State);
        Assert.Equal(0, combined.Runtime.Calls);

        foreach (var unavailable in new[] { true, false }) {
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(failLeaseRelease: true);
            if (unavailable) {
                fixture.Resolver.Change = _ => throw new IOException("Private read policy storage fault.");
            } else {
                fixture.Receipts.Observe = (_, _) => throw new OperationCanceledException("Private receipt cancellation.");
            }
            var operation = AgentExecutionOperationId.New();
            var failure = await Assert.ThrowsAsync<AgentEffectReconciliationLeaseException>(() => fixture.RecoverAsync(operation));
            Assert.Equal(unavailable ? AgentEffectReconciliationFailureKind.ReadUnavailable : AgentEffectReconciliationFailureKind.Cancelled,
                failure.FailureKind);
            var causes = Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions;
            Assert.IsType<TimeoutException>(causes[1]);
            Assert.Equal(unavailable ? typeof(AgentToolReceiptReadUnavailableException) : typeof(OperationCanceledException), causes[0].GetType());
            Assert.Equal(AgentToolProposalState.Executing,
                (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!.ToolAdmission!.Batches[0].Proposals[0].State);
            await using var activity = fixture.Coordinator.OpenReader(fixture.Identity.CreateStreamId(operation), StreamSequence.Beginning);
            var observed = Assert.IsType<SequencedStreamEvents<AgentExecutionActivity>>(await activity.ReadAsync());
            Assert.Equal(unavailable ? AgentExecutionActivityPhase.Failed : AgentExecutionActivityPhase.Cancelled, observed.Items[^1].Event.Phase);
            Assert.Equal(1, fixture.LeaseReleaseFaults);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var reacquired = await fixture.Store.AcquireToolDispatchLeaseAsync(fixture.Original.Id, timeout.Token);
            Assert.Equal(0, fixture.Runtime.Calls);
        }

        foreach (var loseAdmissionAcknowledgement in new[] { false, true }) {
            var armed = true;
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(fault: stage => {
                if (loseAdmissionAcknowledgement && armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                    armed = false;
                    throw new IOException("Private lease admission acknowledgement loss.");
                }
            }, failLeaseRelease: true);
            var reference = fixture.Original.ToolAdmission!.Session.Reference;
            if (!loseAdmissionAcknowledgement) {
                reference = new(reference.ExecutionRunId, reference.ChatSessionId, AgentExecutionAuthorityId.Create());
            }
            var failure = await Assert.ThrowsAsync<AgentEffectReconciliationLeaseException>(async () =>
                await fixture.Journal.AcquireInterruptedReconciliationAsync(reference, default));
            Assert.Equal(loseAdmissionAcknowledgement ? AgentEffectReconciliationFailureKind.Failed : AgentEffectReconciliationFailureKind.AdmissionRejected,
                failure.FailureKind);
            var causes = Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions;
            Assert.Equal(loseAdmissionAcknowledgement ? typeof(IOException) : typeof(AgentToolAdmissionException), causes[0].GetType());
            Assert.IsType<TimeoutException>(causes[1]);
            Assert.Equal(0, fixture.Resolver.Calls);
            Assert.Equal(0, fixture.Receipts.Calls);
            Assert.Equal(1, fixture.LeaseReleaseFaults);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var reacquired = await fixture.Store.AcquireToolDispatchLeaseAsync(fixture.Original.Id, timeout.Token);
            var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!;
            Assert.Equal(AgentToolProposalState.Executing, saved.ToolAdmission!.Batches[0].Proposals[0].State);
            Assert.Null(saved.ToolAdmission.Batches[0].Proposals[0].OwnerReceipt);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
    }

    [Fact]
    public async Task Prepared_receipt_noop_and_unsupported_approval_dispatch_preserve_decisions() {
        await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(run => run with {
            ToolAdmission = run.ToolAdmission! with { Batches = run.ToolAdmission.Batches.Select(batch => batch with {
                Proposals = batch.Proposals.Select(proposal => proposal with { State = AgentToolProposalState.Prepared, DispatchClaimId = null }).ToImmutableArray()
            }).ToImmutableArray() }
        });
        var result = await fixture.RecoverAsync();
        Assert.True(result.EffectContinuation!.HasPreparedEffects);
        Assert.Equal(AgentToolContinuationRequirement.None, result.EffectContinuation.Requirement);
        Assert.Equal(0, fixture.Receipts.Calls);
        var before = JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Service.ContinueExecutionRunAsync(
            fixture.Original.Id, AgentExecutionOperationId.New(), [], autoApprovePendingToolCalls: true));
        Assert.Equal("tool-admission.effect-dispatch-unavailable", failure.Code);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id)));
        Assert.Equal(0, fixture.Runtime.Calls);
    }

    [Fact]
    public async Task Cancelled_runs_remain_on_separate_no_dispatch_reconciliation() {
        await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(run => run with {
            State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = DateTimeOffset.UtcNow
        });
        var before = JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.RecoverAsync());
        Assert.Equal("tool-admission.cancelled-reconciliation-required", failure.Code);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Store.GetExecutionRunDetailAsync(fixture.Original.Id)));
        Assert.Equal(0, fixture.Receipts.Calls);
        Assert.Equal(0, fixture.Runtime.Calls);
    }

    [Fact]
    public async Task Startup_preserves_valid_effect_journal_including_zero_segments_without_sdk_work() {
        foreach (var beforeOrigin in new[] { false, true }) {
            await using var fixture = await AgentEffectRecoveryFixture.CreateAsync(run => beforeOrigin ? run with {
                ToolAdmission = run.ToolAdmission! with { Segments = [], Batches = [], ActiveDispatchLeaseId = null }
            } : run);
            var service = new AgentFrameworkExecutionRecoveryService(fixture.Store, new AgentEffectRecoveryFixture.NoProcessLeases(), [],
                NullLogger<AgentFrameworkExecutionRecoveryService>.Instance);
            Assert.Equal(1, await service.RecoverInterruptedRunsAsync(DateTimeOffset.UtcNow.AddMinutes(1), default));
            var saved = (await fixture.Store.GetExecutionRunAsync(fixture.Original.Id))!;
            Assert.Equal(ExecutionState.Failed, saved.State);
            Assert.Equal(RunOutcome.Failed, saved.Outcome);
            Assert.Equal(JsonSerializer.Serialize(fixture.Original.ToolAdmission), JsonSerializer.Serialize(saved.ToolAdmission));
            Assert.Null(saved.SerializedSessionStateJson);
            Assert.Equal(0, fixture.Receipts.Calls);
            Assert.Equal(0, fixture.Runtime.Calls);
        }
    }
}
