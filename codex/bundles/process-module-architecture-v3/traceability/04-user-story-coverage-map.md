# User Story Coverage Map

## Purpose

This file maps current implementation user stories to target architecture and future subbundles. It is the primary checklist future implementation agents must use to avoid losing Process functionality while replacing the old runtime/dispatcher design.

## Coverage Matrix

| Story range | Capability area | Current evidence | Architecture owners | Subbundle owners | Required proof |
| --- | --- | --- | --- | --- | --- |
| US-001 to US-004 | Workspace shell, definition catalog, scope tree, feed defaults | `ProcessWorkspace.razor`, `/processes` screenshot, template import tests | `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`, `architecture/18-user-story-coverage-model.md` | SB13, SB14, SB19 | Component tests, Playwright workspace screenshot, definition catalog projection tests. |
| US-005 to US-008 | Definition identity, governance, contracts, simulation, lint, save/publish/delete | `ProcessDefinitionForm.razor`, definition component/integration tests | `architecture/03-core-model-and-invariants.md`, `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md` | SB15 | Unit tests, component tests, command validation tests, Playwright definition edit proof. |
| US-009 to US-010, US-016 | Role authoring, executor preference, fallback, approval, role templates, step role assignment | `ProcessRoleEditorForm.razor`, role editor tests | `architecture/03-core-model-and-invariants.md`, `architecture/09-template-git-versioning-and-migrations.md` | SB16, SB18, SB21 | Component tests, role template override tests, launch role resolution tests. |
| US-011 to US-015, US-018 to US-019 | Step authoring, operation contracts, branch/routing, subprocess binding, canvas composition | `ProcessStepEditorForm.razor`, branch editor, canvas tests, operation contract E2E | `architecture/04-builder-and-instance-composition.md`, `architecture/13-branch-switch-and-loop-contract.md` | SB17, SB18, SB09 | Core tests, builder tests, component tests, Playwright canvas proof. |
| US-017, US-040, US-053 | Artifact expectations, obligations, evidence, recovery/resupply, cross-process references | `ProcessArtifactExpectationEditor.razor`, artifact projection/recovery tests | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | SB08, SB09, SB18, SB25 | Artifact ledger tests, recovery strategy tests, evidence UI proof. |
| US-020, US-050 to US-051 | Agent context and process agent tool facade | Workspace agent panel, `ProcessAgentRuntimeToolProvider`, agent tool tests | `architecture/16-execution-adapters-and-integration-boundaries.md` | SB13, SB27 | Tool contract tests, API compatibility tests, UI smoke for agent panel. |
| US-021 to US-025 | Template library, selective import, exchange, generated projections, Git UI | `ProcessTemplateLibraryDialog.razor`, template tests, manifest/seed catalogs | `architecture/09-template-git-versioning-and-migrations.md` | SB04, SB12, SB19, SB20 | Migration tests, template import tests, Git diff/conflict component tests, Playwright template proof. |
| US-026 to US-029 | Launch planning, candidate matching, approval, provisioning, execute ready launch | `ProcessWorkspaceRunsLaunchSection.razor`, launch integration/E2E tests | `architecture/04-builder-and-instance-composition.md`, `architecture/14-manager-runtime-and-control-loop.md` | SB06, SB21 | Launch plan integration tests, approval/provisioning tests, Playwright launch proof. |
| US-030 to US-035, US-054 | Run history, selected run details, active execution, runtime canvas, telemetry, outbox/dead letters | `ProcessWorkspaceRuns*`, runtime read/query/outbox tests | `architecture/05-runtime-dispatcher-and-state-machines.md`, `architecture/12-runtime-persistence-event-store-and-outbox.md` | SB07, SB08, SB22, SB23, SB24 | Runtime state tests, projection tests, Playwright run and runtime canvas proof. |
| US-036 to US-039, US-048 | Operator control center, escalations, approvals, manager directives, rework, live incident actions | Operator console component, live escalation action tests | `architecture/14-manager-runtime-and-control-loop.md` | SB09, SB24, SB26 | Manager incident tests, operator command tests, Playwright operator/live incident proof. |
| US-041 to US-043 | Assignment resolution, direct role messaging, manager chat | Assignment/messaging sections, direct messaging tests | `architecture/06-driver-strategy-and-manager-model.md`, `architecture/14-manager-runtime-and-control-loop.md` | SB25 | Assignment tests, messaging authorization tests, Playwright evidence/messaging proof. |
| US-044 to US-047 | Graphs, analytics, live dashboard, time window filtering, snapshot refresh | Graph/analytics tabs, `LiveProcessesDashboard.razor`, live screenshot | `architecture/08-monitoring-events-snapshots-and-ui-projections.md` | SB10, SB26, SB28 | Projection query tests, live history filter tests, Playwright live proof with screenshot. |
| US-049 | Project structure and project-scoped process integration | Project pages/dialogs and E2E tests | `architecture/16-execution-adapters-and-integration-boundaries.md` | SB27 | Project-scoped E2E, component tests, route-link proof. |
| US-052 | Parent/child subprocess manager communication and artifact propagation | Subprocess integration tests, runtime canvas subprocess action | `architecture/07-artifact-error-recovery-and-subprocess-model.md`, `architecture/14-manager-runtime-and-control-loop.md` | SB09, SB23, SB28 | Subprocess integration tests, parent/child message tests, runtime canvas proof. |
| US-055 | Governance, allowed operations, access summaries, sensitive handling, unauthorized mutation checks | Operation contract tests, access summary methods, governance architecture | `architecture/10-security-governance-and-agent-change-auditing.md` | SB10, SB11, SB20, SB28 | Policy tests, redaction tests, Git unauthorized mutation audit test. |

