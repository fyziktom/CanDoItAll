# R1b/R1c owner-store cost: attribution and release gate

Status: source analysis and proposed measurement/repair sequence, **no optimization
implemented or measured**. The parent's frozen R1b 24 cases passed, including the
maximum-cost characterization. That is correctness evidence, not performance
qualification. R1c's separate 49-input candidate remains frozen. Actual producers
and R1d dispatch remain gated on this cost review as well as their existing ABI and
authority gates. This card supplements the bounded-reconciliation card; it does
not change its journal limits, current-read checks, operation identity or crash rules.

## Retained characterization and its limits

Parent-owned evidence: `.artifacts/concurrent-mutations/validation/c2-w1b-r1b-validation/r1b-cost-characterization.json`.
All byte quantities below are decimal bytes, not MiB or heap occupancy.

| Original proposals | Receipt lookups | Run reads / decodes | Run-read bytes | JSON publications / payload bytes | Elapsed ms | Process allocated bytes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1 | 4 / 2 | 22,522 | 12 / 64,161 | 207.247 | 4,899,888 |
| 4 | 4 | 4 / 2 | 35,364 | 12 / 85,518 | 219.805 | 5,258,528 |
| 32,768 | 4 | 4 / 4 | 141,911,650 | 12 / 235,948,901 | 2,404.720 | 2,171,814,784 |
| 192, near 24 MiB encoded journal | 4 | 4 / 4 | 100,760,088 | 12 / 151,304,348 | 1,371.539 | 1,755,345,160 |

`AgentToolEffectJournalIntegrationTests.Slices.cs:MeasureSliceAsync` measures from
owner construction/acquisition through `ReconcileInterruptedAsync`. It stops the
clock/allocation counter before the `await using` lease is disposed and before
serializing the public continuation. Its four read records cover only
`ExecutionRunRecord`; child files, catalogs, indexes and path/lock operations are
outside that byte total. The 12 writes are `workspace.json.write` activities in the
measurement trace, with encoded payload lengths. They are not 12 physical disk I/O
operations or a complete filesystem byte count. Two durable-intent publications
are separately observed. The returned continuation sizes were 761, 1,574, 1,585 and
1,579 bytes respectively.

`GC.GetTotalAllocatedBytes(precise: false)` is process-wide cumulative allocation.
Parallel tests were disabled, but background allocations can still contribute.
These results establish neither peak/live heap nor a host throughput/SLA. The fake
receipt reader returns `NotObserved`, invoking the real claim guard three extra
times per lookup. It does not measure PostgreSQL receipt reading, real Process
authority queries, newly accepted receipt insertion, UI readback or provider work.

## Pass 1: Initial Performance Review

The owner has bounded *receipt calls*, but its two whole-run commits repeatedly
encode and decode the retained journal. This is structural amplification rather
than evidence that four owner lookups themselves are expensive.

Relevant sources (paths are relative to the engine root):

