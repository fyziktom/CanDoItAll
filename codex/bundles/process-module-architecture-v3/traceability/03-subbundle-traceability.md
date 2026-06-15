# Subbundle Traceability

## Future Implementation Package Matrix

| Subbundle | Requirements / v3 gaps | Architecture files | User-story ownership | Acceptance criteria | Gates / proof |
| --- | --- | --- | --- | --- | --- |
| SB01 Reference Archive | REQ-048, REQ-049, REQ-051 | `analysis/04-current-code-evidence-map.md`, `analysis/05-reuse-decision-log.md`, `analysis/06-current-implementation-user-story-map.md`, `plan/02-phase-0-reference-archive-and-removal.md` | Evidence baseline for US-001 to US-055 | AC-027, AC-028, AC-039 | G01, archive hashes, UI evidence, story baseline. |
| SB02 Removal/Skeleton | REQ-047, REQ-049 | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | Boundary protection for all stories | AC-001, AC-002, AC-031 | G02, dependency/vocabulary/old-symbol tests. |
| SB03 Contracts/Core | REQ-001 to REQ-005, REQ-042 to REQ-045 | `architecture/03-core-model-and-invariants.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/18-user-story-coverage-model.md` | US-005 to US-018 structural coverage | AC-001, AC-003, AC-006, AC-033 | G03, pure core tests, branch contract tests. |
| SB04 Git/Templates | REQ-031 to REQ-041 | `architecture/09-template-git-versioning-and-migrations.md` | US-010, US-021 to US-025, US-051 | AC-022 to AC-026 | G04, Git/template/migration tests. |
| SB05 Driver Abstractions | REQ-006 to REQ-009 | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/11-project-boundary-and-dependency-map.md` | US-020, US-050, US-052 to US-055 | AC-010, AC-011, AC-031 | G05, driver contract/capability tests. |
| SB06 Builder | REQ-010 to REQ-014 | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | US-011 to US-018, US-026 to US-029 | AC-004, AC-005, AC-011, AC-033 | G06, golden plan and missing-binding tests. |
| SB07 Runtime/Event Ports | REQ-002, REQ-003, REQ-026 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | US-030 to US-035, US-054 | AC-006, AC-007, AC-008, AC-032 | G07, transition/claim/idempotency/event tests. |
| SB08 Persistence Stores | REQ-015 to REQ-019, REQ-026 to REQ-030 | `architecture/12-runtime-persistence-event-store-and-outbox.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | US-017, US-040, US-053, US-054 | AC-014, AC-018, AC-019, AC-032 | G08, event/outbox/ledger/projection store tests. |
| SB09 Manager/Branch/Subprocess | REQ-015 to REQ-025, REQ-042 to REQ-045 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-014, US-015, US-036 to US-039, US-052, US-053 | AC-012 to AC-017, AC-033, AC-034 | G09, incident/recovery/branch/subprocess tests. |
| SB10 Monitoring Projections | REQ-026 to REQ-030 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-044 to US-047 plus projection contracts for UI stories | AC-018 to AC-021, AC-035 | G10, live/history/replay/dead-letter tests. |
| SB11 Adapters/Driver Slice | REQ-006 to REQ-009, REQ-039, REQ-040 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | US-020, US-050, US-055 | AC-010, AC-011, AC-036 | G11, adapter envelope/redaction/driver-slice tests. |
| SB12 Template/History Compatibility | REQ-031 to REQ-037, REQ-050 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | US-004, US-021 to US-023, US-051 | AC-022 to AC-027, AC-037 | G12, migration/sidecar/history compatibility reports. |
| SB13 UI Shell | REQ-030, REQ-051, REQ-052 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/18-user-story-coverage-model.md` | US-001, US-020 | AC-021, AC-035, AC-039, AC-040 | G13, component/Playwright/dependency proof. |
| SB14 Definition List | REQ-030, REQ-031, REQ-051, REQ-052 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-001 to US-004 | AC-021, AC-022, AC-035, AC-040 | G14, catalog/search/feed-defaults proof. |
| SB15 Definition Editor | REQ-001, REQ-005, REQ-024, REQ-030, REQ-051, REQ-052 | `architecture/03-core-model-and-invariants.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-005 to US-008 | AC-003, AC-012, AC-021, AC-040 | G15, definition edit/lint/publish proof. |
| SB16 Role Editor | REQ-005, REQ-011, REQ-033, REQ-034, REQ-051, REQ-052 | `architecture/03-core-model-and-invariants.md`, `architecture/09-template-git-versioning-and-migrations.md` | US-009, US-010, US-016 | AC-003, AC-024, AC-040 | G16, role/template/executor proof. |
| SB17 Definition Canvas | REQ-005, REQ-011, REQ-030, REQ-042, REQ-051, REQ-052 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/13-branch-switch-and-loop-contract.md` | US-018, US-019 | AC-021, AC-033, AC-035, AC-040 | G17, canvas/recomposition proof. |
| SB18 Step Editor | REQ-004, REQ-008, REQ-011 to REQ-018, REQ-042 to REQ-045, REQ-051, REQ-052 | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | US-011 to US-017 | AC-004, AC-005, AC-011, AC-014, AC-033, AC-040 | G18, step/branch/artifact/subprocess proof. |
| SB19 Template Library | REQ-031 to REQ-037, REQ-051, REQ-052 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-021 to US-023 | AC-022 to AC-026, AC-040 | G19, template browser/import proof. |
| SB20 Exchange/Git UI | REQ-034, REQ-037 to REQ-041, REQ-051, REQ-052 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | US-024, US-025, US-055 | AC-024, AC-026, AC-036, AC-040 | G20, exchange/Git/conflict/security proof. |
| SB21 Launch Planning | REQ-010, REQ-011, REQ-014, REQ-024, REQ-051, REQ-052 | `architecture/04-builder-and-instance-composition.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-026 to US-029, US-041 | AC-004, AC-005, AC-012, AC-021, AC-040 | G21, launch/approval/provisioning proof. |
| SB22 Run History | REQ-002, REQ-026 to REQ-030, REQ-051, REQ-052 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | US-030 to US-032 | AC-006, AC-018, AC-020, AC-021, AC-040 | G22, run filters/details/control proof. |
| SB23 Runtime View | REQ-002, REQ-003, REQ-012, REQ-013, REQ-026 to REQ-030, REQ-051, REQ-052 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-033 to US-035, US-052 | AC-005 to AC-009, AC-013, AC-021, AC-040 | G23, runtime canvas/telemetry proof. |
| SB24 Operator Control | REQ-020 to REQ-025, REQ-026 to REQ-030, REQ-051, REQ-052 | `architecture/14-manager-runtime-and-control-loop.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | US-036 to US-039, US-048, US-054 | AC-012 to AC-017, AC-018 to AC-021, AC-034, AC-040 | G24, operator/escalation/rework proof. |
| SB25 Evidence/Coordination | REQ-015 to REQ-019, REQ-024, REQ-025, REQ-030, REQ-051, REQ-052 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-040 to US-043, US-053 | AC-014 to AC-017, AC-021, AC-040 | G25, evidence/assignment/messaging proof. |
| SB26 Analytics/Live | REQ-026 to REQ-030, REQ-051, REQ-052 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | US-044 to US-048 | AC-018 to AC-021, AC-035, AC-040 | G26, graph/live/time-window proof. |
| SB27 Project/API Compatibility | REQ-006 to REQ-011, REQ-030, REQ-037 to REQ-040, REQ-051, REQ-052 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | US-002, US-049 to US-051 | AC-010, AC-021, AC-026, AC-036, AC-040 | G27, project/API/tool proof. |
| SB28 Final Closure | REQ-001 to REQ-053 | All architecture files | US-001 to US-055 final regression | AC-001 to AC-041 | G28, E2E, scans, refactoring, security, complete story proof, performance scan summary. |

## Context Protection

Every subbundle README includes:

- context reset files or exact source references,
- source evidence,
- prerequisites,
- in/out scope or do-not-do rules,
- target deliverables,
- invariants through validation depth and acceptance checklist,
- implementation steps,
- refactoring or boundary review expectations,
- required tests/proof,
- browser validation logging where applicable,
- stop-and-report conditions through progression gate and do-not-do rules,
- acceptance checklist,
- handoff notes.

Every browser-facing subbundle must also update the user-story coverage table required by `validation/04-user-story-coverage-validation.md`.

Every subbundle that creates or modifies C# hot-path code must also include exact performance scan counts required by `validation/05-dotnet-performance-antipattern-checklist.md`.
