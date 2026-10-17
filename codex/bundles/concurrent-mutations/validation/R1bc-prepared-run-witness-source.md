# Prepared run witness: source candidate A

Status: authored, source reviewed by the author, not built or executed. Independent
peer review is pending. The parent owns transfer, standard Release builds, counted
discovery, focused execution, full static enforcement and matched performance.
The [accepted trace decision](../plan/R1bc-accepted-trace-next-owner-change.md)
remains diagnostic evidence, not a capacity result.

## Isolation and scope

Candidate root in the managed checkout:
`.artifacts/concurrent-mutations/candidates/run-prepared-witness/`.
Ownership was recorded before creation in
`.artifacts/concurrent-mutations/run-prepared-witness-ownership.json`
(`973D12E3D75253AEB52DA391C45220484032A86C92B431311473053F56474200`).
Each scratch path mirrors its eventual repository target. The source inventory
records exact main predecessor and candidate hashes. No ordinary managed source,
main source, historical reader, C3 code, test host or database was changed.

Four production inputs belong to AgentFramework.Persistence:

- `Storage/FileSandboxWorkspaceJsonStore.cs`: a private typed capture delegate
  binds the initial complete decode to its exact byte digest; fresh verification
  has the separate `PreparedRunValidation` diagnostic kind.
- `Storage/FileSandboxWorkspaceRunReadSnapshot.cs`: one internal run snapshot and
  a leave-open, forward-only hashing stream; neither is public or serialized.
- `Storage/FileSandboxWorkspaceExecutionSliceStore.cs`: an update-only detail read
  and exact-path/previous-reference/Prepared gate before run publication.
- `Storage/FileSandboxWorkspaceStore.cs`: both update callbacks carry only their
  invocation-local snapshot through the existing private save/commit chain.

The three changed files were copied from **main**, not from the managed historical
graph. Parent transfer must compare their main predecessor hashes. Later historical
integration needs an explicit merge preserving both changes; copying the old managed
Store/ExecutionSlice files over this candidate would discard this optimization.

## Immutable graph and byte proof

The source audit includes every `ExecutionRunRecord` constructor and init property:

| Reachable state | Immutability conclusion |
| --- | --- |
| IDs, timestamps, enums, booleans, revisions, provider/failure fields and structured-output/metadata/session strings | Values or immutable strings. `InitialActivityOperationId` is a value ID. |
| `PendingApprovals` | The sole mutable collection contract on this graph; copy to `ImmutableArray` before callback exposure, including runs above the cache threshold. Each `PendingToolApprovalRecord` is sealed with string leaves and optional immutable approval binding. |
| `HistoryCaller` | Sealed record with enum, nullable value credential ID and strings. |
| `EntryAgentRequestCompatibilityEvidence` | Sealed record containing enums, nullable value IDs and strings. |
| `ToolAdmission.Session` | Sealed admission/reference/profile/background records with values and strings; no mutable owner or authority service reference. |
| Journal segments, batches, proposals, provider dispatches and approval bindings | ImmutableArray containers; sealed records; value identities, states and digests, strings and immutable protocol envelopes. Nested segment approvals and provider approved-call arrays are immutable too. |
| Journal original/background input, cursor, cancellation and owner receipt | Sealed records/value wrappers/string envelopes; no mutable result DTO. |
| Runtime context and typed attachments | ImmutableArray attachments with sealed context/source/envelope records and value metadata. `WorkspaceScopeDescriptor` has init-only enum/string state; its instance path lists are freshly computed, not retained aliases. Static root-name lists are not serialized instance state. |

`with` creates another record; it does not mutate the captured instance. No claim is
made against reflection, unsafe mutation or a future mutable field added without
review. Snapshot reuse does not expand the existing cross-request cache or its
8 MiB / 16-entry / 64 MiB limits. It retains an already-needed run plus immutable
approval references, a canonical path and a 64-character digest for this invocation.

Small initial reads keep the existing complete-byte cache validation and JSON/BOM
interpretation. The digest covers those same bytes, including ignored unknown
properties and whitespace. Large reads hash the exact bytes returned through the
same shared handle to `JsonSerializer.DeserializeAsync<T>`. The wrapper supports
array, Span, Memory/async and inherited ReadByte paths, prohibits seeking and requires
a positive-buffer EOF read plus unchanged observed length. The decoder must succeed;
there is no unvalidated drain, reopen-to-hash or metadata-only witness.

