# BND-WORK — Work Management within Project Structure

**Target owner:** Work Management within Project Structure

Tasks/work items, schedules, dependencies, assignments, baselines and acceptance. One mutation route serves Gantt, canvas, CRM and tools.

**Repository discovery location:** `None`

**Primary sources:** SRC-025, MAP-02, MAP-08

## Provided responsibilities

- Canonical task queries and typed creation/edit/dependency/scheduling/assignment commands; Gantt is their view.
- Work baselines, cost allocation and accepted-result rules; publishes workload summaries for CRM.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Agents catalog and Providers quotes; CRM resource/participation facts and capacity reservation.
- Projects lifecycle/scope; Structure native placement; Processes/Workflows execution state and manifests.

## Forbidden shortcuts

- Work assignment is not capacity reservation; a completed runtime does not automatically mean an accepted result.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: distinguish tasks from process steps and assignments from reservations, and establish one writer for CRM/Gantt/tools.
- ESTIMATE: workload-aware estimates, baseline versioning and formal result-specific acceptance; preserve supported policies without automatic expansion.

## Persistence

The initial target can retain ProjectObjectRecord as the canonical task. Keep one task identity, not synchronized writable Tasks and Nodes tables. Current ProjectPartyAssignment data requires semantic reconciliation before ownership moves.

## Recorded capabilities

- **FEAT-105** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Tasks/Gantt, resource-cost strategies, assignment revisions, and plan analytics form the existing work plan.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-005 — AI workload cost quote:** caller.
- **CON-009 — Usage evidence and allocation query:** caller.
- **CON-010 — CRM party/resource catalog:** caller.
- **CON-013 — Capacity reservation:** caller.
- **CON-023 — Canonical work-item commands:** declaration owner, implementation/integration boundary.
- **CON-024 — Work assignment:** declaration owner, implementation/integration boundary.
- **CON-025 — Gantt and dependency mutations:** declaration owner, implementation/integration boundary.
- **CON-026 — Plan estimate/baseline commit:** declaration owner, implementation/integration boundary.
- **CON-031 — Process run result publication:** caller.
- **CON-054 — Agent run result publication:** caller.
- **CON-055 — Workflow run result publication:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-002, QA-004, QA-025, QA-026, QA-035, QA-038, QA-047, QA-048, QA-049, QA-050, QA-052, QA-059, QA-060, QA-061, QA-092, QA-106, QA-118, QA-120, QA-124, QA-127, QA-128, QA-146, QA-149, QA-162.

Related gaps: GAP-002, GAP-010, GAP-011, GAP-012, GAP-028.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
