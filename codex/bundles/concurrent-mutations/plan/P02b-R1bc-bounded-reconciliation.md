# R1b/R1c: bounded receipt recovery and conditional checkpoint

Status: source-reviewed design amendment. After R1a's 91 rows passed and the narrow
R1b-first review, the parent released the remaining R1b/R1c source work. The R1b
bounded slice, cursor, final CAS and Process observation now have an authored candidate
and 24 source-counted rows, **unbuilt and unexecuted**. R1c application wiring is not
yet present; there is no measurement or execution proof. This is the authoritative recovery budget
and store contract for [R1b](P02b-R1b-core-effect-reconciliation.md) and
[R1c](P02b-R1c-application-reconciliation.md).
Existing cancelled recovery and schemas 1–4 retain their meaning. No producer,
saved-effect dispatcher, provider or new generic coordinator is enabled here.

## Operational bounds

The journal's 512 batches × 64 proposals and 24 MiB serialized limit are retention
bounds, not a response or per-request lookup allowance. One explicit recovery command
examines at most **4 eligible uncertain owner-receipt proposals**, including missing
readers, misses and failed attempts. It performs at most one receipt lookup per such
proposal. It stops starting work after a **5-second cooperative slice budget** or
caller cancellation, then uses a separate **2-second cooperative checkpoint budget**
to preserve already observed evidence. No automatic loop starts another slice.

Budget starts before acquiring the run lease. Check cancellation/time before each
owner call and immediately after it; propagate a linked token into owner I/O. These
are cooperative limits, not hard OS/file/SQL wall-time promises. In particular,
`FileSandboxWorkspaceStore.SaveExecutionRunDetailCoreAsync` switches to
`CancellationToken.None` after publishing its durable commit journal. A cancellation
cannot abandon or pretend to undo that commit. The two-second checkpoint budget
limits entry/preparation; once its durable intent exists, finish the existing commit
protocol and report its actual acknowledgement or ambiguity. A timed-out lookup may
not authorize another effect or a speculative owner retry.

Keep at most four complete newly observed receipts. Each new observation is checked
with a bounded encoder against an **8 MiB complete-evidence budget**, including its
effect source strings and JSON overhead, before copying it into the candidate journal.
Thus retained new encoded evidence is at most **32 MiB**; this is not a managed-heap
allocation ceiling. Existing receipt envelopes already bound their payload at 8 MiB;
the complete-evidence check closes their currently unbounded source-string overhead
for new observations without rejecting an already valid historical journal on load.
An oversized otherwise valid observation yields bounded, nonpersisted commitment
knowledge and a capacity failure, never rejection or a truncated receipt. Provider
allocations before returning are outside this owner-side copying bound and must be
attributed in measurement. The final journal still must fit its unchanged **24 MiB**
limit; do not evict old intents/results or relax that limit to make recovery succeed.

Return at most four sanitized observations and **16 KiB serialized continuation JSON**.
Use fixed-size run/chat/batch/intent IDs, version/digest, closed enums, checked counts
and flags. Omit ToolName: its existing payload type has no individual length bound.
Never return receipt/source strings, raw journal, prepared input, SDK Result or
DisclosureEvidence. Measure JSON bytes using the actual public serializer; overflow
is an explicit contract error, not silent truncation. REST, SSE and native state use
this same compact carrier.

## Owner-local immutable slice

Use the actual independent file-store dispatch lease, not a process-local semaphore.
Interrupted lease admission already requires an atomic run-detail update to publish
ActiveDispatchLeaseId. Preserve the exact detail returned by that update as a private,
validated immutable snapshot on the read-only `AgentToolRunLease`; do not discard it
and reload it for every claim. Snapshot mutable collection views once into immutable
owner-held values where needed. Do not expose the full snapshot through a public claim,
accept one from a client, serialize/deserialize it again for each call, or use mtime,
file length or a prior request as a freshness cache.

For this lease mode only, `RequireReceiptReconciliationAsync` verifies the claim's
owner instance, ambient live lease, disposal state, exact original proposal binding,
approval/digest/dispatch claim and immutable session/profile/source from that snapshot.
It performs fresh current run-read/source-read observation at the defined boundaries.
The ordinary dispatch and cancelled paths retain their existing verification rules.
Holding the dispatch lease does not exclude cancellation, approval decisions or other
atomic run updates; snapshot reuse is safe because it authorizes **read-only lookup**
and every publication still requires the final fresh compare below.