| Owner seam | Current normal path and consequence |
| --- | --- |
| `Core/ToolPolicy/AgentToolAdmissionJournal.Effects.cs:AcquireInterruptedReconciliationAsync` under `src/MAF/Common/CanDoItAll.AgentFramework.Core` | Acquires the actual run file lease, then durably changes `ActiveDispatchLeaseId` and run/journal revisions before pinning the exact returned detail. This is the first full commit. |
| Same file, `ReconcileInterruptedAsync` / `RequireEffectCheckpoint` | Builds at most four confirmations, performs a fresh conditional update, and publishes cursor, knowledge and waiting state together. This is the second full commit. Same-revision transition validation also compares the pinned and freshly decoded journal. |
| `Persistence/Storage/FileSandboxWorkspaceStore.cs:UpdateExecutionRunDetailAsync` / `SaveExecutionRunDetailCoreAsync` under `src/MAF/Common/CanDoItAll.AgentFramework.Persistence` | Each update holds the existing store gate and cross-process workspace coordination, freshly loads detail, applies the callback, and builds a durable intent containing complete `PreviousDetail` and `TargetDetail` plus prior/target indexes. |
| Same file, `CommitExistingRunDetailJournalAsync` | Validates the intent again, writes the target run and projections, deletes the intent, and returns the exact target. After publishing intent it finishes with `CancellationToken.None`. Recovery uses the same commit method with `RecoveredJournal`. |
| `FileSandboxWorkspaceExecutionSliceStore.cs:PersistExistingRunPayloadAsync` | Reads the run again to require existing payload equal to previous or target. Large, separately decoded but unchanged records fall through to whole-JSON equality. A revision mismatch alone short-circuits some comparisons, but not the unchanged stored/previous comparison. |
| `FileSandboxWorkspaceJsonStore.cs:RequiresSave` / `Serialize` / `WriteObservedJsonAsync` | Same-reference/detail fast paths already exist. Remaining comparisons serialize whole objects into UTF16 strings. Writes serialize to a UTF16 string, count UTF8 bytes, then `DurableFileWriter.WriteTextAsync` allocates the UTF8 destination. |
| `Models/Execution/AgentToolJournalModels.cs:Validate` / `ValidateCore` under `src/MAF/Common/CanDoItAll.AgentFramework.Models` | Validation is memoized by immutable object identity in a `ConditionalWeakTable`. A new valid snapshot nevertheless allocates the entire compact `SerializeToUtf8Bytes(this)` result to verify the 24 MiB bound. Repeated `Validate()` calls on the same object do **not** repeat this encoding. |

### Source-derived traversal/copy attribution

For the characterized large, unchanged-file, `NotObserved` slice, the following are
normal-path source counts, to verify with instrumentation before assigning CPU or
allocation percentages. They are not inferred from a sampled allocation stack.
Let `J` be a compact journal encoding and `R` a persisted indented run encoding.

| Work | Encodings containing the whole journal | Full output allocation/copy |
| --- | ---: | --- |
| Validate original and lease-admitted journal; validate candidate and freshly read checkpoint basis | 4 | Four compact UTF8 results, approximately `4J`, plus serializer work buffers/validation collections. |
| Same-revision pinned/fresh journal equality in `RequireEffectCheckpoint` | 2 | Two compact UTF16 JSON strings; approximately `4J` character storage for ASCII JSON. |
| Stored/previous run equality, once per commit | 4 | Two pairs of indented UTF16 run strings; approximately `8R` character storage for ASCII JSON. |
| Two durable previous+target intents | 4 | Both journal copies are serialized in each intent, at deeper indentation. |
| Two target run publications | 2 | The target journal is serialized again in each run. |
| **Total** | **16** | Excludes small-index serializations, serializer scratch/pools, deserialized object graphs, child payloads and current-authority work. |

The core path also calls transition validation four times in acquisition and five
times in checkpoint preparation/commit, including the same-revision comparison.
Those traverse retained segments/batches/proposals even where `ValidateCore` is an
identity-cache hit. Normalization repeats sorting/copying child collection views;
it does not by itself deep-clone the journal. Attribute these separately rather
than counting every method named `Validate` as another full JSON encoding.

For ASCII encoded output, the current write path alone materializes approximately
`3 * W` bytes in final UTF16 strings plus explicit UTF8 destinations, where `W` is
the observed encoded publication payload sum: about 708 MB for the maximum-row
case and 454 MB for the near-limit case. This is a source-derived allocation
component, not the whole 2.17/1.76 GB, and assumes the measured fixture output is
ASCII (verify that with the diagnostic byte/character counts). Serializer scratch
buffers and their reuse can change process allocation measurements. It gives a
specific reduction to test, rather than an unsupported speedup estimate.

The fixture's 12 publications correspond to two sets of intent, run, execution
index, usage index, workspace index and chat projection. Unchanged session/empty
child writes do not publish in this fixture; populated histories may differ.
`DurableFileWriter.WriteStreamAsync` writes a temporary file using `WriteThrough`,
calls `FlushAsync` and `Flush(true)`, then performs guarded rename/replacement and
cleanup. Windows replacement may also rename the temporary file to a pending name.
OS writes, flushes, metadata I/O, retries and storage-device writes must be measured
separately; payload activity counts cannot quantify those physical operations.

