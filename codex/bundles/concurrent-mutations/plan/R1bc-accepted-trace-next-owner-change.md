# Accepted receipt trace: next owner-store change

Status: A released for isolated source preparation after the parent's accepted
diagnostic and recursive immutability review; candidate authored but unbuilt and
unexecuted. C remains a separate proposal. The completed UTF8
experiment remains 62 compatibility passes and 10/22 performance passes, with
12 failures; it has not qualified the supported capacity envelope. See
[the UTF8 validation record](../validation/P02b-R1bc-utf8-candidate.md) for that matched experiment.

The source-only A candidate and exact proof selection are recorded in
[the prepared-run witness record](../validation/R1bc-prepared-run-witness-source.md).
Its production copies are isolated under an owned ignored scratch directory and
start from the profiled main predecessors. The managed historical graph is unchanged.

Choose **A, a run-only immutable prepared-read witness**, before **C, bounded
streaming journal-size validation**. Change and measure one at a time. Retain the
current durable previous/target intent, all recovery validation, current authority,
two effect checkpoints, file coordination and original run expectations. Whole-plan
validation reuse, a larger cross-request cache and a storage-format split are not
part of the first candidate.

## Evidence and provenance

The parent collected one fresh owned host for
`AgentReceiptStorePerformanceTests.Serial_owner_slice_detail_and_native_refresh`
with `CoreSlice / MaximumProposals`. The test ended naturally after three samples,
zero measured samples and a workload failure. Fixture cleanup passed. Diagnostic
collection and strict parsing succeeded independently of that failure. This is the
UTF8 candidate plus explicit test-only collection handshake; it is not the original
baseline binary and does not supply a matched performance comparison.

All paths below are beneath main
`.artifacts/concurrent-mutations/validation/r1bc-explicit-gc-diagnostic-v2/`:

| Evidence | SHA256 |
| --- | --- |
| `analysis3/report.json` | `3E4CDEFC8D5E14193B6A35A74074CFA8EE01DCBF7D5345643324B2C19C86A4BC` |
| Complete trace, 40,219,650 bytes | `FC54220584A8FF9197D9EC4AEEE1DB849A216F75F54E32E05460437A0F9FB820` |
| `diagnostic-closure.json` | `052AE97EAD758C44446868E536F31C9663302F8DF8869AD7F225D7B234ECB31A` |
| `collection-evidence.json` | `30264D096370B729D4CF1A3D0B104CEA05265EA888EF7C42E4AD082A6511AA71` |
| `one-row.result.json` | `D4A013F595907FE139B0B77B8518294B6BEA3102587808AA92BB541B9987C0CF` |
| Frozen source/binary manifest identity supplied to parser | `447CE256CF9DD46B483EFED96157C470F1DADD88834D90634926F41478D34C2B` |
| Parser source inventory | `BD7A6FC85FF65BCC087BCE8D537377B8D2129D46F64875DAEFF1C584A8523F79` |

The parent built the parser with zero warnings/errors and executed its nine pure
timeline cases successfully. The report is `AcceptedDiagnostic`: .NET10.0.12,
149,431 expected-process events, no foreign process events, loss, truncation,
timestamp regressions, invalid attempts or open attempts. Runtime rundown and
application allocation stacks were present. There are 1,329 complete four-phase
suspension attempts; their windows are not summed as GC pause time. The evidence
asserts natural termination and source/binary closure; no forced collection exit.

There are 11,433 allocation samples, weighted by `AllocationAmount64`, totaling
5,011,972,792 sampled bytes across the whole host. Byte arrays account for
2,620,527,528 weighted bytes (52.29%) and strings for 1,559,251,240 (31.11%).
No samples lack a stack; seven of 586,221 frames are unresolved, affecting seven
samples. These are sampled attribution weights, not exact allocated-object sizes,
per-operation totals or measured-phase counters.

The report displays the top 50 stack groups, keyed by the hash of each full stack,
but at most 24 frames per display. The following mutually exclusive groups use only
an owner method actually visible in those displays. Validation takes precedence
over an enclosing checkpoint frame. Ranks refer to the immutable accepted report.

| Visible owner path | Top-50 ranks | Samples | Weighted bytes |
| --- | --- | ---: | ---: |
| Stored payload compatibility through `RequiresSave` | 1,5,6,7,21,22,41 | 13 | 885,029,656 |
| `SaveExecutionRunDetailCoreAsync` durable-intent UTF8 encoding | 3,9,13,14,15 | 7 | 536,207,240 |
| `AgentToolJournalRecord.ValidateCore` | 28,30,43–50 | 13 | 266,486,648 |
| `AgentToolJournalTransitions.Equivalent` checkpoint equality | 10,11,12 | 6 | 248,537,168 |
| Existing-run target UTF8 publication | 19,20,34,36 | 6 | 212,869,192 |

