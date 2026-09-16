# MOD-STRUCTURE — Project Structure and Workbench: native graph plus projections

**Target owner:** Project Structure; Work Management; Workbench shell (separate responsibilities)

Structure owns native notes/nodes, structural relationships, placements and bindings. Work Management within this area owns tasks, plans and assignments. Agent, project-hierarchy and execution projections do not create new source authorities. The shell owns presentation only.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Workbench`

**Primary sources:** SRC-008, SRC-004, SRC-005, SRC-025, SRC-026, MAP-02, MAP-03, MAP-04

## Provided responsibilities

- Authorized Structure reads, native node/link commands and typed safe contribution protocol.
- Task/Gantt/planning commands through Work Management; versioned asset binding and content coordination.
- Invocation context, file scopes, a projection extension contract and execution-result attachment.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Projects existence/hierarchy; Agents identity; CRM resource facts; Processes/Workflows definitions and execution records.
- Provider quotes and image generation through Agents; Security secret references; Storage/FileTools; Prompts, Resources and TestLab contributions.

## Forbidden shortcuts

- Neither a projection-only cache nor a universal owner of every domain.
- Do not create native nodes as a side effect of ordinary projection reads or rebuilds.
- Do not calculate agent prices locally or write process runtime rows directly.
- Task/subtype and system-owner flags are not arbitrary JSON switches.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: typed safe creation/ensure from modules, run-correlated writeback and durable idempotency.
- REQUIRED FOR THE BOUNDARY: separate pure queries from the existing GetStatus-to-ApplyStatus path without losing refresh.
- DOCUMENTED FOLLOW-UP: content-version editing, capability-specific asset actions, typed relationships and storage-to-database compensation; verify current state.
- ESTIMATE: deferred generation recovery after restart; the queue read during preparation does not provide this by itself.

## Persistence

Separate Workbench_ProjectObjects/Links, bindings/references, lifecycle, layout, leases and mutation receipts logically by authority, not through mass identity changes. Native note bodies are canonical local content. Managed file content belongs to the storage object; Structure owns its use and binding.

## Recorded capabilities

- **FEAT-032** [USER; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] User-confirmed native notes and relations exist only in Project Structure.
- **FEAT-033** [CODE; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Gateway operations for reads, node/asset creation, leases, parent/root validation, and media validation.
- **FEAT-034** [CODE; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workflow nodes retain definition/version references, launch, status, and results.
- **FEAT-035** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Invocation snapshots have bounded coverage, profile, and freshness with explicit source selection and no silent fallback.
- **FEAT-036** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Managed text, JSON, Markdown, Mermaid, and log files; file-node notes are not the file body.
- **FEAT-037** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Run folders and FileTools use trusted desktop execution where supported.
- **FEAT-038** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Deletion distinguishes retaining files from deleting owned files, including multiple roots and partial outcomes.
- **FEAT-039** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Typed runtime requests for direct process start with optional terminal and elevation behavior.
- **FEAT-040** [CODE; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Deferred image generation uses a bounded in-memory channel and later media/state updates.
- **FEAT-041** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Canonical task creation/editing, resources, estimates, prices, checklists, dependencies, and Gantt operations.
- **FEAT-042** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Canvas, Gantt, inspector, selection/layout, clipboard/subtree, and subproject dialogs.
- **FEAT-043** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Process launch, subprocesses, run projections, roots, and reports.
- **FEAT-044** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Meeting, recording, transcript, participant, repository, infrastructure, and secret-reference nodes have typed metadata and a registry.
- **FEAT-045** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Plan analytics, manager summaries, and Memory durable records.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** caller.
- **CON-010 — CRM party/resource catalog:** caller.
- **CON-014 — Project participation:** caller.
- **CON-015 — Project lookup:** caller.
- **CON-016 — Project lifecycle orchestration:** caller.
- **CON-017 — Project hierarchy:** caller.
- **CON-018 — Structure read / invocation read:** declaration owner, implementation/integration boundary.
- **CON-019 — Native structure authoring:** declaration owner, implementation/integration boundary.
- **CON-020 — Structural links:** declaration owner, implementation/integration boundary.
- **CON-021 — Ensure structure contribution:** declaration owner, implementation/integration boundary.
- **CON-022 — Asset binding and content lifecycle:** declaration owner, implementation/integration boundary.
- **CON-023 — Canonical work-item commands:** caller.
- **CON-024 — Work assignment:** caller.
- **CON-025 — Gantt and dependency mutations:** caller.
- **CON-026 — Plan estimate/baseline commit:** caller.
- **CON-027 — Process launch:** caller.
- **CON-028 — Process run query/control:** caller.
- **CON-029 — Workflow catalog/version lookup:** caller.
- **CON-030 — Workflow launch/control:** caller.
- **CON-031 — Process run result publication:** caller.
- **CON-032 — Read-only structure contribution adapter:** declaration owner, caller.
- **CON-033 — Prompt search/version retrieval:** caller.
- **CON-035 — Resource catalog and promotion:** caller.
- **CON-036 — TestLab plan/run/evidence:** caller.
- **CON-038 — Memory operation API:** caller.
- **CON-039 — Memory source snapshot publication:** implementation/integration boundary, extension adapter.
- **CON-043 — Managed storage lifecycle:** caller.
- **CON-044 — Context surface capture:** implementation/integration boundary, extension adapter.
- **CON-045 — Post-commit invalidation/live progress:** caller.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-047 — Workspace preference API:** caller.
- **CON-048 — Database profile and host capability control:** caller.
- **CON-050 — Collaboration thread/message:** caller.
- **CON-051 — Execution source authority resolver:** implementation/integration boundary, extension adapter.
- **CON-052 — Authorized file browsing/opening:** caller.
- **CON-054 — Agent run result publication:** caller.
- **CON-055 — Workflow run result publication:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-001, QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, QA-008, QA-009, QA-010, QA-013, QA-014, QA-015, QA-016, QA-017, QA-018, QA-019, QA-020, QA-021, QA-022, QA-023, QA-024, QA-025, QA-026, QA-027, QA-028, QA-029, QA-030, QA-031, QA-032, QA-034, QA-035, QA-036, QA-037, QA-038, QA-041, QA-043, QA-044, QA-045, QA-046, QA-047, QA-061, QA-065, QA-070, QA-074, QA-075, QA-076, QA-077, QA-078, QA-079, QA-080, QA-081, QA-082, QA-083, QA-085, QA-086, QA-087, QA-088, QA-089, QA-091, QA-093, QA-094, QA-095, QA-097, QA-098, QA-099, QA-101, QA-103, QA-104, QA-107, QA-108, QA-113, QA-117, QA-118, QA-119, QA-120, QA-121.

Related gaps: GAP-001, GAP-002, GAP-003, GAP-004, GAP-005, GAP-006, GAP-007, GAP-008, GAP-019, GAP-023, GAP-026, GAP-029, GAP-030, GAP-031, GAP-032.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
