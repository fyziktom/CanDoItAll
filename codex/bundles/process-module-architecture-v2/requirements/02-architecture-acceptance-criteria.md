# Architecture Acceptance Criteria

## Core And Boundary Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-001 | Core defines only domain-neutral process concepts and strongly typed IDs. |
| AC-002 | Architecture specifies forbidden dependency rules for every target project. |
| AC-003 | Definition, template source, instance plan, runtime state, events, snapshots, and UI models are separate planes. |
| AC-004 | Instance plan semantics are immutable and selected strategy bindings are persisted before runtime. |
| AC-005 | Subprocesses are recursively composed through the builder and represented in the parent plan. |

## Runtime And Dispatcher Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-006 | Runtime state machines define allowed transitions, owners, events, and idempotency behavior. |
| AC-007 | Dispatcher claim/lease lifecycle defines creation, renewal, expiration, reclaim, completion, cancellation, and duplicate result handling. |
| AC-008 | Runtime emits typed events with correlation, causation, schema version, actor, sensitivity, and UTC timestamps. |
| AC-009 | Runtime, dispatcher, and manager responsibilities are separate. |

## Driver Strategy Manager Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-010 | Driver discovery, dependency, conflict, precedence, and capability matching rules are explicit. |
| AC-011 | Strategy families are named and bound before runtime where required. |
| AC-012 | Manager inputs, outputs, decision records, recovery rules, and escalation rules are explicit. |
| AC-013 | Parent/child manager communication uses durable generic messages. |

## Artifact Error Recovery Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-014 | Artifact model includes definitions, slots, instances, references, ledger, validation, access, lineage, freshness, sensitivity, retention, recovery, and resupply. |
| AC-015 | Missing or stale artifacts produce manager incidents and recovery/resupply requests through policy. |
| AC-016 | Error model separates runtime faults, persistence faults, dispatcher faults, strategy faults, domain diagnostics, policy denials, missing artifacts, blocked external resources, manager incidents, and escalations. |
| AC-017 | Automatic recovery is explicitly bounded by policy, approvals, idempotency, budgets, and fingerprints. |

## Monitoring Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-018 | Monitoring is event-first and projection-backed. |
| AC-019 | Projection worker offsets, dead letters, freshness metadata, replay, current snapshots, and history projections are defined. |
| AC-020 | Live and history query semantics distinguish active-run inclusion from time-range filtered historical events. |
| AC-021 | UI reads projections and does not derive runtime truth. |

## Template Git Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-022 | JSON is canonical template source. |
| AC-023 | Markdown and Mermaid are generated/exported projections with hashes if stored. |
| AC-024 | Global components, local overrides, publish updates, conflict records, and manual conflict resolution are defined. |
| AC-025 | Schema/content versioning and deterministic migration chain with skipped-version safety are defined. |
| AC-026 | Git wrapper and generic Git UI responsibilities are separated from Process-specific screens. |

## Rewrite Criteria

| ID | Acceptance criterion |
| --- | --- |
| AC-027 | Phase 0 archive/removal plan is precise enough for a future implementation pass. |
| AC-028 | Reuse decision log identifies archive/adapt/drop/replace decisions for major current surfaces. |
| AC-029 | Project-by-project rebuild plan defines dependencies, tests, and stop conditions. |
| AC-030 | Traceability maps every normalized requirement to architecture, plan/gate, and validation. |
