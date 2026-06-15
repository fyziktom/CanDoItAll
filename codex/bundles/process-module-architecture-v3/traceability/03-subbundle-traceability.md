# Subbundle Traceability

## Future Implementation Package Matrix

| Subbundle | Requirements / v3 gaps | Architecture files | Acceptance criteria | Gates / proof |
| --- | --- | --- | --- | --- |
| SB01 Reference Archive | REQ-048, REQ-049 | `analysis/04-current-code-evidence-map.md`, `analysis/05-reuse-decision-log.md`, `plan/02-phase-0-reference-archive-and-removal.md` | AC-027, AC-028 | G01, archive hashes, manifest proof. |
| SB02 Removal/Skeleton | REQ-047, REQ-049 | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | AC-001, AC-002, AC-031 | G02, dependency/vocabulary/old-symbol tests. |
| SB03 Contracts/Core | REQ-001 to REQ-005, REQ-042 to REQ-045 | `architecture/03-core-model-and-invariants.md`, `architecture/13-branch-switch-and-loop-contract.md` | AC-001, AC-003, AC-006, AC-033 | G03, pure core tests, branch contract tests. |
| SB04 Git/Templates | REQ-031 to REQ-041 | `architecture/09-template-git-versioning-and-migrations.md` | AC-022 to AC-026 | G04, Git/template/migration tests. |
| SB05 Driver Abstractions | REQ-006 to REQ-009 | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/11-project-boundary-and-dependency-map.md` | AC-010, AC-011, AC-031 | G05, driver contract/capability tests. |
| SB06 Builder | REQ-010 to REQ-014 | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | AC-004, AC-005, AC-011, AC-033 | G06, golden plan and missing-binding tests. |
| SB07 Runtime/Event Ports | REQ-002, REQ-003, REQ-026 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | AC-006, AC-007, AC-008, AC-032 | G07, transition/claim/idempotency/event tests. |
| SB08 Persistence Stores | REQ-026 to REQ-030 | `architecture/12-runtime-persistence-event-store-and-outbox.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | AC-018, AC-019, AC-032 | G08, event/outbox/ledger/projection store tests. |
| SB09 Manager/Branch/Subprocess | REQ-015 to REQ-025, REQ-042 to REQ-045 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/14-manager-runtime-and-control-loop.md` | AC-012 to AC-017, AC-033, AC-034 | G09, incident/recovery/branch/subprocess tests. |
| SB10 Monitoring Projections | REQ-026 to REQ-030 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | AC-018 to AC-021, AC-035 | G10, live/history/replay/dead-letter tests. |
| SB11 Adapters/Driver Slice | REQ-006 to REQ-009, REQ-039, REQ-040 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | AC-010, AC-011, AC-036 | G11, adapter envelope/redaction/driver-slice tests. |
| SB12 Template/History Compatibility | REQ-031 to REQ-037, REQ-050 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | AC-022 to AC-027, AC-037 | G12, migration/sidecar/history compatibility reports. |
| SB13 UI Rebuild | REQ-030, REQ-041 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | AC-021, AC-026, AC-035 | G13, component and Playwright proof. |
| SB14 Final Closure | REQ-001 to REQ-050 | All architecture files | AC-001 to AC-038 | G14, E2E, scans, refactoring, security proof. |

## Context Protection

Every subbundle README includes:

- context reset files,
- source evidence,
- prerequisites,
- in/out scope,
- target projects/files,
- deliverables,
- invariants,
- steps,
- refactoring review,
- required tests/proof,
- search proof,
- stop-and-report conditions,
- do-not-do rules,
- acceptance checklist,
- handoff notes.