Do one bounded classification pass over the admitted snapshot. Count all proposal
states, provider uncertainty and eligible owner receipts; retain only the next four
candidate bindings and the small public samples. Update aggregate counts by the at
most four observed transitions. Do not repeatedly enumerate all proposals, construct
one public outcome per proposal, copy every batch for each receipt, or serialize the
whole journal after each lookup. Clone each changed batch once, then perform one
candidate-journal validation and one logical batched checkpoint. Existing validation
also runs inside the store; the pass described here is not a claim of one validation
or one physical file write overall.

The checkpoint combines exact owner confirmations, continuation requirement,
WaitingOnTool/null terminal outcome and cursor into **one** existing
`UpdateExecutionRunDetailAsync` call. R1c must not subsequently reload and save a
separate waiting summary. Use the returned committed detail/compact result. Inside
the existing atomic update, load current detail and compare the admitted run revision,
journal revision, session/profile/generation/chat/source, active lease ID, run state
and outcome, pending approval collection, and exact originals of every observed
proposal. A concurrent cancellation, completion, decision, append or journal change
fails the checkpoint; do not merge a stale waiting state over it. No owner DB or
external current-policy await occurs inside the mutation callback. The current run
read is freshly checked immediately before publication and before returning new
knowledge; this is an observation boundary, not atomic permission-revocation fencing.

The final failed comparison preserves the current durable state. An actual receipt
observed earlier in the slice remains known committed **in this response only**, with
`Persisted = false`, provided current run read still succeeds. Return a typed checkpoint
conflict/failure, not a successful WaitingOnTool update. Do not promote the durable
proposal state, advance the cursor or fabricate an SDK result. If read is denied or
unavailable, disclose no new run-bound knowledge; the client may retain facts it already
knew. If the checkpoint acknowledgement is lost, mark persistence unconfirmed and
recover the same original operation on a later explicit command. No extra unbounded
recovery reread is hidden in this request's normal store-count budget.

## Fresh Process read without repeated full-run deserialization

Source evidence establishes two distinct paths:

| Existing seam | Full run read / needed change |
| --- | --- |
| `ProcessToolBackgroundSourcePolicy.ObserveAsync(execution, profile)` | Uses the supplied run with `ProcessExecutionClaimEvidenceReader.Read`, then current `EfProcessExecutionAuthorityQuery.ReadDispatchAsync` and source-policy observation. It does not reload the run. Keep those current SQL/catalog/project checks. |
| `ProjectDefinitionProcessReceiptAccess.CanReadAsync` → `ProjectProcessExecutionMutationService.ReadAccessAsync(Guid)` → `ProcessExecutionProjectAuthorityReader.ReadAsync(Guid)` | Calls `GetExecutionRunAsync` for every target receipt. Merely pinning the Core journal would leave this amplification intact. |

Add a narrowly named **read-only original-evidence observation** method to existing
`IProcessExecutionDispatchAuthorityReader` and its Process implementation, using the
existing `ProcessExecutionClaimEvidence` input and `ProcessExecutionDispatchAuthorityResult`
output. The method calls the same current Process owner query and profile checks
without retrieving another execution record. Existing implementations that do not
support it fail explicitly; there is no fallback to a stale dispatch snapshot.
The result is a current observation, not a mutation admission or capability.

```csharp
Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(
    ProcessExecutionClaimEvidence original,
    CancellationToken cancellationToken = default);
```

Workbench alone selects this method from a live pinned reconciliation claim. Its new
internal `ReadReceiptAccessAsync(originalAdmission, claim, cancellationToken)` must
first require that claim, decode/validate the exact saved SourcePreparation, and match
the original Process run/step/claim/executor/time, session/background fingerprint and
proposal binding before forwarding its original evidence. Reuse current source policy,
return `ProjectProcessExecutionAccess` with **CanWrite = false**, and preserve current
target/lifetime and Agent catalog read checks. Pass the verified claim only from
`ProjectDefinitionToolReceiptRecovery.ReconcileAsync` (the approved extraction from
mutation admission) into its private LookupAsync/receipt
access construction. Ordinary lookup/disclosure paths retain the GUID reader; effect
preparation/dispatch retains all current mutation admission/lease/token checks.
No JSON endpoint accepts this evidence as authority, and an arbitrary saved object
without the live owner claim cannot select the fast path. Core has no Process/Workbench
dependency. No registry or run-store cache is added.

