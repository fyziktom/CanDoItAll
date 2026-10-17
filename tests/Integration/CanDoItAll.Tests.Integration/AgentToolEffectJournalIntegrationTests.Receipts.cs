using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolEffectJournalIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owner_confirmation_preserves_original_result_disclosure_and_dispatch_claim(bool hasOriginalResult) {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        if (hasOriginalResult) {
            run = ChangeProposal(run, proposal => proposal with {
                State = AgentToolProposalState.ReconciliationRequired,
                Result = AgentToolAdmissionJournalFixture.Envelope("{\"sdk\":\"original\"}"),
                DisclosureEvidence = AgentToolAdmissionJournalFixture.Envelope("{\"disclosure\":\"original\"}")
            });
        }
        await using var fixture = await RestoreAsync(source, run);
        var reader = new ReceiptReader();
        var result = await ReconcileAsync(fixture, reader);
        Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
        Assert.Equal(1, result.Counts.KnownCommitted);
        Assert.Equal(0, result.Counts.EligibleOwnerEffects);
        Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(result.Observations).Knowledge);
        Assert.True(result.Observations[0].Persisted);
        Assert.Equal(1, reader.Calls);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!;
        var original = run.ToolAdmission!.Batches[0].Proposals[0];
        Assert.Equal(original with {
            State = AgentToolProposalState.OwnerReceiptConfirmed,
            EffectState = AgentToolEffectState.Committed,
            OwnerReceipt = new(ObservedReceipt.CommittedEffect!, ObservedReceipt.Receipt!)
        }, saved.ToolAdmission!.Batches[0].Proposals[0]);
        Assert.Equal(ExecutionState.WaitingOnTool, saved.State);
        Assert.Null(saved.Outcome);
        Assert.Equal(run.SerializedSessionStateJson, saved.SerializedSessionStateJson);
        Assert.Equal(run.ToolAdmission.Segments.ToArray(), saved.ToolAdmission.Segments.ToArray());
    }

    [Fact]
    public async Task Confirmed_receipt_is_terminal_and_summary_never_discloses_cached_detail() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        await using var fixture = await RestoreAsync(source, EffectsRun(source));
        await ReconcileAsync(fixture, new ReceiptReader());
        var before = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        var forbidden = new ReceiptReader((_, _) => throw new InvalidOperationException("Confirmed detail must not be reread by a summary."));
        var summary = await ReconcileAsync(fixture, forbidden);
        Assert.Equal(0, forbidden.Calls);
        Assert.Equal(1, summary.Counts.KnownCommitted);
        Assert.Empty(summary.Observations);
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("owner-private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("source-private", json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(AgentToolProposalRecord.Result), json, StringComparison.OrdinalIgnoreCase);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!.ToolAdmission!;
        Assert.Equal(before.Batches[0].Proposals[0], saved.Batches[0].Proposals[0]);
        var owner = fixture.NewJournal();
        await using var dispatch = await owner.AcquireRunAsync(fixture.Session, default);
        using var bound = dispatch.Bind();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.ClaimInvocationAsync(dispatch,
            saved.Batches[0].Id, saved.Batches[0].Proposals[0].CallId, saved.Batches[0].Proposals[0].Payload, default));
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(saved,
            ChangeProposal(fixture.Detail.Run with { ToolAdmission = saved }, proposal => proposal with {
                State = AgentToolProposalState.ReconciliationRequired, OwnerReceipt = null
            }).ToolAdmission));
    }

    [Fact]
    public async Task Receipt_miss_including_closed_absence_never_authorizes_retry_or_clears_uncertainty() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        await using var fixture = await RestoreAsync(source, run);
        var reader = new ReceiptReader((_, _) => new(AgentToolReceiptObservation.NotObserved));
        var result = await ReconcileAsync(fixture, reader);
        Assert.Equal(AgentToolContinuationRequirement.EffectReconciliationRequired, result.Requirement);
        Assert.Equal(AgentEffectReceiptKnowledge.ReceiptNotObserved, Assert.Single(result.Observations).Knowledge);
        Assert.False(result.Observations[0].Persisted);
        Assert.Equal(1, result.Counts.EligibleOwnerEffects);
        var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
        Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0], saved.Batches[0].Proposals[0]);
        Assert.NotNull(saved.ReceiptReconciliationCursor);
        Assert.Null(saved.Batches[0].Proposals[0].OwnerReceipt);
    }

    [Fact]
    public async Task Missing_duplicate_malformed_and_failing_receipt_owners_fail_without_invented_acceptance() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        IAgentToolReceiptReconciliationProvider[][] owners = [
            [],
            [new ReceiptReader(), new ReceiptReader()],
            [new ReceiptReader((_, _) => ValueTask.FromResult(new AgentToolReceiptObservation(null, ObservedReceipt.Receipt)))],
            [new ReceiptReader((_, _) => ValueTask.FromResult(new AgentToolReceiptObservation(new(" invalid ", "source"), ObservedReceipt.Receipt)))],
            [new ReceiptReader((_, _) => throw new IOException("Injected owner reader outage."))]
        ];
        foreach (var providers in owners) {
            var run = EffectsRun(source);
            await using var fixture = await RestoreAsync(source, run);
            var owner = fixture.NewJournal();
            await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
            using var held = lease.Bind();
            var result = await owner.ReconcileInterruptedAsync(lease, providers, (_, _) => new(true), default);
            Assert.Equal(providers.Length == 0 ? AgentEffectReconciliationStop.ExhaustedSnapshot : AgentEffectReconciliationStop.ReaderFailure, result.Stop);
            Assert.Equal(AgentToolContinuationRequirement.EffectReconciliationRequired, result.Requirement);
            Assert.Equal(AgentEffectReceiptKnowledge.ReaderUnavailable, Assert.Single(result.Observations).Knowledge);
            Assert.Equal(0, result.Counts.KnownCommitted);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0], saved.Batches[0].Proposals[0]);
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => owner.ReconcileInterruptedAsync(lease, providers, (_, _) => new(true), default));
        }
    }

    [Fact]
    public async Task Current_read_revocation_before_or_during_lookup_denies_historical_disclosure() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        foreach (var revokeDuring in new[] { false, true }) {
            var run = EffectsRun(source);
            await using var fixture = await RestoreAsync(source, run);
            var owner = fixture.NewJournal();
            await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
            using var held = lease.Bind();
            var readable = revokeDuring;
            var reader = new ReceiptReader((_, _) => {
                readable = false;
                return new(ObservedReceipt);
            });
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => owner.ReconcileInterruptedAsync(lease, [reader],
                (observation, _) => {
                    Assert.Equal(run.ToolAdmission!.Session, observation.Session);
                    Assert.Equal(AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson), observation.TurnContext);
                    Assert.Equal(AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson), observation.Governance);
                    return new(readable);
                }, default));
            Assert.Equal(revokeDuring ? 1 : 0, reader.Calls);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0], saved.Batches[0].Proposals[0]);
            Assert.Null(saved.ReceiptReconciliationCursor);
        }
        foreach (var unavailableAt in new[] { 1, 2, 3, 4 }) {
            foreach (var unrelatedCancellation in new[] { false, true }) {
                var run = EffectsRun(source, 2);
                await using var fixture = await RestoreAsync(source, run);
                var owner = fixture.NewJournal();
                await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
                using var held = lease.Bind();
                var readCalls = 0;
                var reader = new ReceiptReader();
                Exception outage = unrelatedCancellation
                    ? new OperationCanceledException("Unrelated read-policy cancellation.")
                    : new IOException("Injected current-read policy outage.");
                var failure = await Assert.ThrowsAsync<AgentToolReceiptReadUnavailableException>(() =>
                    owner.ReconcileInterruptedAsync(lease, [reader], (_, token) => {
                        Assert.False(token.IsCancellationRequested);
                        if (++readCalls == unavailableAt) {
                            throw outage;
                        }
                        return new(true);
                    }, default));
                Assert.Same(outage, failure.InnerException);
                Assert.Equal(unavailableAt, readCalls);
                Assert.Equal(unavailableAt == 1 ? 0 : 1, reader.Calls);
                var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
                Assert.Equal(JsonSerializer.Serialize(run.ToolAdmission!.Batches), JsonSerializer.Serialize(saved.Batches));
                Assert.Null(saved.ReceiptReconciliationCursor);
            }
        }
    }

    [Fact]
    public async Task Reconciliation_cannot_change_original_binding_after_owner_observation() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        await using var fixture = await RestoreAsync(source, run);
        var independent = fixture.NewStore();
        var reader = new ReceiptReader(async (claim, token) => {
            var original = await claim.RequireAsync(token);
            var changed = original.Binding with { IntentId = new(Guid.NewGuid()) };
            var forged = new AgentToolReceiptReconciliationClaim(claim.Lease.AdmissionVerifier as AgentToolAdmissionJournal
                ?? throw new InvalidOperationException(), claim.Lease, changed, currentRead: (_, _) => new(true));
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => forged.RequireAsync(token).AsTask());
            var altered = AgentToolAdmissionJournalFixture.Payload(value: "altered");
            await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ((ISandboxWorkspaceExecutionRunMutationStore)independent)
                .UpdateExecutionRunDetailAsync(run.Id, detail => detail with {
                    Run = ChangeProposal(detail.Run, proposal => proposal with { Payload = altered, ApprovedDigest = altered.Digest })
                }, token));
            return ObservedReceipt;
        });
        var result = await ReconcileAsync(fixture, reader);
        Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
        Assert.Equal(Binding(run.ToolAdmission!), Assert.Single(result.Observations).Binding);
        var saved = (await independent.GetExecutionRunAsync(run.Id))!.ToolAdmission!.Batches[0].Proposals[0];
        Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[0].Payload, saved.Payload);
        Assert.Equal(run.ToolAdmission.Batches[0].Proposals[0].IntentId, saved.IntentId);
    }

    [Fact]
    public async Task Journal_commit_ack_loss_or_capacity_failure_preserves_exact_intent_and_known_commitment() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source);
        await using (var fixture = await RestoreAsync(source, run)) {
            var armed = false;
            var injected = false;
            var faulty = fixture.NewStore(stage => {
                if (armed && stage == ExistingRunDetailCommitStage.RunPersisted) {
                    armed = false;
                    injected = true;
                    throw new IOException("Injected receipt-checkpoint acknowledgement loss.");
                }
            });
            var owner = fixture.NewJournal(faulty);
            await using (var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default)) {
                using var bound = lease.Bind();
                armed = true;
                var result = await owner.ReconcileInterruptedAsync(lease, [new ReceiptReader()], (_, _) => new(true), default);
                Assert.True(injected);
                Assert.Equal(AgentEffectCheckpointPersistence.Unconfirmed, result.Persistence);
                Assert.Equal(AgentEffectReconciliationStop.CheckpointFailure, result.Stop);
                Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(result.Observations).Knowledge);
                Assert.False(result.Observations[0].Persisted);
            }
            var restored = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, restored.Batches[0].Proposals[0].State);
            Assert.Equal(Binding(run.ToolAdmission!), Binding(restored));
            var reader = new ReceiptReader((_, _) => throw new InvalidOperationException("Recovery must retain the original confirmation."));
            Assert.Equal(1, (await ReconcileAsync(fixture, reader)).Counts.KnownCommitted);
            Assert.Equal(0, reader.Calls);
        }
        var padded = EffectsRun(source, 4);
        padded = padded with { ToolAdmission = padded.ToolAdmission! with {
            Batches = padded.ToolAdmission.Batches.Select(batch => batch with {
                Proposals = batch.Proposals.Select(proposal => proposal with {
                    Result = AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(new { retained = new string('r', 5 * 1024 * 1024) })),
                    State = AgentToolProposalState.ReconciliationRequired
                }).ToImmutableArray()
            }).ToImmutableArray()
        } };
        padded.ToolAdmission.Validate();
        await using (var fixture = await RestoreAsync(source, padded)) {
            var large = new AgentToolReceiptObservation(new("owner", "source"),
                AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(new { accepted = new string('a', 2 * 1024 * 1024) })));
            var result = await ReconcileAsync(fixture, new ReceiptReader((_, _) => new(large)));
            Assert.Equal(AgentEffectReconciliationStop.EvidenceCapacity, result.Stop);
            Assert.Equal(AgentEffectCheckpointPersistence.NotAttempted, result.Persistence);
            Assert.All(result.Observations, item => {
                Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, item.Knowledge);
                Assert.False(item.Persisted);
            });
            var saved = (await fixture.NewStore().GetExecutionRunAsync(padded.Id))!.ToolAdmission!;
            Assert.Equal(JsonSerializer.Serialize(padded.ToolAdmission.Batches), JsonSerializer.Serialize(saved.Batches));
            Assert.Null(saved.ReceiptReconciliationCursor);
        }
    }

    private static readonly AgentToolReceiptObservation ObservedReceipt = new(new("owner-private", "source-private"),
        AgentToolAdmissionJournalFixture.Envelope("{\"accepted\":\"owner-private\"}"));

    private static ExecutionRunRecord ChangeProposal(ExecutionRunRecord run, Func<AgentToolProposalRecord, AgentToolProposalRecord> change) {
        var journal = run.ToolAdmission!;
        var batch = journal.Batches[0];
        return run with { ToolAdmission = journal with {
            Revision = checked(journal.Revision + 1),
            Batches = journal.Batches.SetItem(0, batch with { Proposals = batch.Proposals.SetItem(0, change(batch.Proposals[0])) })
        } };
    }

    private static ExecutionRunRecord EffectsRun(AgentToolAdmissionJournalFixture fixture, int proposalCount) {
        var run = EffectsRun(fixture);
        var seed = run.ToolAdmission!.Batches[0];
        return run with { ToolAdmission = run.ToolAdmission with {
            Batches = Enumerable.Range(0, (proposalCount + 63) / 64).Select(batch => seed with {
                Id = new(Guid.NewGuid()), Ordinal = batch,
                Proposals = Enumerable.Range(0, Math.Min(64, proposalCount - batch * 64)).Select(index => seed.Proposals[0] with {
                    IntentId = new(Guid.NewGuid()), Ordinal = index, CallId = $"call-{batch}-{index}"
                }).ToImmutableArray()
            }).ToImmutableArray()
        } };
    }

    private static async Task<AgentEffectContinuation> ReconcileAsync(AgentToolAdmissionJournalFixture fixture,
        IAgentToolReceiptReconciliationProvider reader, CancellationToken cancellationToken = default) {
        var owner = fixture.NewJournal(fixture.NewStore());
        await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, cancellationToken);
        using var bound = lease.Bind();
        return await owner.ReconcileInterruptedAsync(lease, [reader], (_, _) => new(true), cancellationToken);
    }

    private sealed class ReceiptReader(Func<AgentToolReceiptReconciliationClaim, CancellationToken, ValueTask<AgentToolReceiptObservation>>? read = null)
        : IAgentToolReceiptReconciliationProvider {
        internal int Calls { get; private set; }
        public bool Supports(string toolName) => toolName == AgentToolAdmissionJournalFixture.Payload().ToolName;
        public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
            CancellationToken cancellationToken = default) {
            Calls++;
            await claim.RequireAsync(cancellationToken);
            return read is null ? ObservedReceipt : await read(claim, cancellationToken);
        }
    }
}
