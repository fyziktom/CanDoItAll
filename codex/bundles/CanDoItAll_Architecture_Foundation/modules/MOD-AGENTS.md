# MOD-AGENTS — Agents: definitions, capabilities and governed use

**Target owner:** Agents

Technical agent identity, definitions and templates, capabilities, status and configuration; agent execution and governance policy. Provider prices belong to Agents/Providers and technical AI workflows to Agents/Workflows. The current module folder also includes integration and presentation; an assembly name does not define a domain.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.AgentFramework`

**Primary sources:** SRC-009, SRC-003, MAP-05, MAP-06

## Provided responsibilities

- Agent discovery and details as safe summaries; technical create/update/archive commands.
- Governed start, observation, cancellation and approval continuation; execution-source registration.
- Owned capabilities and agent-launch ports supporting curators of other domains.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Providers: runtime profiles, capability compatibility, quotes and usage.
- Security: purpose-limited secret references and resolution.
- Owner-published tools for Structure, Prompts, CRM, Memory, Scheduler and Plugins.
- Conversation shell: presentation and input intent, not execution ownership.
- Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

## Forbidden shortcuts

- Do not write Party, AiResourceBinding or tasks directly through EF.
- Do not move all product DTOs into generic MAF Core.
- Do not store CRM profiles in agent ConfigurationJson or turn Simple Chats into agents.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: separate technical catalog and execution APIs from UI and CRM writers.
- ESTIMATE: more precise resource-capacity/admission views and workload cost quotes; not authorization to implement new functions.
- UNPROVEN: complete human-in-the-loop coverage across every workflow/backend; preserve existing approvals.

## Persistence

Agent-owned definitions and execution state remain in their canonical stores. Preserve profile fencing, concurrency-token stamping, committed-with-warning outcomes and history when splitting persistence. Do not indiscriminately move every runtime cache into the database or the reverse.

## Recorded capabilities

- **FEAT-001** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Agent and provider catalogs, governed execution, agent chat, usage, and technical-agent integration.
- **FEAT-002** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Agent chat accepts an execution activity before context preparation completes and exposes an observable stream.
- **FEAT-003** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Immutable preparation and provider snapshots are fenced by database profile, generation, and revision.
- **FEAT-004** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Source-authority registry validates Projects, Workbench, and Processes context during restoration and rejects mismatches.
- **FEAT-005** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Tool availability depends on purpose, permissions, capability assignments, and approval; registration alone is insufficient.
- **FEAT-006** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Serial execution of tool calls preserves provider order and approval barriers.
- **FEAT-007** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Capabilities, templates, validation, MCP/A2A, lifecycle status, and governance editors.
- **FEAT-008** [USER; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] User-confirmed floating agent over Project Structure/Gantt can read and create files and create tasks.
- **FEAT-009** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Voice, attachments, image generation, HR, and curator-adjacent tabs require rebaselining of their actual delivered surfaces.
- **FEAT-110** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] HR provider performs managed-identity and CRM-scope checks at attachment and again at invocation.
- **FEAT-120** [USER; REQUIRED_EXTENSION_WHERE_NOT_ALREADY_SUPPORTED] Scoped operational agents can query CRM/Resources for task planning without impersonating managed HR.
- **FEAT-122** [CODE; TARGETED_CODE_READ_NOT_FULL_HELPER_AUDIT] HR agent administration filters privileged capabilities, creates drafts, and distinguishes saved agent from downstream CRM/team warnings.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** declaration owner, implementation/integration boundary.
- **CON-002 — Agent definition commands:** declaration owner, caller, implementation/integration boundary.
- **CON-003 — Governed agent execution:** declaration owner, caller, implementation/integration boundary.
- **CON-004 — Agent approval / cancellation / status:** declaration owner, caller, implementation/integration boundary.
- **CON-006 — Provider administration:** caller.
- **CON-007 — Provider execution profile lease:** caller.
- **CON-008 — Shared-provider publish/import/sync:** caller.
- **CON-009 — Usage evidence and allocation query:** caller.
- **CON-011 — CRM domain commands:** caller.
- **CON-012 — AI resource projection/binding reconcile:** caller.
- **CON-018 — Structure read / invocation read:** caller.
- **CON-019 — Native structure authoring:** caller.
- **CON-020 — Structural links:** caller.
- **CON-021 — Ensure structure contribution:** caller.
- **CON-022 — Asset binding and content lifecycle:** caller.
- **CON-023 — Canonical work-item commands:** caller.
- **CON-024 — Work assignment:** caller.
- **CON-025 — Gantt and dependency mutations:** caller.
- **CON-027 — Process launch:** caller.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-033 — Prompt search/version retrieval:** caller.
- **CON-034 — Prompt authoring/curation:** caller.
- **CON-035 — Resource catalog and promotion:** caller.
- **CON-036 — TestLab plan/run/evidence:** caller.
- **CON-037 — Schedule plan and fire dispatch:** caller.
- **CON-038 — Memory operation API:** caller.
- **CON-040 — Plugin lifecycle/capability API:** caller.
- **CON-041 — Connector execution:** caller.
- **CON-042 — Secret reference and purpose-scoped use:** caller.
- **CON-044 — Context surface capture:** declaration owner, caller.
- **CON-045 — Post-commit invalidation/live progress:** caller.
- **CON-047 — Workspace preference API:** caller.
- **CON-048 — Database profile and host capability control:** caller.
- **CON-049 — Ordinary Simple Chats operations:** caller.
- **CON-051 — Execution source authority resolver:** declaration owner, caller.
- **CON-052 — Authorized file browsing/opening:** caller.
- **CON-053 — Managed curator launch port:** implementation/integration boundary, extension adapter.
- **CON-054 — Agent run result publication:** declaration owner, implementation/integration boundary.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-057 — Automation-safe CRM search and identity resolution:** caller.
- **CON-058 — Bounded CRM party/contact creation:** caller.
- **CON-059 — CRM affiliation read and mutation:** caller.
- **CON-060 — Staffing/resource-profile administration:** caller.
- **CON-061 — Simple Chats definition discovery for administrators:** caller.
- **CON-062 — Simple Chats definition administration:** caller.
- **CON-063 — Managed agent administration adapter:** declaration owner, caller, implementation/integration boundary.
- **CON-064 — Workflow definition and component curation:** caller.
- **CON-065 — Capability catalog curation:** declaration owner, caller, implementation/integration boundary.
- **CON-066 — Scheduler target and schedule discovery:** caller.
- **CON-067 — Governed workflow schedule administration:** caller.
- **CON-068 — Process definition and planning administration:** caller.
- **CON-069 — Authorized Resources discovery and retrieval metadata:** caller.
- **CON-070 — Resource catalog creation and metadata administration:** caller.
- **CON-071 — Authorized content streaming for automation:** caller.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-074 — Collaboration message contributions:** caller.
- **CON-075 — Memory ingestion of owner-published evidence:** caller.
- **CON-076 — TestLab evidence and verdict authoring:** caller.

## Proof and unresolved questions

Relevant planned scenarios: QA-001, QA-002, QA-003, QA-004, QA-005, QA-006, QA-007, QA-008, QA-009, QA-011, QA-012, QA-013, QA-014, QA-015, QA-017, QA-020, QA-023, QA-027, QA-033, QA-036, QA-039, QA-040, QA-053, QA-054, QA-055, QA-056, QA-057, QA-058, QA-066, QA-067, QA-068, QA-072, QA-084, QA-089, QA-091, QA-096, QA-098, QA-099, QA-100, QA-104, QA-109, QA-115, QA-121, QA-122, QA-123, QA-124, QA-125, QA-126, QA-127, QA-128, QA-129, QA-130, QA-131, QA-132, QA-133, QA-134, QA-135, QA-136, QA-137, QA-138, QA-143, QA-144, QA-146, QA-147, QA-149, QA-150, QA-151, QA-152, QA-153, QA-154, QA-155, QA-156, QA-157, QA-158, QA-159, QA-161.

Related gaps: GAP-001, GAP-008, GAP-009, GAP-013, GAP-017, GAP-018, GAP-019, GAP-025, GAP-026, GAP-027, GAP-032, GAP-033, GAP-034, GAP-036, GAP-038.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
