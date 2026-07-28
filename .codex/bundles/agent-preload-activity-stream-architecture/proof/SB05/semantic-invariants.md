# SB05 Governed Semantic Invariants

SB05 is a validation-only subbundle. It changes no production or test source. The
failing-first boundary is therefore the SB01 baseline and the prior backend
subbundles; SB05 does not manufacture a new red transcript after the implementation
already exists.

## SB05-PERF-001 — Immediate truthful activity

- Invariant ID: `SB05-PERF-001`
- Source raw note: the UI must not appear frozen while the agent captures and prepares
  data; backend improvement must be measured before UI work.
- Expected behavior: one typed `Accepted` activity is published before catalog,
  provider, persistence, or runtime entry.
- Disallowed shallow implementation: change spinner text or seed an activity record in
  a test after catalog loading.
- Failing-first test: `bundle://proof/SB01/startup-baseline.md` records that no
  earlier typed activity existed.
- Passing test: `bundle://proof/SB05/startup-raw.md` and the activity 11/11 handoff.
- Changed source files: N/A — SB05 is a validation-only subbundle and changed no
  production or test source; reviewed identities are in
  `bundle://proof/SB05/transcripts/source-hashes.md`.
- Production assertions: coordinator admission publishes the production activity;
  current-profile dispatch requires the operation lease.
- Red-team negative case: block cold workspace/catalog resolution; the accepted activity
  must remain readable and the operation must terminalize on failure.
- Downstream dependency check: SB06 may render only this typed stream, not infer
  progress from selected-run state.

## SB05-PERF-002 — Duplicate startup work is removed

- Invariant ID: `SB05-PERF-002`
- Source raw note: use already-loaded immutable state and safe initialization so agent
  startup becomes materially better.
- Expected behavior: the final path records exactly
  `Accepted1/CatalogLoad0/CatalogSnapshot1/ProviderGet0/ProviderAcquire1/
  ProviderCapture3/SessionGet0/SummaryList0/AtomicStart1/DetailGet0/DetailSave0/
  DetailUpdate1`.
- Disallowed shallow implementation: rename counters, cache live runtime objects, or
  omit a required durable update to make counts look lower.
- Failing-first test: SB01 operation counts in
  `bundle://proof/SB05/operation-counts.md`.
- Passing test: all 20 final startup executions report the same invariant row.
- Changed source files: N/A — SB05 is a validation-only subbundle and changed no
  production or test source; reviewed identities are in
  `bundle://proof/SB05/transcripts/source-hashes.md`.
- Production assertions: immutable catalog/provider snapshots plus one atomic
  chat-backed start; live agents, credentials, tools, sessions, approvals, and
  `DbContext` are excluded from reusable state.
- Red-team negative case: cold/warm and new/existing cases must keep the same count
  contract.
- Downstream dependency check: UI timing may be evaluated only after this backend
  count gate.

## SB05-EF-003 — Bounded query shape without shared-context parallelism

- Invariant ID: `SB05-EF-003`
- Source raw note: read-only parallelism is allowed only when dependencies are
  independent; a shared `DbContext` must not be used concurrently.
- Expected behavior: warm provider validation emits one scalar SQL command; synthetic
  validation emits zero; changed-provider refresh emits three bounded commands.
  Process enrichment selects 10 runs and batches state/assignment reads.
- Disallowed shallow implementation: one query per provider/run, tracked entity
  materialization for scalar validation, or `Task.WhenAll` over scoped process stores.
- Failing-first test: SB01 query review names the provider/process proof still
  required at A5.
- Passing test: `bundle://proof/SB05/ef-query-proof.md`.
- Changed source files: N/A — SB05 is a validation-only subbundle and changed no
  production or test source; reviewed identities are in
  `bundle://proof/SB05/transcripts/source-hashes.md`.
- Production assertions: factory-created provider contexts; `AsNoTracking`; keyed
  scalar projection; `AsSplitQuery`; bounded batch APIs.
- Red-team negative case: six selected runs still produce one state-batch and one
  assignment-batch call, not six of each.
- Downstream dependency check: no UI optimization may introduce shared-context
  overlap.

## SB05-STORE-004 — Atomic recovery and history-independent admission

- Invariant ID: `SB05-STORE-004`
- Source raw note: avoid bottlenecks and cross-thread/store corruption while preserving
  canonical run state.
- Expected behavior: typed WAL recovery rolls forward exactly once after every injected
  commit-stage failure; admission physical-open count does not grow from 4 to 96 runs.
- Disallowed shallow implementation: scan all historical payloads, delete a corrupt
  journal, or treat file replacement alone as proof of power-loss durability.
- Failing-first test: SB01 baseline identified duplicate/full-scan storage work.
- Passing test: generic 6/6, combined 33/33, storage 10/10, and 11/15 physical opens.
- Changed source files: N/A — SB05 is a validation-only subbundle and changed no
  production or test source; reviewed identities are in
  `bundle://proof/SB05/transcripts/source-hashes.md`.
- Production assertions: journal-before-commit, preflight, idempotent commit stages,
  chat index, latest-run header, and delta usage projection.
- Red-team negative case: corrupt journals fail explicitly and remain; cancellation
  before journal persists nothing; cancellation after journal completes committed
  recovery.
- Downstream dependency check: later UI/reload flows may trust canonical state but
  must not claim physical-media durability.

## SB05-ARCH-005 — Architecture remains aligned

- Invariant ID: `SB05-ARCH-005`
- Source raw note: review the architecture so general module behavior and
  source-of-truth boundaries are not broken.
- Expected behavior: project dependencies remain acyclic and point inward; snapshots
  are projections, not canonical write sources; runtime resources remain per run.
- Disallowed shallow implementation: new reverse project reference, service locator,
  partial-file expansion, cached live runtime, or snapshot write-back.
- Failing-first test: prepared boundary/dependency maps and SB01 snapshot.
- Passing test: CodeAnalytics snapshot `snap-20260727233256-654bc9d9` plus
  `bundle://proof/SB05/architecture-review.md`.
- Changed source files: N/A — SB05 is a validation-only subbundle and changed no
  production or test source; reviewed identities are in
  `bundle://proof/SB05/transcripts/source-hashes.md`.
- Production assertions: direct project inventory, source assertions, construction and
  lifetime review.
- Red-team negative case: reported module/nested-type cycles remain disclosed and are
  not falsely presented as a cycle-free result.
- Downstream dependency check: SB06 may proceed only under
  `bundle://proof/SB05/a5-decision.md`.

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| typed `Accepted` activity | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs` | scoped reader/current-profile activity path | operation lease binds and terminalizes exactly once | activity admission/profile isolation 11/11 |
| provider immutable lease | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` | execution preparation/provider acquisition | revision probe, publication generation, profile-switch invalidation | changed/delete/fault/superseded refresh tests |
| chat/generic/update WAL journal | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs` | store recovery before later reads/mutations | journal persisted, stages roll forward, journal removed after commit | generic 6/6 and combined 33/33 |

## Semantic decision

Pass with the three P2 limitations in
`bundle://proof/SB05/concurrency-invariants.md`. The positive cases exercise
production producers and the negative cases reject the identified shallow passes.