### Exact pinned provenance contract

Before R1b-first, public `AgentToolReceiptReconciliationAdmission` contained only
Session, Binding and Payload. Those fields alone do not independently expose the
actual lease-admitted execution creation time. The released first subset adds the
projection below; do not fill that gap from `ProjectDefinitionToolSource.Process` JSON.

Add one optional typed Core-owned `AgentToolPinnedReceiptEvidence` projection to the
transient admission. Keep its existing three-argument constructor. The projection is
a sealed class with an internal constructor, get-only `DispatchLeaseId`,
`RunRevision`, `JournalRevision` and `ExecutionCreatedAtUtc` properties; it is not a
record with public `with` mutation and has no JSON construction contract. The admission
property is owner-set only and ignored by JSON. Populate it only from the successfully
committed detail returned by interrupted lease admission. Ordinary/cancelled claims
retain their current path and carry no pinned projection.

The lease owns the single projection instance. Before each exposure, the journal
verifies owner identity, ambient live lease, non-disposal, interrupted mode, exact
snapshot session/profile/source, lease ID and run/journal revision against its private
pin, plus the exact proposal binding/approval/digest/dispatch claim. The current-read
callback and current background source policy still run freshly. A copied projection
or copied source document is never accepted as a claim; the Workbench method receives
the actual `AgentToolReceiptReconciliationClaim` and calls `RequireAsync` itself.
Retaining the projection after disposal does not retain permission or enable a write.

Workbench decodes the SourcePreparation from that verified claim payload, preserves
its exact reservation/operation/expectation, and verifies it belongs to the same
admitted session. Compare the original Process evidence execution/agent IDs with
the session, StepKey with the admitted background SourceId, and
`ExecutionCreatedAtUtc` with the owner-set pinned value. Its original owner fingerprint
must hash to the admitted background OwnerFingerprint, as the current proposal codec
already requires (`ProjectDefinitionToolProposalCodec.cs:158`). After the new current
Process query, additionally require **exact current owner fingerprint equality** with
that original fingerprint before using its read policy. The existing
`ProjectDefinitionGovernedReadPolicy.ProcessCanRead` comparison alone is insufficient
for this optimization because it does not compare the owner fingerprint.

This equality binds run/root/plan/step/claim/executor/readiness/material fields through
the Process owner's existing canonical fingerprint; its SQL query also verifies the
original claim and assignment. The fingerprint intentionally does not include Agent
execution creation time (`EfProcessExecutionAuthorityQuery.cs:125`), which is why the
small pinned projection is necessary. Its other three fields identify the exact
owner lease/revision provenance rather than adding a detached timestamp that callers
could mistake for authority. No Process types or parser move into Core.

Return `ProjectProcessExecutionAccess` with `CanWrite = false` regardless of the
query's dispatch observation. Never feed this read result to reservation, mutation or
provider dispatch. Failure/missing pinned evidence on the interrupted path is an
explicit refusal, not a fallback to GUID lookup or client source text. Existing
ordinary and cancelled paths deliberately retain their existing GUID lookup behavior.
Owner confirmation still requires a complete validated receipt from exactly one
registered owner, fresh current run/source/target read and the final conditional
checkpoint; this projection cannot manufacture acceptance or bypass those checks.

