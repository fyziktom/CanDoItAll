# MOD-PROCESSES — Processes: definitions and authoritative execution

**Target owner:** Processes

Process definitions, instances, step orchestration, run assignments, approvals and recovery. A task assignment is a work plan; ProcessStepAssignment is a run execution binding. Process runtime remains authoritative over its result.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Processes`

**Primary sources:** SRC-012, SRC-003, MAP-09

## Provided responsibilities

- Definition/version and run queries; start, cancel, resume, approval and recovery according to process policy.
- Execution outcomes, artifact manifests and result contributions with stable lineage.
- Subprocess start ports and source-authority descriptors.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Agents execution/catalog; Workflows execution; Plugins driver/executor capabilities.
- Project/Structure context through contracts; CRM resource summaries; Storage for run-owned artifacts.
- Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

## Forbidden shortcuts

- Do not launch through Agents UI or a concrete workspace factory.
- Structure must not mark ProcessRun Completed directly.
- Do not invent a universal ProcessAgentRuntimeToolProvider by relabeling HTTP endpoints.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: a narrow execution port and atomic launch admission with durable origin binding.
- REQUIRED FOR THE BOUNDARY: distinguish process results, artifact-attachment state and task acceptance.
- UNPROVEN: every live-driver and human-in-the-loop scenario; a mock pass is not production proof.

## Persistence

Owned process persistence already exists. Do not prematurely split all Process Core again. Limit foreign mappings while preserving run history, transitions, idempotency, approval/recovery evidence and source generation.

## Recorded capabilities

- **FEAT-046** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Process definitions, launch, assignment, observation, recovery, and project context.
- **FEAT-047** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Processes use HTTP, governed execution, and Project Structure bridges; no universal direct runtime process tool is established.
- **FEAT-048** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Agent and Workflow execution adapters implement process-step execution.
- **FEAT-049** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Launch resolution, variable preparation, readiness, and source authority.
- **FEAT-050** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Subprocess parentage, artifact roots, policy, and outputs.
- **FEAT-051** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Process run-record search, history, summary, and graph UI require backend coverage rebaselining.
- **FEAT-052** [USER; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] User-confirmed process launch from Project Structure and result writeback.
- **FEAT-117** [DOC; DOCUMENTED_SURFACE_LIMIT] No general first-party Process tool provider is documented; existing process UI/HTTP/governed/Structure paths remain separate.
- **FEAT-119** [USER; TARGET_PROTOCOL_NOT_BLANKET_DELIVERY] Process steps can use authorized target-owner operations beyond Structure with durable per-step receipts.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** caller.
- **CON-003 — Governed agent execution:** caller.
- **CON-004 — Agent approval / cancellation / status:** caller.
- **CON-005 — AI workload cost quote:** caller.
- **CON-009 — Usage evidence and allocation query:** caller.
- **CON-010 — CRM party/resource catalog:** caller.
- **CON-013 — Capacity reservation:** caller.
- **CON-015 — Project lookup:** caller.
- **CON-018 — Structure read / invocation read:** caller.
- **CON-019 — Native structure authoring:** caller.
- **CON-020 — Structural links:** caller.
- **CON-021 — Ensure structure contribution:** caller.
- **CON-022 — Asset binding and content lifecycle:** caller.
- **CON-027 — Process launch:** declaration owner, implementation/integration boundary.
- **CON-028 — Process run query/control:** declaration owner, implementation/integration boundary.
- **CON-029 — Workflow catalog/version lookup:** caller.
- **CON-030 — Workflow launch/control:** caller.
- **CON-031 — Process run result publication:** declaration owner, implementation/integration boundary.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-036 — TestLab plan/run/evidence:** caller.
- **CON-037 — Schedule plan and fire dispatch:** caller.
- **CON-043 — Managed storage lifecycle:** caller.
- **CON-044 — Context surface capture:** implementation/integration boundary, extension adapter.
- **CON-045 — Post-commit invalidation/live progress:** caller.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-051 — Execution source authority resolver:** caller, implementation/integration boundary, extension adapter.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-057 — Automation-safe CRM search and identity resolution:** caller.
- **CON-058 — Bounded CRM party/contact creation:** caller.
- **CON-059 — CRM affiliation read and mutation:** caller.
- **CON-060 — Staffing/resource-profile administration:** caller.
- **CON-061 — Simple Chats definition discovery for administrators:** caller.
- **CON-062 — Simple Chats definition administration:** caller.
- **CON-063 — Managed agent administration adapter:** caller.
- **CON-064 — Workflow definition and component curation:** caller.
- **CON-065 — Capability catalog curation:** caller.
- **CON-066 — Scheduler target and schedule discovery:** caller.
- **CON-067 — Governed workflow schedule administration:** caller.
- **CON-068 — Process definition and planning administration:** declaration owner, caller, implementation/integration boundary.
- **CON-069 — Authorized Resources discovery and retrieval metadata:** caller.
- **CON-070 — Resource catalog creation and metadata administration:** caller.
- **CON-071 — Authorized content streaming for automation:** caller.
- **CON-073 — Process step owner-operation extension port:** declaration owner, caller, implementation/integration boundary, extension adapter.
- **CON-074 — Collaboration message contributions:** caller.
- **CON-075 — Memory ingestion of owner-published evidence:** caller.
- **CON-076 — TestLab evidence and verdict authoring:** caller.

## Proof and unresolved questions

Relevant planned scenarios: QA-011, QA-021, QA-032, QA-036, QA-037, QA-038, QA-039, QA-040, QA-042, QA-044, QA-045, QA-046, QA-051, QA-052, QA-060, QA-062, QA-064, QA-078, QA-082, QA-097, QA-105, QA-140, QA-142, QA-143, QA-145, QA-159, QA-160, QA-161, QA-163, QA-164.

Related gaps: GAP-005, GAP-010, GAP-013, GAP-014, GAP-028, GAP-033, GAP-036, GAP-037, GAP-040.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