## Read-only paths and refresh frequency

`AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:GetExecutionRunDetailAsync`
loads the full run and all requested detail collections, enriches native-tool
presentation, validates the effect journal, classifies every proposal, and applies
current read policy before and after classification. `ReadInterruptedContinuationAsync`
returns a bounded `SnapshotRead`, but the input work remains proportional to the
full retained record. It performs no receipt lookup, lease admission or publication.
The journal's weak identity cache prevents repeated encoding within this call; a
fresh large file decode creates a new identity and pays validation again.

`FileSandboxWorkspaceSnapshotCache` supports only run/chat payloads of at most
8 MiB, with 16 entries/64 MiB estimated retention. A hit still reads and hashes
fresh bytes; the two large fixtures bypass it. Increasing these bounds or adding a
per-request/full-run cache is not this plan. OS-cache warmth and owner snapshot-cache
warmth must be reported separately.

`AgentChatSession.LoadAsync` first calls `GetChatAgentWorkspaceAsync`. Its
`LoadLatestChatRunAsync` loads `SelectedRun` through `GetExecutionRunAsync`; then
the session explicitly calls `GetExecutionRunDetailAsync` for the selected run.
Thus a normal selected-run refresh reads the large run twice, even though only the
second result supplies the final selected run/continuation. Catalog/session/index
reads are additional. Relevant triggers in `AgentChatPanel.razor.cs` are manual
refresh, selection/reopen, command/recovery readback, first observation of a
different active run and a guarded external terminal update. There is no periodic
detail poll in these paths. `FloatingAgentChatCoordinator`'s timer prunes activity;
it is not a detail refresh interval. Multiple tabs or callers can still issue
concurrent fresh reads; do not invent a fixed requests/second rate for ordinary use.

Artifact/tool-receipt list methods also delegate to `GetExecutionRunDetailAsync`,
and scoped HTTP detail helpers use the same owner method. SSE already avoids its
extra post-command detail read when the result contains `EffectContinuation`;
preserve that behavior. The public detail DTO does not disclose the raw journal,
but constructing it does not avoid loading that journal. Trace response bytes for
both the compact continuation and the complete mapped detail separately.

Current interactive authority resolution and Process owner observations remain
fresh. `ProcessToolBackgroundSourcePolicy.ObserveAsync` reads authority from the
provided original run evidence and current Process/project policy; it does not
reload that run file. Pinned receipt claims have the reviewed original-evidence
fast path. Repeated current-read checks still perform their own metadata/owner
work: the characterization's four candidate checks plus three provider checks per
candidate, followed by two final checks, make 18 policy callbacks for four lookups.
Measure actual registered providers rather than assuming one policy query per lookup.

## Pass 2: Deep Pattern Scan

This is a targeted manual second pass using the .NET performance guidance; no
repository/static scanner or runtime profiler was executed during the parent's
freeze. Scope is the hot methods above, not a general style/refactor review.

Confirmed compound allocation patterns are whole-payload UTF16-to-UTF8 conversion,
whole-JSON semantic equality after a fresh decode, exact-size validation by allocating
the complete encoded result, and previous+target intent serialization followed by
target serialization. Existing reference/revision fast paths and bounded immutable
snapshot caching are positive findings, not missing optimizations. There is no
evidence here that LINQ syntax, locking primitives or EF query tuning explains the
measured allocation total. Allocation stacks and phase counters are required before
prioritizing a micro-optimization over these owner-store costs.

## Smallest ordered change candidates

These are review proposals, not permission to implement or relax an invariant.

