# MOD-CRM — CRM / HR: parties and human/AI resource planning

**Target owner:** CRM / HR

Personnel and commercial identities, contacts, relationships, recruitment, owned resource profiles, governance and capacity records. Technical agents and AI tariffs have external authorities. Project-level participation and WorkItem assignment must not collapse into one anonymous link.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.CrmHr`

**Primary sources:** SRC-010, MAP-08

## Provided responsibilities

- Party/resource discovery and personnel detail according to scope and privacy policy.
- Owner CRM commands and HR lifecycle; CRM owns synchronization of AI bindings.
- Capacity and staffing/reservation contracts where functionally proven or delivered under separate scope.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.
- Existing HR-wired safe search, party creation, and affiliation operations are preservation requirements; broader staffing editing is a separately scoped extension.

## Required capabilities

- Agents: safe technical catalog and lifecycle events.
- Providers: quote and tariff summaries.
- Projects: existence and scope; Work Management: workload and assignment commands.
- Memory and Collaboration through owned source gateways and references, not foreign writes.

## Forbidden shortcuts

- Do not own a second agent definition or AI price list.
- Do not send confidential notes to a generic agent picklist.
- Do not repair assignments by writing Workbench rows; use the single owner command.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: separate technical agent projections from CRM governance field by field.
- REQUIRED FOR THE BOUNDARY: preserve multiple roles, primary assignments, time intervals and cardinality when migrating assignments.
- ESTIMATE: fully enforced capacity reservations and concurrent planning; a CapacityBlock alone does not prove a complete reservation engine.

## Persistence

CRM owns Party, AiResourceBinding and personnel/commercial aggregates. Moving work-item assignment authority to Work Management is an explicit data change requiring reconciliation. Project participation remains a separate CRM/Projects integration with one designated writer.

## Recorded capabilities

- **FEAT-016** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] CRM directory, workforce, CRM, and recruiting surfaces with pagination and detail dialogs.
- **FEAT-017** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] The Agents route projects technical Agents identities through AiResourceBinding.TechnicalAgentId and governance information.
- **FEAT-018** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Agent edits use a composite snapshot with invalidation and a bounded cache lifetime.
- **FEAT-019** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] The /api/crm-hr surface uses application services for audit, indexing, activity, and lifecycle behavior.
- **FEAT-020** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Parties, contact points, addresses, relationships, affiliations, confidential notes, and audit.
- **FEAT-021** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Accounts, interactions, opportunities, participants, and status history.
- **FEAT-022** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Workforce skills, capacity, staffing, applications, interviews, and onboarding.
- **FEAT-023** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] ProjectPartyAssignment, project roles, node relations, rates, and Gantt transfer/move receipts.
- **FEAT-024** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Privacy-safe agent queries and a Memory source gateway.
- **FEAT-111** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] HR already invokes canonical CRM party creation for nonsensitive people, organizations, and organization units.
- **FEAT-112** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] HR already lists and upserts bounded CRM affiliations while preserving restricted HR fields.
- **FEAT-113** [CODE; OBSERVED_CODE_NOT_RUNTIME_PROVEN] Typed CRM agent summaries declare redaction, untrusted-business-text status, bounded query, record kind, and availability.
- **FEAT-121** [USER; OPTIONAL_FUTURE_PRESET] Optional CRM-specialist preset uses the same CRM-owned commands and separately granted capabilities, not a second CRM writer.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-001 — Agent catalog / batch lookup:** caller.
- **CON-002 — Agent definition commands:** caller.
- **CON-005 — AI workload cost quote:** caller.
- **CON-009 — Usage evidence and allocation query:** caller.
- **CON-010 — CRM party/resource catalog:** declaration owner, implementation/integration boundary.
- **CON-011 — CRM domain commands:** declaration owner, implementation/integration boundary.
- **CON-012 — AI resource projection/binding reconcile:** declaration owner, implementation/integration boundary.
- **CON-013 — Capacity reservation:** declaration owner, implementation/integration boundary.
- **CON-014 — Project participation:** declaration owner, implementation/integration boundary.
- **CON-015 — Project lookup:** caller.
- **CON-023 — Canonical work-item commands:** caller.
- **CON-024 — Work assignment:** caller.
- **CON-026 — Plan estimate/baseline commit:** caller.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-038 — Memory operation API:** caller.
- **CON-039 — Memory source snapshot publication:** implementation/integration boundary, extension adapter.
- **CON-045 — Post-commit invalidation/live progress:** caller.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-057 — Automation-safe CRM search and identity resolution:** declaration owner, implementation/integration boundary.
- **CON-058 — Bounded CRM party/contact creation:** declaration owner, implementation/integration boundary.
- **CON-059 — CRM affiliation read and mutation:** declaration owner, implementation/integration boundary.
- **CON-060 — Staffing/resource-profile administration:** declaration owner, implementation/integration boundary.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-009, QA-022, QA-047, QA-048, QA-049, QA-050, QA-052, QA-056, QA-057, QA-058, QA-059, QA-060, QA-065, QA-070, QA-076, QA-077, QA-083, QA-084, QA-085, QA-086, QA-087, QA-088, QA-089, QA-091, QA-092, QA-098, QA-099, QA-101, QA-102, QA-106, QA-109, QA-110, QA-117, QA-118, QA-119, QA-120, QA-122, QA-123, QA-124, QA-125, QA-126, QA-127, QA-128, QA-137, QA-138, QA-139, QA-141, QA-142, QA-144, QA-145, QA-146, QA-149, QA-154, QA-155, QA-157, QA-161, QA-162, QA-163, QA-164.

Related gaps: GAP-009, GAP-010, GAP-011, GAP-012, GAP-019, GAP-038, GAP-039.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
