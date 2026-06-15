# Future Subbundle Roadmap

## Purpose

This roadmap maps SB01-SB14 to architecture files, acceptance criteria, and required context. These subbundles are ready for later execution after user approval, but none are executed in v3.

## Roadmap Table

| Subbundle | Depends on | Primary architecture files | Acceptance criteria focus | Context reset files |
| --- | --- | --- | --- | --- |
| SB01 Reference Archive | Architecture approval | `analysis/04-current-code-evidence-map.md`, `analysis/05-reuse-decision-log.md`, `plan/02-phase-0-reference-archive-and-removal.md` | AC-027, AC-028 | README, analysis evidence, Phase 0 plan, source evidence map. |
| SB02 Removal/Skeleton | SB01 | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | AC-001, AC-002, AC-027 | SB01 report, dependency map, Phase 0 plan. |
| SB03 Contracts/Core | SB02 | `architecture/03-core-model-and-invariants.md`, `architecture/13-branch-switch-and-loop-contract.md` | AC-001, AC-003, AC-006 | SB02 report, core model, branch contract. |
| SB04 Git/Templates | SB03 | `architecture/09-template-git-versioning-and-migrations.md` | AC-022 through AC-026 | SB03 report, template/Git architecture. |
| SB05 Driver Abstractions | SB03 | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/11-project-boundary-and-dependency-map.md` | AC-010, AC-011 | SB03 report, driver model. |
| SB06 Builder | SB04, SB05 | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | AC-004, AC-005, AC-011 | SB04/SB05 reports, builder/compiler architecture. |
| SB07 Runtime/Event Ports | SB06 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | AC-006 through AC-009 | SB06 report, runtime state machines, persistence ports. |
| SB08 Persistence Stores | SB07 | `architecture/12-runtime-persistence-event-store-and-outbox.md` | AC-008, AC-018, AC-019 | SB07 report, persistence/event/outbox architecture. |
| SB09 Manager/Branch/Subprocess | SB08 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/14-manager-runtime-and-control-loop.md` | AC-012 through AC-017 | SB08 report, manager/branch/artifact architecture. |
| SB10 Monitoring Projections | SB08, SB09 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | AC-018 through AC-021 | SB08/SB09 reports, monitoring/UI projection architecture. |
| SB11 Adapters/Driver Slice | SB10 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/06-driver-strategy-and-manager-model.md` | AC-010, AC-011, AC-016 | SB10 report, adapter architecture. |
| SB12 Template/History Compatibility | SB11 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | AC-022 through AC-027 | SB11 report, template/Git/history architecture. |
| SB13 UI Rebuild | SB12 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/09-template-git-versioning-and-migrations.md` | AC-021, AC-026 | SB12 report, UI projection architecture. |
| SB14 Final Closure | SB13 | All architecture files, validation plans, hardening gates | AC-001 through AC-030 | All prior reports and proof manifests. |

## Dependency Notes

- SB04 and SB05 can be parallelized after SB03 only if public contracts are stable.
- SB10 can begin projection contract work after SB08 storage contracts exist, but live/history correctness requires SB09 manager/incident events.
- SB13 must not start UI rewrite before SB10 projection contracts are usable and SB12 compatibility decisions are known.

## Required Handoff Pattern

Every future subbundle execution report must state:

- files changed,
- tests run,
- tests skipped and why,
- dependency scan,
- domain leak scan,
- old-symbol scan,
- refactoring review,
- known risks,
- exact next-bundle handoff.