1. **Single UTF8 serialization in the existing owner publication path.** Reuse
   `DurableFileWriter.WriteStreamAsync` with the same serializer options and guarded
   publication, or initially its `WriteBytesAsync` with one UTF8 encoding if that
   makes compatibility proof smaller. No new Infrastructure API is necessary.
   Preserve usage-history preparation's special string contract; do not silently
   route `ProviderUsageObservation` around its journal. Keep exact JSON bytes,
   including indentation, escaping and dates. A counting stream may record actual
   encoded bytes without storing the full string; record failed publication bytes
   as attempted, not successful. Do not remove flush/rename/cleanup checks.
2. **Owner-local prepared-plan evidence, not trusted caller data.** The private
   plan is created while the existing store gate/workspace coordination is held,
   from a fresh read and the exact callback result. Bind any validation witness to
   that owner instance, held scope, immutable previous/target identities, run/revision
   and exact plan; it must not be public, serializable or reusable after scope exit.
   Reuse successful plan/normalization/transition validation on the immediate
   `Prepared` path. A recovered serialized journal always gets full validation.
   Do not skip the current stored-payload check merely because a lock is held:
   external edits and recovered partial publication are existing explicit refusal
   cases. Prefer allocation-bounded exact structural/encoded comparison, or a
   fresh byte digest tied to the initial owner read. The smallest digest candidate
   records the digest of the exact bytes decoded for the already-retained previous
   detail; the commit still opens and hashes current bytes under the same lock.
   An exact match may reuse that validated immutable object. A mismatch must decode
   and fully validate the actual current payload against previous/target, preserving
   the existing handling of formatting-only changes and an already-published target;
   it must not treat the mismatch as evidence of acceptance. Recovery gets no
   ephemeral witness and retains full validation. This avoids an additional decoded
   graph on the unchanged prepared path, not the fresh-byte read. No mtime/size cache,
   arbitrary caller digest or silent use of a stale object is acceptable.
3. **Bounded validation encoding and same-snapshot transition equality.** Replace
   allocate-then-count with an exact bounded counting writer only if profiling
   confirms its contribution; preserve the serializer interpretation of schemas
   1–5 and every field/duplicate/invalid-state rule. A full structural equality
   implementation needs exhaustive field coverage and immutable-proof tests; a
   revision match alone never proves content equality. This also benefits fresh
   `SnapshotRead` without suppressing current authorization or classifying stale data.
4. **Eliminate the native refresh's redundant first full run through an explicit
   existing-owner read shape.** Make summary selection and one coherent selected
   detail available without loading then discarding a whole first run. Review the
   existing chat workspace contract/consumers first; do not pass a caller-supplied
   previously read run into the protected detail operation or cache its authority.
   Narrow artifact/receipt queries may similarly use their actual owner projection,
   but only with equivalent current read checks and unchanged output semantics.

Removing durable interrupted-lease admission would eliminate one whole commit,
but changes the reviewed lease/claim protocol. It is **not the first patch**. Any
later amendment must prove exact file-handle ownership, claims/cursor binding,
intervening cancellation/approval CAS and crash/lost-ack semantics before removing
`ActiveDispatchLeaseId` publication. Splitting historical journal storage or changing
the previous+target recovery format is also a separate durable-format migration,
not an incidental optimization. Neither is needed to test candidates 1–3.

## Controlled measurement proposal

Use standard Release outputs, the existing owned file-store fixture and parent
serialized execution. No new database/stack, provider call or background application
host is needed for the owner-store matrix. Record source/binary hashes, runtime/GC
configuration, machine memory and storage, exact fixture bytes/proposals/history,
and the ownership root before setup. Candidate and baseline use identical fixtures
and histories. Persist evidence outside disposable fixture roots. Keep failure and
cleanup outcomes; no automatic retry hides a failed/cancelled publication.

Authored opt-in source inventory, **unbuilt and unexecuted**: 12 explicit rows for three
workloads (`CoreSlice`, fresh `GetExecutionRunDetailAsync`/mapped response,
`NativeSessionRefresh`) × four existing shapes (1, 4, 32,768, near-24-MiB). Add eight
explicit rows for two maximum shapes × two workloads (slice/detail) × 2/4 concurrent
independent workspaces, plus two four-run same-workspace queue controls. Total 22
source-derived rows, kept outside ordinary Stable by `Category=LongRunning`. Reuse deterministic receipt/read
policies for the isolated matrix; an additional real interactive/Process authority
integration measurement is a prerequisite to an end-to-end claim, not implied by
these 22 rows. Existing 24 correctness rows remain independently selected.