Rank1, full-stack hash
`9A4A28191C774867B3EF50DACD4D07EE6E4D999EEA67B326FEEB8485CDAE7ED8`,
has `TranscodeHelper → JsonStore.Serialize → RequiresSave → HasSamePayload →
EnsureStoredPayloadIsTransitionCompatible → PersistExistingRunPayloadAsync`.
Its four allocation samples carry 283,825,920 weighted bytes. This identifies an
actual owner seam; it does not mean four invocations or predict that many saved
bytes after a change.

The top50 account for 3,596,548,624 weighted bytes. Of those, 1,227,101,448 cannot
be assigned to one of the owner seams from the displayed frames alone; several
show pooled serializer buffer growth. Do not assign those deeper hidden stacks to
A, C, durable publication or setup. Rank32 explicitly includes fixture creation,
so the trace demonstrably includes non-operation work. There are 104 heap-stat
observations; maximum reported heap/LOH sizes are 1,960,569,336/1,816,003,456 bytes.
Those samples include heap layout/fragmentation and establish neither retained
roots nor an exact lifetime peak. No physical I/O or native-allocation conclusion
follows from this trace.

## A: run-only prepared evidence

The narrow source perimeter is beneath
`src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/`:

| File and current seam | Minimal prospective edit |
| --- | --- |
| `FileSandboxWorkspaceJsonStore.cs`: `ReadJsonAsync`, `ReadSnapshotAsync`, `RequiresSave` | Add a narrow internal run-read result that binds freshly consumed complete bytes to a frozen `ExecutionRunRecord` and SHA256. Preserve safe-path/open/retry/BOM/JSON semantics. Add fresh streaming byte verification without retaining a second full byte array. Do not alter generic equality or expand snapshot-cache limits. |
| `FileSandboxWorkspaceExecutionSliceStore.cs`: `LoadRunDetailAsync`, `PersistExistingRunRecordAsync`, `PersistExistingRunPayloadAsync` | Carry optional owner-created run evidence with the initial detail read; use it only for the execution-run member of the immediate Prepared path. Session/index/child-file checks remain unchanged. |
| `FileSandboxWorkspaceStore.cs`: the two `UpdateExecutionRunDetailAsync` overloads, `SaveExecutionRunDetailCoreAsync`, `CommitExistingRunDetailJournalAsync` | Keep the evidence local to the actual held store gate/cross-process scope, bound to the exact previous run/path/owner and immediate prepared plan. Recovered journals and ordinary save entry paths without that evidence retain their existing validation. No public, serialized or callback-supplied witness. |
| `FileSandboxWorkspaceSnapshotCache.cs`: existing `Freeze` | Reuse its run-freezing logic only if a cohesive internal helper is smaller than duplicating the same one-line immutable copy. Do not add entries, change 8MiB/16-entry/64MiB cache bounds, or share evidence across calls. |

Correctness sequence:

1. Under the existing store and workspace ownership, complete pending-journal
   recovery as today. Freshly read the run and capture the digest of the exact full
   bytes consumed. Preserve full JSON validation and EOF behavior; the witness
   must describe the bytes decoded, including any BOM/whitespace, not a later
   reserialization. Do not introduce a new retained-file size limit in this patch.
2. Freeze the run **before** passing its containing detail to the update callback.
   Large reads currently bypass `SnapshotCache.Freeze`. `PendingApprovals` is an
   `IReadOnlyList` and may be a mutable decoded list. Its records contain immutable
   values; the journal already uses immutable arrays. A shallow run copy plus an
   immutable approval array costs O(approval count) references, without copying
   the entire proposal journal. The completed recursive inventory, including run
   init properties and runtime-context scope/attachment leaves, is in the source
   record; an interface name alone is not proof of immutability.
3. Build and validate the current complete persistence/intent plan as today.
   Evidence must match the exact immutable previous run used in that plan, the
   canonical path and this owner scope. A caller retaining a prior detail, another
   store/run, a recovered plan or a finished scope cannot supply reusable evidence.
   Prefer private data flow over a new general witness registry or transaction
   abstraction; no long-lived evidence field or public creation API is needed.
4. At `PersistExistingRunRecordAsync`, after intent persistence and before target
   publication, open the current run through the existing safe read policy and
   freshly hash all bytes with cancellation/EOF checks. Only exact byte equality
   permits substituting the immutable previous object for the current extra decode.
   The known previous/target revision and identity checks still apply. This saves
   the second decoded graph and its full stored/previous JSON equality, not the
   fresh read or all run validation.
5. A byte mismatch is not acceptance or rejection by itself. Use the existing full
   fresh decode and previous-or-target semantic compatibility path. Formatting-only
   changes, equivalent reordered properties and an already-published target retain
   current behavior; malformed/unexpected current content still refuses and retains
   the durable intent. Do not silently trust the old object, revision, size or mtime.