The file share remains `ReadWrite | Delete`; it does not exclude external writes.
As before, cooperating store/workspace gates exclude participating writers, and
external mutation is checked optimistically before publication. This is not an
atomic filesystem CAS against arbitrary external writers.

After complete existing plan validation and durable intent persistence, only
Prepared origin + exact canonical path + `ReferenceEquals` to the immutable previous
run permits fresh streaming SHA comparison. `NormalizeRunDetail` preserves that
run reference. Exact byte equality substitutes the captured original for the extra
current decode and stored/previous JSON comparison. It still compares previous to
target and preserves canonical target publication.

Mismatch or missing current file takes the existing full fresh decode and
previous-or-target validation. Formatting/property order/BOM/unknown-field changes
can remain semantically acceptable; an already-written target is canonicalized;
unexpected, truncated or trailing payload is rejected with the intent retained.
I/O and cancellation are exceptions, not byte mismatches. Existing transient read
retry, safe paths and stream disposal remain. A retry ending in missing/null data
does not return an earlier captured snapshot.

Ordinary Save and RecoveredJournal receive no witness. Full plan validation,
session/index/child checks, semantic recovery, commit stages and post-intent
cancellation behavior are unchanged. No new public/store interface, registration,
package, schema, journal format or source-authority rule is introduced.

## Source-derived proof selection

The candidate inventory supplies exact project/filter/source identities and counts.
These are source expectations, not discovery or execution results. All selected
rows use ordinary Facts or fixed InlineData; no dynamic data, skips or excluded
categories occur. The two cost Facts are selected by exact method name in their
existing partial class; the rest of that class is not implicitly counted.

| Disjoint lane | Expected discovery and runtime | Derivation |
| --- | ---: | --- |
| New `FileSandboxWorkspaceRunWitnessTests` | 3 | Three Facts: all stream read forms/EOF/seek; actual >8 MiB BOM decode, cancel and I/O refusal; collection freeze and exact owner path/reference binding. |
| New `FileSandboxWorkspacePreparedRunWitnessTests` | 11 | Three Facts + fixed theories 2 + 3 + 3. Both update overloads exceed 8 MiB; same-size/revision/mtime tampering and fresh-owner recovery; three equivalent encodings; already-target canonicalization; missing/truncated/trailing bytes; ordinary Save full validation. |
| `FileSandboxWorkspacePreparedCommitReadIntegrationTests` | 19 | One Fact + fixed theories 2 + 6 + 10. Only the two unchanged-current Run assertions and diagnostic summary now name hash validation; six already-target/canonicalization and ten mutation/deletion cases remain. |
| `FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests` | 15 | Six Facts + nine commit-stage cases, unchanged source. |
| Existing actual receipt cost methods | 2 | `Pinned_claims_and_batched_checkpoint_bound_full_run_decodes_independent_of_candidate_count` and `Maximum_journal_slice_bounds_evidence_response_bytes_and_records_real_store_cost`. Four fresh run reads and two durable intents stay exact; maximum-shape expected full decodes changes from four to two. |
| Prior UTF8 Unit compatibility selection | 47 | JsonWrite 2; JsonBatch 3 Facts + 2 inline = 5; DurableFileWriter 15 + 2 = 17; DurableFilePublication 4 + 4 = 8; FileProviderUsageWindow 7 + 8 = 15. All unchanged. |
| Prior UTF8 owner integration selection | 15 | `FileSandboxWorkspaceUsageProjectionIntegrationTests`: seven Facts + eight inline rows, unchanged. |

Total **112 distinct correctness cases: 50 Unit + 62 Integration**. The original
62 UTF8 compatibility cases do not overlap the 19 prepared-read or 15 existing-run
recovery rows. They are retained as regression coverage, not added twice. The two
newly added test files have **14 rows**, not 14 successful tests.

The unchanged **22 opt-in LongRunning performance cases** remain a separate matrix,
with the same reference envelope and fixture/host isolation. The predecessor
selection manifest and every harness input are hashed in the candidate inventory.
Parent can reuse its closed UTF8 candidate as the immediately preceding experiment;
failed/incomplete cohorts still cannot supply seven-sample medians or capacity.
This source change predicts fewer decodes, not a numerical time/allocation saving.
Fresh verification still reads the complete run; mismatch now pays hashing plus
existing decode. Real CPU, memory, GC, contention and complete matrix outcomes must
determine whether A remains acceptable. No extra trace is required before the first
matched experiment, and no forced GC or larger reference envelope is proposed.