Execute one verified display-name row per fresh testhost for the isolation pass;
verify source-expected discovery of one, not a broad unfiltered selection. Record
one cold owner instance, two warmups and seven measured repetitions with full
samples/median/max (no reliable p95 claim from seven observations). Keep later
warm reads explicit, including the existing <=8 MiB snapshot-cache boundary. Use
independent runs/workspaces for overlap and a barrier only before entry, not inside
the production lock. At most four are in flight; no new global semaphore is a fix.
Report same-workspace lock queue time separately from owner CPU/serialization.

Measure through awaited lease disposal and the actual response mapping/encoding,
with phase deltas for acquire, owner lookup, conditional checkpoint, release and
readback. Use precise process allocation counters only in an otherwise idle owned
testhost, record idle overhead and allocations per phase, and do not blindly
subtract an idle sample. Never use thread allocation counters across awaits.
Capture GC heap/LOH, collections, GC pause/time, CPU, working set and private bytes;
report cumulative allocations and peak live/committed memory as different metrics.
A post-run forced GC, if used to inspect retention, is a separately timed diagnostic,
never part of the latency loop or a production workaround.

In a separate diagnostic pass, capture allocation stacks and GC events with the
available .NET diagnostic toolchain; allocation sampling is attribution, not an
exact census. First verify installed tool/runtime compatibility; installation or
trace launch requires the parent's separate execution release. Compare unprofiled
timings; tracing overhead is not a candidate regression. Add only bounded type/phase,
byte/count and completion tags to existing activities. Do not emit JSON, prompts,
source authority, secrets or arbitrary paths. In particular count actual
`ValidateCore` cache misses, full comparer encodings, prepared/recovered validations,
run/child/index reads, and JSON encode invocations rather than inferring from names.

Use process/file I/O events filtered to the recorded fixture to quantify logical
reads/writes, temporary/target files, flushes, renames/deletes and retries. OS/device
cache behavior must be stated; a process byte counter cannot establish physical
device traffic. Capture all pending-intent and target bytes and their maximum
simultaneous retained buffers. The existing payload activities remain useful but
are not relabeled as total or physical I/O.

## Proposed acceptance criteria and remaining approval

* Correctness is mandatory: all original-schema, lease/claim/read revocation,
  cancellation/approval races, cursor fairness, partial-publication replay,
  acknowledged/lost-ack and cleanup-knowledge tests must retain their assertions.
  A fresh read can never become a write, provider/tool execution or lease admission.
* Structural gates for the first change: identical accepted bytes/interpretation,
  unchanged two durable intents and successful-publication counts, no extra owner
  lookup or full read. Remove the large UTF16 write encoding where selected and
  demonstrate it in allocation stacks/counters. Prepared evidence may remove
  repeated validation/decoding only at its reviewed boundary; recovered journals
  retain full validation. Four lookups and 16 KiB continuation remain limits.
* The existing 5-second/2-second budgets remain cooperative admission/checkpoint
  limits; they are not a hard latency SLA. In controlled candidate measurements,
  require every no-fault maximum slice to reach its intended four lookups and
  confirmed checkpoint rather than consume its budget in store preparation. Report
  total time including noncancelable commit/release and any over-budget work. Use
  matched samples and per-phase results; a single fastest sample cannot qualify.
