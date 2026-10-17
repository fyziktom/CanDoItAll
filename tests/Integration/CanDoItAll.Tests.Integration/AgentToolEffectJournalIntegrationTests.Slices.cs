using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class AgentToolEffectJournalIntegrationTests {
    private readonly ITestOutputHelper output;

    public AgentToolEffectJournalIntegrationTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task Reconciliation_slice_counts_misses_and_missing_readers_with_a_four_candidate_limit() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        foreach (var missing in new[] { false, true }) {
            var run = EffectsRun(source, 8);
            await using var fixture = await RestoreAsync(source, run);
            var reader = new ReceiptReader((_, _) => new(AgentToolReceiptObservation.NotObserved));
            var owner = fixture.NewJournal();
            await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
            using var bound = lease.Bind();
            var result = await owner.ReconcileInterruptedAsync(lease, missing ? [] : [reader], (_, _) => new(true), default);
            Assert.Equal(4, result.ExaminedThisSlice);
            Assert.Equal(4, result.UnexaminedEligibleInSnapshot);
            Assert.Equal(8, result.Counts.EligibleOwnerEffects);
            Assert.Equal(AgentEffectReconciliationStop.LookupLimit, result.Stop);
            Assert.Equal(missing ? 0 : 4, reader.Calls);
            Assert.All(result.Observations, item => Assert.Equal(missing
                ? AgentEffectReceiptKnowledge.ReaderUnavailable : AgentEffectReceiptKnowledge.ReceiptNotObserved, item.Knowledge));
            var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[3].IntentId, saved.ReceiptReconciliationCursor!.IntentId);
            Assert.Equal(JsonSerializer.Serialize(run.ToolAdmission.Batches), JsonSerializer.Serialize(saved.Batches));
        }
    }

    [Fact]
    public async Task Durable_cursor_wraps_without_starvation_and_survives_append_restart_and_old_schema_five() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var run = EffectsRun(source, 10);
        var json = JsonSerializer.Serialize(run.ToolAdmission);
        Assert.DoesNotContain(nameof(AgentToolJournalRecord.ReceiptReconciliationCursor), json, StringComparison.Ordinal);
        var old = JsonSerializer.Deserialize<AgentToolJournalRecord>(json)!;
        old.Validate();
        Assert.Null(old.ReceiptReconciliationCursor);
        await using var fixture = await RestoreAsync(source, run with { ToolAdmission = old });
        var reader = new ReceiptReader((_, _) => new(AgentToolReceiptObservation.NotObserved));
        var first = await ReconcileAsync(fixture, reader);
        var second = await ReconcileAsync(fixture, reader);
        Assert.Equal(run.ToolAdmission!.Batches[0].Proposals.Take(4).Select(item => item.IntentId), first.Observations.Select(item => item.Binding.IntentId));
        Assert.Equal(run.ToolAdmission.Batches[0].Proposals.Skip(4).Take(4).Select(item => item.IntentId), second.Observations.Select(item => item.Binding.IntentId));
        var appended = run.ToolAdmission.Batches[0] with {
            Id = new(Guid.NewGuid()), Ordinal = 1,
            Proposals = run.ToolAdmission.Batches[0].Proposals.Take(2).Select((item, index) => item with {
                IntentId = new(Guid.NewGuid()), Ordinal = index
            }).ToImmutableArray()
        };
        await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(run.Id, current => current with {
            Run = current.Run with { Revision = current.Run.Revision + 1, ToolAdmission = current.Run.ToolAdmission! with {
                Revision = current.Run.ToolAdmission.Revision + 1, Batches = current.Run.ToolAdmission.Batches.Add(appended)
            } }
        });
        var third = await ReconcileAsync(fixture, reader);
        Assert.Equal(run.ToolAdmission.Batches[0].Proposals.Skip(8).Select(item => item.IntentId)
            .Concat(appended.Proposals.Select(item => item.IntentId)), third.Observations.Select(item => item.Binding.IntentId));
        var wrapped = await ReconcileAsync(fixture, reader);
        Assert.Equal(first.Observations.Select(item => item.Binding), wrapped.Observations.Select(item => item.Binding));
        var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
        Assert.Throws<InvalidDataException>(() => (saved with {
            ReceiptReconciliationCursor = saved.ReceiptReconciliationCursor! with { IntentId = new(Guid.NewGuid()) }
        }).Validate());
        Assert.Throws<AgentToolAdmissionException>(() => AgentToolJournalTransitions.ValidatePersistence(saved,
            saved with { Revision = saved.Revision + 1, ReceiptReconciliationCursor = null }));
    }

    [Fact]
    public async Task Single_checkpoint_rejects_cancellation_state_approval_and_journal_races_without_dispatch() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        foreach (var race in Enum.GetValues<CheckpointRace>()) {
            var run = WithPendingApproval(EffectsRun(source, 2));
            await using var fixture = await RestoreAsync(source, run);
            ExecutionRunDetail? concurrent = null;
            var reader = new ReceiptReader(async (_, token) => {
                concurrent = await ((ISandboxWorkspaceExecutionRunMutationStore)fixture.NewStore()).UpdateExecutionRunDetailAsync(run.Id,
                    current => ApplyRace(current, race), token);
                return ObservedReceipt;
            });
            var result = await ReconcileAsync(fixture, reader);
            Assert.Equal(AgentEffectCheckpointPersistence.NotAttempted, result.Persistence);
            Assert.Equal(AgentEffectReconciliationStop.CheckpointConflict, result.Stop);
            Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, Assert.Single(result.Observations).Knowledge);
            Assert.False(result.Observations[0].Persisted);
            var saved = (await fixture.NewStore().GetExecutionRunDetailAsync(run.Id))!;
            Assert.Equal(JsonSerializer.Serialize(concurrent), JsonSerializer.Serialize(saved));
            Assert.Null(saved.Run.ToolAdmission!.ReceiptReconciliationCursor);
            Assert.Null(saved.Run.ToolAdmission.Batches[0].Proposals[0].OwnerReceipt);
            Assert.Equal(1, reader.Calls);
        }
    }

    [Fact]
    public async Task Partial_failure_or_cancel_preserves_observed_commitment_and_only_persisted_cursor_progress() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        foreach (var cancel in new[] { false, true }) {
            var run = EffectsRun(source, 6);
            await using var fixture = await RestoreAsync(source, run);
            using var request = new CancellationTokenSource();
            var calls = 0;
            var reader = new ReceiptReader((_, _) => {
                calls++;
                if (calls == 2) {
                    throw new IOException("Injected second owner read failure.");
                }
                if (cancel) {
                    request.Cancel();
                }
                return new(ObservedReceipt);
            });
            var result = await ReconcileAsync(fixture, reader, request.Token);
            Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
            Assert.Equal(cancel ? AgentEffectReconciliationStop.Cancelled : AgentEffectReconciliationStop.ReaderFailure, result.Stop);
            Assert.Equal(cancel ? 1 : 2, result.ExaminedThisSlice);
            Assert.True(result.Observations[0].Persisted);
            Assert.Equal(AgentEffectReceiptKnowledge.ReceiptConfirmed, result.Observations[0].Knowledge);
            var saved = (await fixture.NewStore().GetExecutionRunAsync(run.Id))!.ToolAdmission!;
            Assert.Equal(run.ToolAdmission!.Batches[0].Proposals[result.ExaminedThisSlice - 1].IntentId, saved.ReceiptReconciliationCursor!.IntentId);
            Assert.Equal(AgentToolProposalState.OwnerReceiptConfirmed, saved.Batches[0].Proposals[0].State);
            Assert.Equal(run.ToolAdmission.Batches[0].Proposals[1], saved.Batches[0].Proposals[1]);
            var next = await ReconcileAsync(fixture, new ReceiptReader((_, _) => new(AgentToolReceiptObservation.NotObserved)));
            Assert.Equal(1, next.Counts.KnownCommitted);
            Assert.Equal(run.ToolAdmission.Batches[0].Proposals[result.ExaminedThisSlice].IntentId, next.Observations[0].Binding.IntentId);
        }
    }

    [Fact]
    public async Task Pinned_claims_and_batched_checkpoint_bound_full_run_decodes_independent_of_candidate_count() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var measured = new List<ReceiptStoreCost>();
        foreach (var candidates in new[] { 1, 4 }) {
            await using var fixture = await RestoreAsync(source, EffectsRun(source, candidates));
            measured.Add(await MeasureSliceAsync(fixture, candidates));
        }
        Assert.All(measured, cost => {
            Assert.Equal(4, cost.FullRunReads);
            Assert.InRange(cost.FullRunDeserializations, 1, 4);
            Assert.Equal(2, cost.DurableIntents);
            Assert.True(cost.Writes > 2);
            Assert.True(cost.ReadBytes > 0);
            Assert.True(cost.WrittenBytes > 0);
        });
        Assert.Equal(measured[0].FullRunReads, measured[1].FullRunReads);
        Assert.Equal(measured[0].DurableIntents, measured[1].DurableIntents);
    }

    [Fact]
    public async Task Maximum_journal_slice_bounds_evidence_response_bytes_and_records_real_store_cost() {
        await using var source = await AgentToolAdmissionJournalFixture.CreateAsync();
        var maximumRows = EffectsRun(source, AgentToolJournalRecord.MaximumBatches * AgentToolJournalRecord.MaximumCallsPerBatch);
        maximumRows.ToolAdmission!.Validate();
        var maximumBytes = EffectsRun(source, 192);
        var baseline = JsonSerializer.SerializeToUtf8Bytes(maximumBytes.ToolAdmission).Length;
        var padding = (AgentToolJournalRecord.MaximumUtf8Bytes - 64 * 1024 - baseline) / 3;
        maximumBytes = maximumBytes with { ToolAdmission = maximumBytes.ToolAdmission! with {
            Batches = maximumBytes.ToolAdmission.Batches.Select(batch => batch with {
                Response = AgentToolAdmissionJournalFixture.Envelope(JsonSerializer.Serialize(new { retained = new string('x', padding) }))
            }).ToImmutableArray()
        } };
        maximumBytes.ToolAdmission.Validate();
        Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(maximumBytes.ToolAdmission).Length, 23 * 1024 * 1024, AgentToolJournalRecord.MaximumUtf8Bytes);
        foreach (var run in new[] { maximumRows, maximumBytes }) {
            await using var fixture = await RestoreAsync(source, run);
            var cost = await MeasureSliceAsync(fixture, expectedCandidates: 4);
            Assert.Equal(4, cost.FullRunReads);
            Assert.Equal(2, cost.FullRunDeserializations);
            Assert.Equal(2, cost.DurableIntents);
            Assert.InRange(cost.ResponseBytes, 1, AgentEffectContinuation.MaximumUtf8Bytes);
            Assert.True(cost.ReadBytes > AgentToolJournalRecord.MaximumUtf8Bytes);
        }
    }

    private async Task<ReceiptStoreCost> MeasureSliceAsync(AgentToolAdmissionJournalFixture fixture, int expectedCandidates) {
        var reads = new ConcurrentQueue<FileSandboxWorkspacePhysicalJsonRead>();
        var writes = new ConcurrentQueue<long>();
        var intents = 0;
        var store = new FileSandboxWorkspaceStore(fixture.WorkspaceRoot, fixture.StorageScope, null,
            stage => {
                if (stage == ExistingRunDetailCommitStage.JournalPersisted) {
                    intents++;
                }
            }, new FileSandboxWorkspaceJsonReadDiagnostics(reads.Enqueue));
        await store.LoadCatalogSnapshotAsync();
        reads.Clear();
        using var root = new Activity("receipt-slice-measurement").Start();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.json.write") {
                    writes.Enqueue(Convert.ToInt64(activity.GetTagItem("payload.bytes"), System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        var allocated = GC.GetTotalAllocatedBytes(precise: false);
        var elapsed = Stopwatch.StartNew();
        var owner = fixture.NewJournal(store);
        await using var lease = await owner.AcquireInterruptedReconciliationAsync(fixture.Session, default);
        using var bound = lease.Bind();
        var lookupReads = new List<int>();
        var reader = new ReceiptReader(async (claim, token) => {
            lookupReads.Add(reads.Count(item => item.PayloadType == typeof(ExecutionRunRecord)));
            for (var count = 0; count < 3; count++) {
                await claim.RequireAsync(token);
            }
            return AgentToolReceiptObservation.NotObserved;
        });
        var result = await owner.ReconcileInterruptedAsync(lease, [reader], (_, _) => new(true), default);
        elapsed.Stop();
        var delta = GC.GetTotalAllocatedBytes(precise: false) - allocated;
        Assert.Equal(expectedCandidates, reader.Calls);
        Assert.All(lookupReads, count => Assert.Equal(2, count));
        Assert.Equal(AgentEffectCheckpointPersistence.Confirmed, result.Persistence);
        var full = reads.Where(item => item.PayloadType == typeof(ExecutionRunRecord)).ToArray();
        var cost = new ReceiptStoreCost(full.Length, full.Count(item => item.Kind == FileSandboxWorkspaceJsonReadKind.Deserialization),
            full.Sum(item => item.LengthBytes), writes.Count, writes.Sum(), intents,
            JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length,
            elapsed.Elapsed.TotalMilliseconds, delta);
        output.WriteLine("receipt-slice-source-proof " + JsonSerializer.Serialize(new {
            proposals = result.Counts.TotalProposals, expectedCandidates, cost,
            allocationScope = "process-wide; concurrent allocations may contribute", latencyScope = "this owned file fixture only"
        }));
        return cost;
    }

    private static ExecutionRunRecord WithPendingApproval(ExecutionRunRecord run) {
        var journal = run.ToolAdmission!;
        var batch = journal.Batches[0];
        var pending = batch.Proposals[1] with {
            State = AgentToolProposalState.Prepared, RequiresApproval = true, ApprovalStatus = ExecutionApprovalStatus.Pending,
            ApprovedDigest = null, DispatchClaimId = null, ApprovalId = "pending-second"
        };
        var approval = new PendingToolApprovalRecord(pending.ApprovalId, pending.CallId, pending.Payload.ToolName, "function",
            "Original decision", pending.Payload.ArgumentsJson) {
            ToolAdmission = new(batch.Id, pending.IntentId, pending.Payload.SemanticVersion, pending.Payload.Digest)
        };
        return run with { PendingApprovals = [approval], ToolAdmission = journal with {
            Batches = journal.Batches.SetItem(0, batch with { Proposals = batch.Proposals.SetItem(1, pending) }),
            Segments = journal.Segments.SetItem(0, journal.Segments[0] with { PendingApprovals = [approval] })
        } };
    }

    private static ExecutionRunDetail ApplyRace(ExecutionRunDetail current, CheckpointRace race) {
        var run = current.Run with { Revision = current.Run.Revision + 1, UpdatedAtUtc = DateTimeOffset.UtcNow };
        run = race switch {
            CheckpointRace.Cancellation => run with { State = ExecutionState.Failed, Outcome = RunOutcome.Cancelled, CompletedAtUtc = DateTimeOffset.UtcNow },
            CheckpointRace.Completion => run with { State = ExecutionState.Completed, Outcome = RunOutcome.Succeeded, CompletedAtUtc = DateTimeOffset.UtcNow },
            CheckpointRace.Approval => run with {
                ToolAdmission = AgentToolJournalTransitions.ApplyDecisions(run.ToolAdmission!, run.PendingApprovals,
                    [new(run.PendingApprovals[0].ApprovalId, true) { ToolAdmission = run.PendingApprovals[0].ToolAdmission }], automatic: false),
                PendingApprovals = []
            },
            CheckpointRace.Journal => run with { ToolAdmission = run.ToolAdmission! with {
                Revision = run.ToolAdmission.Revision + 1, ToolContinuationRequirement = AgentToolContinuationRequirement.EffectReconciliationRequired
            } },
            _ => throw new ArgumentOutOfRangeException(nameof(race))
        };
        return current with { Run = run };
    }

    private enum CheckpointRace { Cancellation, Completion, Approval, Journal }
    private sealed record ReceiptStoreCost(int FullRunReads, int FullRunDeserializations, long ReadBytes, int Writes,
        long WrittenBytes, int DurableIntents, int ResponseBytes, double ElapsedMilliseconds, long ProcessAllocatedBytes);
}
