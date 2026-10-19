# Atomic Process generation persistence

The [storage design](../plan/P07-process-generation-persistence.md) is implemented and
passes **101/101** selected cases: 31 new PostgreSQL storage cases, 29 pure lifecycle
cases, 21 existing accounting cases, two admission cases and 18 execution-authority cases.
This closes the internal durable generation increment. It does not activate governed
Process/Workflow execution, verify actual child journals, or close R5.

## Result

Normalized heads, generation history and child records commit through the existing
runtime UOW/root transaction. Enrollment requires the same child's reservation; closure
requires its exact allocation settlement and owner revision. Standalone settlement cannot
release enrolled work. Material capture/refresh has separate retained allocations, and an
automatic refresh consumes one original policy replan. No generation resets the ledger.
Review/quiescence/terminal transitions update the runtime step and release its claim in
the same commit. Terminal/refresh transitions verify the complete bounded child set.

Strict, canonical, versioned command receipts preserve original results independently of
the current runtime state. Tests prove restart, lost acknowledgement, changed-intent
refusal, rollback after SQL flush, racing enrollment, unrelated-root progress, capture and
child identity reuse refusal, stale/foreign bindings, corrupted receipts/counters, missing
generation, budget overrun after cancellation and actual migration up/down. Query-shape
assertions require indexed current-generation and exact-child reads without event-history
scans. These are correctness/query-shape tests, not engine-wide throughput qualification.

Migration `20261019185011_AddProcessDecisionGenerations` creates empty normalized tables
and nullable receipt storage. It does not fabricate historical decisions. Downgrade locks
affected tables and refuses retained evidence. The earlier budget migration round-trip
test now removes/restores its dependent generation schema in the same test transaction.

## Validation and architecture gate

Runtime, Persistence, Web/migration, Integration and Unit Release builds passed with
refreshed standard outputs. Discovery confirmed 29, 31, 21, two and 18 cases before the
single frozen execution. EF reports no pending model change. Rendering-register,
documentation and diff gates passed; final doc-only closure checks are retained separately.

The initial full portability scan reported four new matches in the generated designer:
the existing `RootPathState`, `RootPathSyntax`, `RaisesEscalation` and `EscalationCount`
column metadata. Each excerpt was inspected; none adds a path assumption, OS operation or
process launch. Only those four reviewed allowances were added, and the diff was inspected.
Final enforcement without `--write-baseline` passes at **15,623** findings. The initial
failure remains in the evidence. Historical Stable failures have not been reclassified;
this focused increment did not trigger another broad run.

C# architecture gate: **Pass for durable internal owner integration; governed execution
follow-up required.** Snapshot `snap-20261019185941-d1ce2c09` covers the same four Process
projects as `snap-20261019183806-d1ce2c09`: 999 types, 8,997 members, 27 entities, 328
findings, 190 existing workspace diagnostics and zero cycles. Five new Info member-count
findings were reviewed: the cohesive 313-line context-bound store; two nine-field rows;
the strict receipt codec and its private wire document. No project reference, runtime
partial, service locator, transaction owner or provider dependency was added. The UOW
only delegates to the collaborator; pure lifecycle and receipt handling remain separate.
Actual builds, not the workspace diagnostics, establish compilation.

Source identity: 9,449 files,
`ADF2A25844AE49B0C80951607331B2D631171B7AE4D6CB133B3B45E7789F26D7`.
Standard runtime identity: 5,004 files,
`1D5A6241F6DC815769667C715F426581C7E2B83533D038A7377AA82342D3A89B`.
Both remained unchanged through execution and were reverified before freeze release.
The 311.5859325-second observer covered every complete test command. Reconciliation found
zero remaining observed processes and zero database-name delta; 17 unattributed TEMP
entries were preserved. Earlier cleanup-review denials were not retried or bypassed.
The existing isolated PostgreSQL18 fixture remains needed for continuing work.

Candidate: `.artifacts/concurrent-mutations/validation/c4-process-generation-persistence`.
Compact proof: `proof/2026-10-19-planning-authority/c4-process-generation-persistence.json`,
SHA-256 `265DACB7B5C2B29BED3D43E96E8B40EBB7704D1B4020FEBAFC6D3F69824B5548`.

## Remaining integration

Existing policy-bearing execution/effect-owner refusals remain. Application must verify
actual Agent/Workflow closure evidence outside SQL, and effect adapters must validate
the enrolled generation while holding the root fence through publication. The producer,
execution-version handshake, generic runtime-writer rules, inherited material/budgets,
semantic source occurrences and durable manager continuation remain open. Agent background
reservation currently enumerates workspace run history before filtering source lineage;
its indexed recovery design and capacity proof are required before the new path is enabled.
