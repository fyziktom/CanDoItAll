# Future Subbundle Roadmap

## Purpose

This roadmap maps SB01-SB28 to architecture files, acceptance criteria, user-story ownership, and required context. These subbundles are ready for later execution after user approval, but none are executed in v3.

## Roadmap Table

| Subbundle | Depends on | Primary architecture files | Story ownership | Acceptance criteria focus | Context reset files |
| --- | --- | --- | --- | --- | --- |
| SB01 Reference Archive | Architecture approval | `analysis/04-current-code-evidence-map.md`, `analysis/05-reuse-decision-log.md`, `analysis/06-current-implementation-user-story-map.md`, `plan/02-phase-0-reference-archive-and-removal.md` | Evidence for US-001 to US-055 | AC-027, AC-028, AC-039 | README, analysis evidence, current story map, Phase 0 plan. |
| SB02 Removal/Skeleton | SB01 | `architecture/11-project-boundary-and-dependency-map.md`, `plan/03-project-by-project-rebuild-plan.md` | Boundary protection for all stories | AC-001, AC-002, AC-027 | SB01 report, dependency map, Phase 0 plan. |
| SB03 Contracts/Core | SB02 | `architecture/03-core-model-and-invariants.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/18-user-story-coverage-model.md` | US-005 to US-018 structural coverage | AC-001, AC-003, AC-006, AC-033 | SB02 report, core model, branch contract, story coverage model. |
| SB04 Git/Templates | SB03 | `architecture/09-template-git-versioning-and-migrations.md` | US-010, US-021 to US-025, US-051 | AC-022 to AC-026 | SB03 report, template/Git architecture. |
| SB05 Driver Abstractions | SB03 | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/11-project-boundary-and-dependency-map.md` | US-020, US-050, US-052 to US-055 | AC-010, AC-011 | SB03 report, driver model. |
| SB06 Builder | SB04, SB05 | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | US-011 to US-018, US-026 to US-029 | AC-004, AC-005, AC-011 | SB04/SB05 reports, builder/compiler architecture. |
| SB07 Runtime/Event Ports | SB06 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | US-030 to US-035, US-054 | AC-006 to AC-009 | SB06 report, runtime state machines, persistence ports. |
| SB08 Persistence Stores | SB07 | `architecture/12-runtime-persistence-event-store-and-outbox.md` | US-017, US-040, US-053, US-054 | AC-008, AC-018, AC-019 | SB07 report, persistence/event/outbox architecture. |
| SB09 Manager/Branch/Subprocess | SB08 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/13-branch-switch-and-loop-contract.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-014, US-015, US-036 to US-039, US-052, US-053 | AC-012 to AC-017, AC-033, AC-034 | SB08 report, manager/branch/artifact architecture. |
| SB10 Monitoring Projections | SB08, SB09 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-044 to US-047 and projection contracts for UI stories | AC-018 to AC-021, AC-035 | SB08/SB09 reports, monitoring/UI projection architecture. |
| SB11 Adapters/Driver Slice | SB10 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/06-driver-strategy-and-manager-model.md` | US-020, US-050, US-055 | AC-010, AC-011, AC-036 | SB10 report, adapter architecture. |
| SB12 Template/History Compatibility | SB11 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/17-runtime-history-migration-and-readonly-compatibility.md` | US-004, US-021 to US-023, US-051 | AC-022 to AC-027, AC-037 | SB11 report, template/Git/history architecture. |
| SB13 UI Shell | SB10, SB12 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/18-user-story-coverage-model.md` | US-001, US-020 | AC-021, AC-035, AC-039, AC-040 | SB10/SB12 reports, UI projection architecture, story map. |
| SB14 Definition List | SB13 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-001 to US-004 | AC-021, AC-035, AC-040 | SB13 report, story coverage map. |
| SB15 Definition Editor | SB14 | `architecture/03-core-model-and-invariants.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-005 to US-008 | AC-003, AC-021, AC-039, AC-040 | SB14 report, definition model, story map. |
| SB16 Role Editor | SB15 | `architecture/03-core-model-and-invariants.md`, `architecture/09-template-git-versioning-and-migrations.md` | US-009, US-010, US-016 | AC-003, AC-024, AC-040 | SB15 report, role/template architecture. |
| SB17 Definition Canvas | SB16 | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/13-branch-switch-and-loop-contract.md` | US-018, US-019 | AC-021, AC-033, AC-040 | SB16 report, canvas projection contract. |
| SB18 Step Editor | SB17 | `architecture/03-core-model-and-invariants.md`, `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | US-011 to US-017 | AC-004, AC-005, AC-014, AC-033, AC-040 | SB17 report, step/branch/artifact architecture. |
| SB19 Template Library | SB18, SB12 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-021 to US-023 | AC-022 to AC-026, AC-040 | SB12/SB18 reports, template architecture. |
| SB20 Exchange/Git UI | SB19 | `architecture/09-template-git-versioning-and-migrations.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | US-024, US-025, US-055 | AC-026, AC-036, AC-040 | SB19 report, Git wrapper and UI component contracts. |
| SB21 Launch Planning | SB18 | `architecture/04-builder-and-instance-composition.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-026 to US-029, US-041 | AC-004, AC-012, AC-021, AC-040 | SB18 report, builder/manager architecture. |
| SB22 Run History | SB21 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | US-030 to US-032 | AC-006, AC-020, AC-021, AC-040 | SB21 report, runtime/projection architecture. |
| SB23 Runtime View | SB22 | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | US-033 to US-035, US-052 | AC-006 to AC-009, AC-021, AC-040 | SB22 report, runtime canvas projection. |
| SB24 Operator Control | SB23 | `architecture/14-manager-runtime-and-control-loop.md`, `architecture/07-artifact-error-recovery-and-subprocess-model.md` | US-036 to US-039, US-048, US-054 | AC-012 to AC-017, AC-021, AC-040 | SB23 report, manager/incident architecture. |
| SB25 Evidence/Coordination | SB24 | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/14-manager-runtime-and-control-loop.md` | US-040 to US-043, US-053 | AC-014 to AC-017, AC-021, AC-040 | SB24 report, artifact/manager architecture. |
| SB26 Analytics/Live | SB25 | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | US-044 to US-047 | AC-018 to AC-021, AC-040 | SB25 report, monitoring projection architecture. |
| SB27 Project/API Compatibility | SB26 | `architecture/16-execution-adapters-and-integration-boundaries.md`, `architecture/10-security-governance-and-agent-change-auditing.md` | US-002, US-049 to US-051 | AC-010, AC-036, AC-039, AC-040 | SB26 report, adapter/tool/API architecture. |
| SB28 Final Closure | SB27 | All architecture files, validation plans, hardening gates | US-001 to US-055 final regression | AC-001 to AC-041 | All prior reports, proof manifests, and performance scan summaries. |