## Subbundle Story Ownership

| Subbundle | Primary user stories |
| --- | --- |
| SB01 | Evidence capture for all US rows. |
| SB02 | Boundary protection for all US rows; no active old implementation fallback. |
| SB03 | US-005 to US-018 structural core coverage. |
| SB04 | US-010, US-021 to US-025, US-051. |
| SB05 | US-020, US-050, US-052 to US-055 strategy/driver coverage. |
| SB06 | US-011 to US-018, US-026 to US-029 builder/plan coverage. |
| SB07 | US-030 to US-035, US-054 runtime/dispatcher coverage. |
| SB08 | US-017, US-030 to US-035, US-040, US-053 to US-054 durable storage coverage. |
| SB09 | US-014 to US-015, US-036 to US-039, US-052 to US-053 manager/branch/subprocess coverage. |
| SB10 | US-044 to US-047 and projection contracts for every browser-facing story. |
| SB11 | US-020, US-050, US-055 execution adapter coverage. |
| SB12 | US-004, US-021 to US-023, US-051 template migration/history compatibility coverage. |
| SB13 | US-001, US-020 shell/routing/projection client foundation. |
| SB14 | US-001 to US-004 definition list/scope/search/feed defaults. |
| SB15 | US-005 to US-008 definition editor/governance/lint/publish/delete. |
| SB16 | US-009 to US-010 role editor/templates/executor model. |
| SB17 | US-018 to US-019 definition canvas/toolbox/recomposition. |
| SB18 | US-011 to US-017 step editor/contracts/routing/artifacts/subprocess mapping. |
| SB19 | US-021 to US-023 template library browser/preview/selective import. |
| SB20 | US-024 to US-025 exchange/import-export/Git conflict UI. |
| SB21 | US-026 to US-029 launch planning/candidates/approval/provisioning. |
| SB22 | US-030 to US-032 run history/activity/selected-run controls. |
| SB23 | US-033 to US-035 runtime execution canvas/telemetry/subprocess run actions. |
| SB24 | US-036 to US-039, US-048 operator control/escalation/approval/rework/directives. |
| SB25 | US-040 to US-043 evidence/assignments/direct messaging/manager chat. |
| SB26 | US-044 to US-047 analytics/graphs/live dashboard/time filtering. |
| SB27 | US-002, US-049 to US-051 project-scoped process and tool/API compatibility. |
| SB28 | Full US-001 to US-055 regression and final coverage proof. |

## Handoff Rule

Each implementation subbundle report must include a story coverage table with columns:

| Story ID | Implemented source | Test proof | Browser proof | Delta from current UX | Remaining risk |
| --- | --- | --- | --- | --- | --- |

Final closure must include the complete table for US-001 through US-055.
