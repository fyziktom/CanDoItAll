# Independent SB03 implementation review

Decision: **Pass for source and test design.** No actionable defect was found in the frozen diff. This is a separate review from the implementation owner. Candidate execution results and the final integrated/performance gate remain owned by the root and storage agent; this review does not claim tests still in progress have passed. No source/test edits, builds, tests, application requests or deployment were performed for this review.

## Reviewed boundary and evidence

Reviewed all three production diffs against `3d5def561`, the complete new nineteen-case test class, surrounding validation/comparison/durable writer code, and the approved `design-review.md`. Exact reviewed file hashes are in `independent-review-source-hashes.json`. `FileSandboxWorkspaceJsonStore.cs` is normalized-source identical to the baseline.

- The only immediate and recovered calls to private `CommitExistingRunDetailJournalAsync` explicitly pass `Prepared` and `RecoveredJournal`, respectively (`FileSandboxWorkspaceStore.cs:1003`, `:1136`). The new internal enum is transient control flow, not a journal field, request parameter, persisted trust marker or public API. There is no caller that derives this origin from untrusted persisted contents.
- Both journal-validation calls remain: before journal persistence and at the start of commit. The awaited persistence/callback boundary remains between them. Fresh workspace-index conflict validation, record-collection checks and all nine logical commit callbacks remain in their original order.
- The new private generic helper (`FileSandboxWorkspaceExecutionSliceStore.cs:1195`) is called only by session, run, execution-index and aggregate usage-index wrappers. It performs the same fresh typed read and the same previous/target transition compatibility checks, including missing-record rejection, before selecting the writer. No `ProviderUsageObservation` record enters this helper.
- For a prepared payload whose fresh typed serialization differs from the target, the existing atomic JSON writer is used. For every recovery call and every already-matching typed payload, the existing raw `WriteJsonIfChangedAsync` path is retained. Unrecognized internal enum values also take the conservative existing path; no new permissive default is introduced.
- The chat projection owner retains the complete expected-target rebuild, legacy-target compatibility check and fresh current-index validation before its equivalent final write choice (`FileSandboxWorkspaceChatProjectionStore.cs:542`). Its external session/latest-run dependencies remain in that owner.
- `WriteJsonAtomicallyAsync` still reaches `WriteObservedJsonAsync` and the existing durable writer. File/path/link validation, serialization, observation/history route and payload flush/atomic replacement code are unchanged by SB03. The provider history branch remains limited to actual `ProviderUsageObservation` payloads; aggregate usage is a separate type.
- No project reference, DI registration, schema, lock scope, public interface, runtime progress emission or unrelated record-directory optimization is added. The helper removes duplication within an existing owner without introducing a service layer or widening a public contract.

## Test-design assessment

The new test uses real temporary filesystem stores and the existing physical JSON read diagnostic port. Capturing begins after the durable journal boundary, so setup/admission reads do not masquerade as commit reads.

- Two changed-progress cases, with 1 and 32 existing logs, assert exactly five retained deserialization/conflict reads and zero corresponding raw comparison reads. They also verify a single added logical log, unchanged chat messages, byte-identical individual provider usage record, unchanged usage totals/count and consistent aggregate/index revisions. The implementation owner retained their old-code failures (five raw comparison reads) separately.
- Six canonicalization cases install the typed target at the journal callback for both Run and ChatIndex, with canonical, compact and unknown-property JSON. Each requires the raw comparison path and exact canonical final bytes. This covers both the shared helper and separate chat branch.
- Ten conflict cases cover missing and unrelated edited data for all five optimized payload paths. They require explicit failure and retained pending journal; missing targets remain missing.
- The recovery case faults after journal persistence, uses a new store and requires all five raw comparisons, one roll-forward and no duplicate logical log on reload.
- Existing recovery tests supply nine commit fault boundaries, cancellation before/after durable admission, corrupt journal, catalog mutation recovery and second-store lock behavior. Existing writer/path tests remain necessary for actual link/path safety; the new read-count fixture is not presented as a replacement for those tests.

The cases are bounded and behavior-oriented. Test-only JSON field names are fixture wire-format mutations, not a new stringly typed production protocol. No test uses a live provider or application profile.

## Performance and concurrency limits retained

The extra typed comparison serializes current and target payloads; this trades CPU/allocation work for avoided disk reads and their path-policy overhead. Physical read counts alone do not prove a net latency improvement. Root's final host measurements must establish the actual effect.

As acknowledged in the accepted design, a noncooperating writer that installs the exact target after the fresh validation can cause an additional identical atomic write compared with the old second-read skip. Final canonical payload and logical commit stage behavior remain equivalent in this case; identical physical write counts under this external race are not claimed. The code does not reuse a stale observation to skip a write when the typed payload already matches, so the previously identified canonicalization/stale-raw hazard remains avoided.

This pass is invalidated by changes to the hashed production/test files, the compared serializer/durable writer semantics or caller/origin topology. Final execution, recovery, full-suite decision and both-host UI/performance acceptance remain separate gates.