## Dependency Notes

- SB04 and SB05 can be parallelized after SB03 only if public contracts are stable.
- SB10 can begin projection contract work after SB08 storage contracts exist, but live/history correctness requires SB09 manager/incident events.
- SB13 must not start UI rewrite before SB10 projection contracts are usable and SB12 compatibility decisions are known.
- SB14 through SB20 are definition-authoring UI subbundles. They must produce browser proof before launch/runtime UI work starts.
- SB21 through SB26 are runtime-operation UI subbundles. They must validate one complex user journey at a time instead of bundling all run screens together.
- SB27 closes project-scoped integration and API/tool compatibility before final E2E regression.
- SB28 repeats critical stories but must not be the first browser proof for any major UI surface.
- SB28 must aggregate .NET performance scan evidence from all C# hot-path subbundles and run final scans over the rebuilt Process projects.

## Required Handoff Pattern

Every future subbundle execution report must state:

- files changed,
- user stories owned,
- user stories deferred and the downstream owner,
- tests run,
- tests skipped and why,
- Playwright route/screenshot proof for browser-facing stories,
- dependency scan,
- domain leak scan,
- old-symbol scan,
- refactoring review,
- performance scan counts and accepted tradeoffs for C# hot-path changes,
- known risks,
- exact next-bundle handoff.