* The accepted provisional reference profile is **four independent maximum-size
  runs**, with an **8 GiB application envelope, 2 GiB idle reserve and at most
  3 GiB incremental observed private bytes**, plus the four-run same-workspace
  queue control. This reserves half the remaining 6 GiB for other work. Record
  actual pre-setup and pre-operation private/committed/managed memory, physical
  headroom, the last GC's total available memory and Windows current-process commit
  headroom. A pre-operation baseline above 2 GiB, physical/commit headroom below
  6 GiB or a GC total available below 8 GiB invalidates this reference profile.
  Report whole-row and operation-entry increments independently. Setup is included
  in whole-row peak evidence: a small operation increment cannot conceal an
  oversized setup/retained baseline. An exceeded setup envelope requires attribution
  before making an application-capacity claim. No forced GC makes the profile fit.
  No OOM, paging-driven stall or memory-growth trend is acceptable. These are
  declared engineering margins, not a production limit or enforced OS memory cap.
* Allocation acceptance is tied to removed work: compare measured reductions with
  the captured eliminated string/UTF8/comparison bytes. Investigate unexplained
  residual full-size buffers and source-count mismatches. Do not set a GB-to-MB
  target from the process counter alone. Peak-memory and GC evidence, not merely
  reduced cumulative allocation, decide concurrency readiness. Record small-read,
  full-detail and native-refresh regressions separately; no tradeoff may hide an
  expensive read-only path behind a smaller continuation response.

Unresolved gates are the isolated allocation stacks/peaks, measured reference
headroom/concurrency fit, exact prepared witness/compatibility design and
the smallest coherent native read shape. No measured improvement, physical-I/O
reduction, maximum service rate, enabled runtime policy or production readiness is
claimed by this card.

## Authored instrumentation-only checkpoint

The four new Integration source files are `AgentReceiptStorePerformanceTests.cs`,
`AgentReceiptStorePerformanceFixture.cs`, `AgentReceiptStorePerformanceProbe.cs`
and `AgentReceiptStorePerformanceEvidence.cs`. The only existing fixture edit adds
an optional `ownedParentRoot` argument to `AgentToolAdmissionJournalFixture.CreateAsync`;
callers that omit it retain the existing behavior. There is no
production or project-reference change in this instrumentation checkpoint. The
parked UTF8 JsonStore candidate is explicitly excluded until a matched baseline
has been retained. The 56-file R1c inventory is unchanged.

The three public theory methods carry exactly 12, 8 and 2 explicit enum InlineData
rows. No MemberData, inherited test, conditional skip or generated row is involved.
After building standard Release outputs, the parent verifies aggregate discovery
of **22** with the class FullyQualifiedName filter, then verifies **one** row using
that exact method plus the supported DisplayName filters for workload, shape and,
where present, `concurrency: 2` or `concurrency: 4`. The list output must show the
expected enum arguments; an unrecognized display filter is a blocked runner gate,
not permission to execute several rows. Every execution starts a fresh testhost;
an in-process one-row guard rejects a second invocation. Compare retained PID plus
process start time, source manifest/hash and binary hashes across the matched runs.
Do not launch the whole class once: its second row is deliberately refused.

The parent supplies existing, absolute, explicitly owned paths in
`CDITALL_RECEIPT_PERFORMANCE_OUTPUT_ROOT`,
`CDITALL_RECEIPT_PERFORMANCE_SOURCE_ROOT` and
`CDITALL_RECEIPT_PERFORMANCE_SOURCE_MANIFEST`, and the closed
`CDITALL_RECEIPT_PERFORMANCE_VARIANT` value `Baseline` or `Candidate`. The manifest
is the parent's reviewed complete source input inventory; the row also hashes its
direct owner/harness source and actually loaded assemblies. Each immutable
CreateNew intent records the process identity and intended unique fixture subtree
before allocation. Actual fixture roots are recorded after preparation. Evidence
stays outside those roots. Recursive cleanup is owned by the existing fixture;
the harness first refuses reparse-point paths, then verifies only empty parent
directories remain. A missing terminal manifest, failed cleanup or parent timeout
requires exact manifest reconciliation; it is never inferred successful or retried.
The parent still owns the process-tree deadline and any interrupted cleanup.