6. `RecoveredJournal` receives no ephemeral witness. It continues full intent,
   current run, indexes, previous/target and projection validation. All post-intent
   completion/cancellation and publication-recovery knowledge remains unchanged.

This targets the clearest directly attributed owner allocation path and avoids
some duplicate deserialization as a second consequence. SHA256 adds a bounded
streaming pass to the initial read and retains only a digest plus already needed
immutable objects. It adds CPU, while initial and verification I/O remain. It must
be measured; the selected 885MB sampled group is not a savings forecast. A changed
file may pay both hashing and fallback decoding, an explicit uncommon-path cost.
Cross-process coordination cannot exclude nonparticipating external mutation; the
fresh check preserves the existing optimistic check-before-publication boundary,
not an atomic filesystem compare-and-swap against external writers.

Do not skip the second **whole-plan** validation in A. `NormalizeRunDetail` still
constructs mutable child Lists, and a broad validated-plan witness would need an
additional complete alias audit. Nor does A change Core's fresh same-revision
checkpoint comparison: the separately visible 248MB group remains required until
an independent bounded-equivalence design is reviewed.

## C: exact journal-size validation, separately

`Models/Execution/AgentToolJournalModels.cs:ValidateCore` first validates identities,
versions, arrays and state, then uses default-options
`JsonSerializer.SerializeToUtf8Bytes(this).Length` to enforce 24MiB. Its
ConditionalWeakTable correctly skips repeat validation of the **same immutable
object**, but newly decoded journals allocate a full encoded result. The accepted
trace contains direct allocation stacks for this branch, including eight separate
displayed samples of 20,711,520 weighted bytes and serializer buffer growth.

The smallest C proposal is a local bounded counting Stream plus the same default
`JsonSerializer.Serialize(stream, this)` interpretation. Preserve all earlier
semantic validation and the exact inclusive 24MiB rule; only successful validation
may enter the existing weak cache. No Web-options substitution, custom JSON codec,
record projection, equality shortcut or new shared serialization framework.
The stream checks cumulative written bytes and rejects as soon as the limit is
crossed. This avoids retaining the complete final encoded byte array; it does not
avoid encoding/traversal, prove fixed serializer scratch allocation or guarantee a
small buffer for one large escaped string. Default serializer equivalence and
actual buffer behavior must be proven before choosing its implementation.

C is a smaller source change and can benefit fresh read-only detail too, but it
addresses a smaller clearly attributed group in this trace and leaves A's second
decode/equality path intact. Rank it second, unless A's immutable capture cannot
be established without widening owner/public contracts. Neither candidate alone
is evidence that the complete retained envelope will fit the reference memory
profile; durable-intent and target output buffers remain independently visible.

## Proof and release sequence

The authored source record now accounts for the new and retained cases; none has
been executed for A. A needs
real-file arrangements for unchanged bytes with one fewer run decode; callback
attempted mutation of approval collections; same-revision unexpected replacement;
formatting/BOM/property-order equivalence; already-target bytes; truncated/malformed
content; and a new owner recovering an interrupted intent with full validation.
No generic mutation of a private witness through reflection or production test
hook is needed. Observe actual initial/current byte reads and decode counts through
the existing diagnostics, not timing assertions or a mock saying it was fresh.

The existing `FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests` has
six Facts and one nine-row fixed theory, source-derived 15 rows. Its held-reader,
all nine publication boundaries, pre/post-intent cancellation, corrupt journal and
roll-forward assertions must continue to pass. Existing UTF8 byte-equivalence,
publication-knowledge, lease/checkpoint and full portability checks remain required.

C separately needs exact encoded limit-minus/exact/plus boundaries, escaping and
non-ASCII expansion, supported journal versions1–5, invalid-state rejection before
cache insertion, repeated validation identity behavior and the existing actual
commit/recovery suite. Compare counted bytes to the old default serializer as an
oracle in tests; do not derive expectations from the new counter itself.

After source review, parent serializes standard Release build, independently counted
discovery, focused correctness and full static validation. Then repeat the same
unprofiled workload/reference envelopes and retain every failed attempt/sample.
Re-establish complete same-result baselines before median/allocation claims. A new
sound whole-host trace may corroborate changed stacks, but neither sampled stack
weight nor forced GC substitutes for successful matched capacity. Do not enlarge
the envelope, hide failures, merge partial cohorts or bundle A+C before attribution.

Predecessor notes: current JsonStore and JournalModels match main hashes
`9A580CD4B357B814E079B5817C74C32EEE94AD6430839BD40AA5F3043DE4C985`
and `4C6B1BB44C041ADDBFF9866FC4866552CFE3F7832421A1AE717E0510D601D923`.
The managed Store/ExecutionSlice files already include the separately authored cold
historical graph, so their complete hashes differ from the profiled main files.
Before authoring A, preserve/coordinate that graph; do not overwrite either version
or treat a matching hot method as whole-file binary identity.
