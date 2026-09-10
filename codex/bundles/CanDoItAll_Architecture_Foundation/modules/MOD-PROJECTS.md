# MOD-PROJECTS — Projects: portfolio and lifecycle

**Target owner:** Projects

Projects, phases, lifecycle, portfolio and project hierarchy. A project is not the entire Project Structure graph. Projects coordinates its lifecycle and invites owner participants; it must not delete their tables itself.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Projects`

**Primary sources:** SRC-011, MAP-01

## Provided responsibilities

- Project catalog, authorized existence/scope queries, phases and lifecycle commands.
- Project hierarchy and its changes; projection contributions to Structure.
- Export, deletion and transfer lifecycle coordination with owner participants.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Structure/Work Management: structural content, tasks and workload summaries.
- CRM: party summaries and project participation.
- Storage/FileTools: file portfolios under authorized scopes.

## Forbidden shortcuts

- Do not own technical runtime statuses or claim foreign files by path.
- Do not replace bridges with a direct Projects-to-Workbench.UI reference.
- Do not introduce a second ProjectId for the graph.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: a clean project query contract instead of every consumer listing all projects through concrete ProjectsService.
- ESTIMATE: portfolio baselines and economic summaries; historical planning snapshots are not live price lists.

## Persistence

The Projects runtime context maps owned project tables. Decide cross-domain foreign keys and lifecycle transactions explicitly. Deletion retains durable coordination and a blocked/deleting state preventing new writes.

## Recorded capabilities

- **FEAT-025** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Projects, portfolios, phases, options, hierarchy, parties, and files.
- **FEAT-026** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Canonical Project Structure nodes and mutations in Workbench are consumed through a Projects bridge.
- **FEAT-027** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Hierarchy summaries, deterministic queries, recent activity, and access checks.
- **FEAT-028** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Coordinated project deletion with participant effects, receipts, and cleanup.
- **FEAT-029** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Export/import manifests, transfer participants, files, and portfolios.
- **FEAT-030** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] ProjectParty and WorkAssignment bridge transaction coupling.
- **FEAT-031** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Project-sourced agent-chat context.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-010 — CRM party/resource catalog:** caller.
- **CON-011 — CRM domain commands:** caller.
- **CON-014 — Project participation:** caller.
- **CON-015 — Project lookup:** declaration owner, implementation/integration boundary.
- **CON-016 — Project lifecycle orchestration:** declaration owner, implementation/integration boundary.
- **CON-017 — Project hierarchy:** declaration owner, implementation/integration boundary.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-039 — Memory source snapshot publication:** implementation/integration boundary, extension adapter.
- **CON-044 — Context surface capture:** implementation/integration boundary, extension adapter.
- **CON-046 — Project lifecycle participant protocol:** declaration owner, caller.
- **CON-050 — Collaboration thread/message:** caller.
- **CON-051 — Execution source authority resolver:** implementation/integration boundary, extension adapter.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-022, QA-025, QA-046, QA-059, QA-076, QA-077, QA-083, QA-099, QA-107, QA-108, QA-113, QA-118.

Related gaps: GAP-010, GAP-022, GAP-030.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