Each row uses one cold-owner observation, two warmups and seven measured cohorts.
Every operation constructs a fresh store/service; OS caches are not flushed.
Seeding is sequential and outside operation timing. Large setup journals and seed
store caches are not retained in harness targets: only a journal-free original
run's identity/authority metadata, the exact session reference and scalar shape
measurements survive. Synthetic profile, run, chat, authority, context, origin,
segment, batch, proposal and original dispatch identities use versioned deterministic
per-worker seeds; initial timestamps are fixed at 2026-10-01 UTC. Initial compact
run/journal SHA256 values must match between baseline and candidate. The source
template is small and disposed. Whole-row memory
still includes setup allocations and any delayed reclamation. The initial shape
sizes are explicitly compact/default JSON sizes, not on-disk byte counts.

The saved runs are deliberately cumulative across the ten cohorts. A successful
slice advances the same original lease, cursor and revisions; it does not reset
the effect identity or publish a new original proposal. NotObserved keeps the same
candidate population eligible, including cursor wrap. Per-worker resulting run and
journal revisions are retained. Detail/native reads do not advance them. This is
identical in the matched baseline/candidate; no failed sample is automatically
retried and the row stops after preserving its evidence. The deterministic receipt
provider performs one additional actual claim RequireAsync, unlike the earlier
characterization's three; those old timings are not a matched baseline. The actual
owner clock is unchanged: subsequent UpdatedAtUtc/checkpoint observations and new
admitted dispatch-lease IDs remain nondeterministic, as recorded in each intent.
No extra full-run read is added just to capture those timestamp fields. Elapsed and
measurement timestamps remain real. No production identity behavior changes.

CoreSlice includes real admission, four-or-fewer deterministic receipt-provider observations, conditional
checkpoint, awaited lease release, bounded response encoding and owner disposal.
FreshDetail calls the real service and `AgentApiResponseMapper.ToExecutionRunDetail`.
NativeSessionRefresh calls real `AgentChatSession.LoadAsync`, catalog/workspace/detail
reads and state acceptance, followed by encoding only its bounded continuation for
measurement; this is not a browser rendering/SignalR transport measurement. Both
use the actual `ConfigureAgentApiJson` options. Provider execution throws if reached.
The native provider catalog is the only stub. Actual service authority-resolver
calls are counted, including failure cleanup, rather than reported as zero.

Each 2/4-way cohort records worker and phase start/end offsets and the actual maximum
overlapping operations. Each independent workspace has its own real file owner;
the shared-workspace control has four distinct run/chat/session identities and four
store instances using the existing shared workspace lock/indexes. The only barrier
is before entry. Existing coordination-wait activities are retained; no fairness or
maximum-throughput claim follows from this queue control.

Precise process allocation is taken once around the entire cohort through all
owners' disposal and encoding. Concurrent per-phase allocation is null, not summed
from overlapping process deltas. Serial phase deltas are labeled process-wide.
A separately reported one-second idle window runs the same two 20 ms samplers and
an empty activity listener; its overhead is not subtracted. Sample interval, maximum
observed gap and sample count accompany maximum observed managed estimate,
last-GC heap/LOH/committed, private bytes and working set. Last GC index zero means
there is no completed-GC sample; it is not evidence of an empty heap. OS lifetime
peak working set is recorded separately. Polling can miss transients; diagnostic
GC/allocation/OS evidence must corroborate any later peak/retention qualification.

Existing activities retain the enclosing typed worker/phase, separating admission
commit activities from final checkpoint commit activities. Individual deterministic
receipt-provider callbacks also have bounded phase intervals. The original read
diagnostics and activity payload bytes are logical observation
counts, not total or physical storage I/O. Activities may nest; their durations
cannot be added as wall time. The bounded event inventory marks overflow explicitly.
Per-worker continuation knowledge is captured before release/encoding so a cleanup
or encoding error cannot discard reached stop/persistence evidence. Failed worker
types, partial results, memory window and phase failures are saved before assertions
or qualification checks. Workload failure and cleanup status remain separate.
Evidence publication failure also leaves the row incomplete. No measurement has
been executed and no improvement/capacity claim is made by this source checkpoint.