The Core-only first checkpoint and its four carved-out test rows are specified in
[the R1b release table](P02b-R1b-core-effect-reconciliation.md#first-buildable-release-after-r1a).
The later real Process test remains a required proof; file-only Core fixtures cannot
establish this PostgreSQL observation or its read/write separation.

The existing Projects provider checks its claim before and after lookup. The journal
also enforces its own pre/post checks, and the Process adapter requires the claim
before using original evidence. Those fresh checks can repeat current SQL/catalog
observations, but none may deserialize the full run in this mode. Record actual
current-policy and owner SQL counts separately in proof; do not equate four lookups
with four total database queries or promise bounds for arbitrary third-party provider
implementations. The owner-loop call/receipt/response bounds remain enforced.

## Cursor, compatibility and honest progress

Add optional schema-5 `ReceiptReconciliationCursor`, containing the last examined
BatchId/IntentId and its immutable ordinals. Old schema-5 JSON without it starts at
the beginning. Schemas 1–4 require it absent. Validate that a nonnull cursor names an
existing exact proposal; appends preserve that identity. New normal writers retain
it unchanged. Only the interrupted reconciliation checkpoint may advance it, including
when a visited proposal has just become confirmed. No client supplies the cursor.

Traverse stable batch/proposal order strictly after the cursor, wrap once, and stop
before examining the same proposal twice in a slice. Skip ineligible/terminal states.
An absent reader, miss or completed failed lookup consumes a candidate slot and may
advance the cursor while uncertainty remains. This avoids starving later intents
behind the first unavailable owner. Stop before starting the fifth lookup. If time
or cancellation arrives before a lookup starts, do not mark that candidate examined.
Append/state changes after snapshot capture cause final CAS failure; a later slice
recaptures current order and proceeds from the last **durable** cursor. Cursor movement
and confirmations are inseparable in a successful checkpoint. Restart or failed
checkpoint cannot skip unpersisted observations. A partial lookup error can checkpoint
earlier valid observations and progress only under the same final compare/read checks;
the error remains visible and no failure becomes NotObserved.

Counts are from the exact snapshot revision, with at most four local transition deltas:
total proposals, persisted known commitments, eligible uncertain owner effects,
prepared effects, pending approvals, other unresolved proposals, and uncertain native
provider dispatches. Keep proposal and provider counts separate. Expose
`ExaminedThisSlice`, `UnexaminedEligibleInSnapshot` and the still-uncertain count as
different values. A miss is examined but unresolved. Previously confirmed proposals
are counted as `KnownCommitted` and **are not reread on ordinary receipt recovery**.
Apply commitment/state count deltas only after an acknowledged checkpoint. On CAS
failure or lost acknowledgement retain the admitted snapshot counts, label that
revision explicitly, and expose newly observed commitment only in the bounded samples
with nonpersisted/unconfirmed persistence status. Do not count it as a durable
confirmation or present snapshot counts as a fresh view after a conflict.
Detail was not requested; never imply it was available or currently authorized.
R1d's exact saved-output restoration must independently obtain owner detail under
current target read and verify equality, including expired/denied/missing cases.
That later detail operation is not permission to reexecute an effect.

Use a closed stop reason (ExhaustedSnapshot, LookupLimit, CooperativeBudget,
Cancelled, ReaderFailure, CheckpointConflict, CheckpointFailure, EvidenceCapacity)
and persistence status (Confirmed, NotAttempted, Unconfirmed). `HasMoreCandidates`
means the captured snapshot contains unexamined eligible work; it is not a promise
that current state is unchanged. Explicitly show unresolved work even when a slice
examined every eligible item. No background paging, fabricated success, permission to
retry an effect, or implicit new provider/input reason follows from any progress flag.

## Actual store work and measurement prerequisite

The initialized, healthy file-store application path has **five physical full
run-record reads**, independent of whether it examines one or four candidates:

1. Application `LoadExecutionRunAsync`: one.
2. Lease admission `UpdateExecutionRunDetailAsync`: `LoadRunDetailAsync` reads one;
   `PersistExistingRunPayloadAsync<ExecutionRunRecord>` rereads the original before
   replacing it: two.
3. One final conditional checkpoint follows the same two-read path: two.

A direct Core call that starts at lease admission has four. A CAS failure before
persisting the final detail may have fewer. The existing content-hash-validated
`FileSandboxWorkspaceSnapshotCache` can reuse immutable decoded records up to 8 MiB;
it still reads and hashes their full current bytes. Therefore actual deserialization
count may be lower than physical read count. Above 8 MiB each such read deserializes.
Existing `FileSandboxWorkspaceJsonReadDiagnostics` distinguishes Deserialization
from SnapshotValidation and records byte lengths; it is reused, not replaced by a
new cache or observer. Current source/read callbacks and the authored Process
original-evidence path add zero full-run reads. Any design that
reloads after admission, on claim Require, after lookup or for R1c's summary breaks
this count. Count actual deserializations, not only public store calls.

This is **not** five reads total, one 24 MiB physical write, or an unconditional
file-store cost bound. LoadRunDetail also reads session/approval/metric/log/artifact
records. Each update serializes an `ExistingRunDetailCommitJournal` containing both
previous and target detail, then writes run/session/index/projection records before
deleting that commit journal. Existing pending-commit recovery, legacy split/index
repair and cold initialization can read more records, including other runs; measure
those separately and do not report them as normal bounded-slice results. They require
their own owner-store optimization if measurement makes them a release blocker.

Before source release is called performant, instrument real existing-store read,
decode, serialized-byte and write boundaries in tests (no production metrics added
solely for convenience). Compare one versus four examined candidates on small and
near-24 MiB journals and at the maximum 32768-proposal shape, without asserting both
maxima necessarily fit one synthetic record. Record allocations/GC/elapsed time,
actual encoded receipt and public response bytes, whole-run decodes, validations,
physical writes and current Process SQL observations. A single classification/candidate
update must scale linearly in journal size, not journal size × examined proposals.
Five full reads (and, above the cache ceiling, decodes) plus durable previous/target copies can still be costly: measured
latency/allocation is an exit gate, not a claim that the limits prove acceptable cost.

## Exact prospective proof amendment

Retain the 18 R1b rows, renaming its confirmed-receipt case to
`Confirmed_receipt_is_terminal_and_summary_never_discloses_cached_detail` (no owner
lookup for that terminal proposal). Add these **6 new R1b Integration Facts**, giving
**24 authored R1b rows**: 23 file-store rows and the separate one-row real Process
fixture. These six additions are authored, not discovered or executed proof:

1. `Reconciliation_slice_counts_misses_and_missing_readers_with_a_four_candidate_limit`.
2. `Durable_cursor_wraps_without_starvation_and_survives_append_restart_and_old_schema_five`.
3. `Single_checkpoint_rejects_cancellation_state_approval_and_journal_races_without_dispatch`.
4. `Partial_failure_or_cancel_preserves_observed_commitment_and_only_persisted_cursor_progress`.
5. `Pinned_claims_and_batched_checkpoint_bound_full_run_decodes_independent_of_candidate_count`.
6. `Maximum_journal_slice_bounds_evidence_response_bytes_and_records_real_store_cost`.

Use finite matrices inside these Facts, not hidden dynamic provider expansion. Case 3
must run independent stores and real lease ownership; block owner lookup while another
writer cancels, completes, changes approvals or appends/revises the journal, then prove
failed CAS leaves that state intact and returns no provider/effect calls. Cases 4/6
include exact capacity, lost commit acknowledgement, denied final read and an explicit
nonpersisted flag; no claim that transient knowledge survived a crash. Case 5 counts
all physical full-run decodes including the persist-validation reread and proves
captured collections cannot be changed by the caller. Case 6 records measurements;
its acceptance threshold must be parent-reviewed before execution, not invented from
source or adjusted silently after a slow result.

Keep R1c's 28 proposed rows. Amend its actual expired-Process test to instrument the
complete claim/background/target path, proving no per-lookup run decode, fresh current
read revocation, and no write use of the original-evidence observer. Amend its REST/
SSE/readback cases for the 16 KiB/four-sample contract and honest unresolved/unexamined
counts. Its known-expired-detail case belongs to current detail projection only when
such detail is explicitly requested; the ordinary summary test instead proves sticky
KnownCommitted without querying expired detail. R1d retains the required real expired
receipt restoration proof; moving it does not waive it.

The existing R1b selected Integration baseline is 50; proposed new 24 yields **74**,
with existing R1a Unit 41 unchanged. R1c remains 28 prospective rows. Re-derive all
attributes/filters from authored source before parent discovery. The new observation
method, pinned lease and cursor are explicit implementation prerequisites; existing
APIs alone do not satisfy this amendment. No performance or activation claim is made.
