# BND-WORKFLOWS — Agents / Workflows

**Target owner:** Agents / Workflows

Versioned technical AI workflows, execution backends, runs/checkpoints/approvals and results. A product integration executor belongs to an adapter; generic runtime does not know the concrete product.

**Repository discovery location:** `None`

**Primary sources:** SRC-026, SRC-027, SRC-003, MAP-09

## Provided responsibilities

- Definition/version catalogs, input schemas and compiler/executor contracts; explicit backend/simulation support.
- Durable or currently supported launch/status/approval/cancel/recovery routes and owner output manifests.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Provider/Agent execution, Prompt versions, Plugin grants and capability-owned executors.
- Product-owned Structure gateways and authority resolvers through integration executors; Scheduler/Processes provide source-origin bindings.
- Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

## Forbidden shortcuts

- Preserve ProjectStructureWorkflowExecutor and launch from Structure. Do not generalize a name-based enum into a universal service locator.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: separate status queries from Structure projection updates while preserving launch/writeback in both directions.
- UNPROVEN: identical human-in-the-loop/recovery capabilities across all backends; extend only against a verified support matrix.

## Persistence

Workflows owns definitions/versions, runs and checkpoints. Structure stores bindings and projections, not a second state machine. A later edit must not change the resolved definition of an accepted run; LastRunId is not complete history.

## Recorded capabilities

- **FEAT-103** [CODE; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] The ProjectStructure executor exposes ListProjects, ReadTree, ReadNode, CreateAsset, and CreateTaskNodes through the runtime gateway.
- **FEAT-104** [USER; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workflows can be launched from Project Structure and write results back.
- **FEAT-118** [USER; TARGET_PROTOCOL_NOT_BLANKET_DELIVERY] Workflow steps can use authorized target-owner operations beyond Structure through explicitly registered typed adapters.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-003 — Governed agent execution:** caller.
- **CON-005 — AI workload cost quote:** caller.
- **CON-007 — Provider execution profile lease:** caller.
- **CON-018 — Structure read / invocation read:** caller.
- **CON-019 — Native structure authoring:** caller.
- **CON-020 — Structural links:** caller.
- **CON-021 — Ensure structure contribution:** caller.
- **CON-022 — Asset binding and content lifecycle:** caller.
- **CON-023 — Canonical work-item commands:** caller.
- **CON-029 — Workflow catalog/version lookup:** declaration owner, implementation/integration boundary.
- **CON-030 — Workflow launch/control:** declaration owner, implementation/integration boundary.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-033 — Prompt search/version retrieval:** caller.
- **CON-037 — Schedule plan and fire dispatch:** caller.
- **CON-040 — Plugin lifecycle/capability API:** caller.
- **CON-041 — Connector execution:** caller.
- **CON-043 — Managed storage lifecycle:** caller.
- **CON-051 — Execution source authority resolver:** caller.
- **CON-055 — Workflow run result publication:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-057 — Automation-safe CRM search and identity resolution:** caller.
- **CON-058 — Bounded CRM party/contact creation:** caller.
- **CON-059 — CRM affiliation read and mutation:** caller.
- **CON-060 — Staffing/resource-profile administration:** caller.
- **CON-061 — Simple Chats definition discovery for administrators:** caller.
- **CON-062 — Simple Chats definition administration:** caller.
- **CON-063 — Managed agent administration adapter:** caller.
- **CON-064 — Workflow definition and component curation:** declaration owner, caller, implementation/integration boundary.
- **CON-065 — Capability catalog curation:** caller.
- **CON-066 — Scheduler target and schedule discovery:** caller.
- **CON-067 — Governed workflow schedule administration:** caller.
- **CON-068 — Process definition and planning administration:** caller.
- **CON-069 — Authorized Resources discovery and retrieval metadata:** caller.
- **CON-070 — Resource catalog creation and metadata administration:** caller.
- **CON-071 — Authorized content streaming for automation:** caller.
- **CON-072 — Workflow executor owner-operation extension port:** declaration owner, caller, implementation/integration boundary, extension adapter.
- **CON-074 — Collaboration message contributions:** caller.
- **CON-075 — Memory ingestion of owner-published evidence:** caller.
- **CON-076 — TestLab evidence and verdict authoring:** caller.

## Proof and unresolved questions

Relevant planned scenarios: QA-010, QA-012, QA-017, QA-021, QA-027, QA-028, QA-029, QA-030, QA-031, QA-032, QA-033, QA-034, QA-035, QA-037, QA-039, QA-042, QA-044, QA-062, QA-064, QA-066, QA-068, QA-078, QA-120, QA-139, QA-141, QA-143, QA-144, QA-148, QA-151, QA-160, QA-161, QA-164.

Related gaps: GAP-003, GAP-004, GAP-005, GAP-013, GAP-014, GAP-033, GAP-036, GAP-040.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
