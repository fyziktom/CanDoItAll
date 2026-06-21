# Current Implementation User Story Map

## Purpose

This map captures the user-facing capabilities that exist in the current Process implementation and UI. It is not an endorsement of the current runtime/dispatcher design. It is a coverage instrument for the rewrite: future architecture and implementation subbundles must either preserve the capability through the new model, replace it with an explicitly better equivalent, or record an approved removal.

## Evidence Sources

Source evidence inspected:

- `repo://src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/Pages/LiveProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessRoleEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepEditorForm.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessArtifactExpectationEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessStepBranchOutcomeEditor.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRuns*.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessTemplateLibraryDialog.razor`
- `repo://src/CanDoItAll.Modules.Processes/Canvas`
- `repo://src/CanDoItAll.Modules.Processes/AgentTools`
- `repo://src/CanDoItAll.Modules.Processes/Runtime`
- `repo://src/CanDoItAll.Modules.Processes/Launch`
- `repo://src/CanDoItAll.Modules.Processes/Messaging`
- `repo://Templates/Processes/manifest.json`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://Templates/Processes/seed-catalog/live-run-profiles.json`
- `repo://tests/CanDoItAll.Tests.Components`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessManagementBundle.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessOperationContract.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`

Live UI evidence captured from `http://localhost:5032/`:

- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-page-workspace-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-steps-tab-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-runs-tab-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-template-library-dialog-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/live-processes-page-loaded-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/*.md`

## Current Route And Surface Inventory

| Surface | Current route/component | User-facing purpose | Rewrite treatment |
| --- | --- | --- | --- |
| Global Process workspace | `/processes`, `ProcessWorkspace` | Browse, create, edit, launch, run, monitor, and manage process definitions. | Preserve UX direction; replace data source with projection/application services. |
| Project-scoped Process workspace | `/projects/{ProjectId:guid}/processes`, `ProcessWorkspace` | Use the same workspace scoped to a project context. | Preserve; rebuild over project-scoped projection and command contracts. |
| Live Processes dashboard | `/processes/live`, `/projects/{ProjectId:guid}/processes/live`, `LiveProcessesDashboard` | Observe active/recent runs, incidents, activity, agents, graphs, and tool analytics. | Preserve; fix time-window filtering through projection-query semantics. |
| Template library dialog | `ProcessTemplateLibraryDialog` | Browse process, role, and artifact templates with previews and selective import. | Preserve; make JSON canonical and Git-backed. |
| Definition forms | `ProcessDefinitionForm` | Edit identity, governance, contracts, simulation readiness, linting, save/publish/delete. | Preserve fields as projection/command contracts; reject direct entity mutation. |
| Role forms | `ProcessRoleEditorForm` | Define executor expectations, fallback, approval, staffing intent, allocation, and template source. | Preserve, but use typed executor/role models. |
| Step forms | `ProcessStepEditorForm` | Define step kind, subprocess binding, operation contract, routing, role assignments, artifacts. | Preserve, map to typed core definition and builder plan contracts. |
| Canvas | `Canvas/*`, workspace steps tab | Visual composition of steps, routes, artifacts, branches, subprocesses, and runtime state. | Adapt visual concepts; rebuild against definition/runtime canvas projections. |
| Run workspace | `ProcessWorkspaceRuns*` | Launch, activity, lifecycle, control, execution, graphs, coordination, and evidence. | Split into smaller validated UI subbundles. |
| Manager chat | workspace manager chat tab | Communicate with process manager about selected runs. | Preserve through manager message projection and authorized command services. |

## Story Map

| ID | User story | Current implementation evidence | Target architectural coverage | Owning future subbundle |
| --- | --- | --- | --- | --- |
| US-001 | As a user, I can open global Process management and see counts, available definitions, and workspace commands. | `ProcessWorkspace.razor`; `/processes`; workspace screenshot. | UI shell projection, definition summary projection, command receipts. | SB13, SB14 |
| US-002 | As a user, I can open Process management inside a project and keep the project scope through the workspace. | `ProjectProcessesPage.razor`; project-scoped E2E tests. | Project-scoped query/command context and projection filters. | SB27 |
| US-003 | As a user, I can search and browse process definitions by scope. | `ProcessWorkspace.razor`; left tree snapshot; global/project categories. | Definition catalog projection with scope, search tokens, freshness. | SB14 |
| US-004 | As a user, I can seed/feed default process definitions from templates. | Workspace `Feed defaults`; template import services/tests. | Template catalog import command through application service and migration gate. | SB12, SB14, SB19 |
| US-005 | As a user, I can create, save, publish, archive/delete, and lint a process definition. | `ProcessDefinitionForm.razor`; `ProcessesService`; component tests. | Definition aggregate commands, lint projection, version status transitions. | SB15 |
| US-006 | As a user, I can edit definition identity fields such as name, scope, customer, owner, summary, and value statement. | Definition form Identity tab. | Definition identity command contract and projection. | SB15 |
| US-007 | As a user, I can configure governance fields such as criticality, autonomy, working status, manager override, notes, and change summary. | Definition form Governance tab. | Governance policy model, manager binding inputs, projection. | SB15 |
| US-008 | As a user, I can review contracts and simulation readiness on a definition. | Definition form Contracts and Simulation tabs. | Contract summary projection and simulation-readiness validation service. | SB15 |
| US-009 | As a user, I can define process roles with executor kind, workflow preference, staffing intent, allocation, fallback, and explicit approval. | `ProcessRoleEditorForm.razor`; role component tests. | Role definition model, executor model, launch role resolution inputs. | SB16, SB21 |
| US-010 | As a user, I can apply and customize global role templates locally. | Role template picker and apply path. | Template component reference, local override, conflict tracking. | SB04, SB16, SB19 |
| US-011 | As a user, I can define steps with key, title, subtitle, kind, target lead time, dependencies, and subprocess definition binding. | `ProcessStepEditorForm.razor`; subprocess integration tests. | Step definition model, subprocess plan compiler, dependency graph. | SB17, SB18 |
| US-012 | As a user, I can define operation contracts for steps, including allowed operations and target scope. | Step Execution tab; `ProcessStepOperation`; operation contract E2E test. | Typed operation policy contract enforced by builder/runtime/manager. | SB18 |
| US-013 | As a user, I can define step input/output/evidence contracts, decision rights, and exception policy. | Step Contracts tab. | Contract model, validation/linting, manager incident classification. | SB18, SB24 |
| US-014 | As a user, I can define branch outcomes and default/error routing. | `ProcessStepBranchOutcomeEditor.razor`; branch outcome editor; branching templates. | Typed branch definitions, route table, loop budgets. | SB09, SB18 |
| US-015 | As a user, I can route a process forward or backward, including through branch/switch steps. | Branching-code-review template; branch outcomes; runtime transitions. | Branch route target model, backward route fingerprints, escalation policy. | SB09, SB18, SB28 |
| US-016 | As a user, I can bind roles to steps with responsibility kinds. | Step Roles tab; `ProcessStepRoleAssignmentEditor`. | Step role assignment model and launch/runtime assignment projection. | SB16, SB18, SB26 |
| US-017 | As a user, I can define artifact expectations for steps, including kind, trust, sensitivity, retention, workflow output mapping, child artifact mapping, future usage, and validation. | `ProcessArtifactExpectationEditor.razor`. | Artifact slot model, artifact ledger, trust/sensitivity/retention policy, subprocess artifact references. | SB08, SB18, SB25 |
| US-018 | As a user, I can use a canvas/toolbox to add and connect steps, branches, subprocesses, role bindings, and artifacts. | `ProcessWorkspaceStepsTab`, `Canvas/*`, canvas component tests. | Definition canvas projection and canvas command adapter. | SB17 |
| US-019 | As a user, I can select canvas nodes and edit the selected process element without losing context. | Canvas selection panel tests and workspace step tab. | Selection projection, command receipts, refresh token model. | SB17, SB18 |
| US-020 | As a user, I can open process agents contextually from the workspace. | Workspace agent floating window. | Agent availability projection and authorized handoff command. | SB13, SB27 |
| US-021 | As a user, I can browse a template catalog by category and search term. | `ProcessTemplateLibraryDialog.razor`; template dialog screenshot. | Template catalog projection backed by JSON/Git index. | SB19 |
| US-022 | As a user, I can preview template overview, Markdown, diagrams, JSON, and structure tree. | Template dialog preview tabs. | JSON canonical source plus generated Markdown/Mermaid/structure projections. | SB04, SB19 |
| US-023 | As a user, I can import a full process template or selectively add related role/artifact components. | Template dialog Add role/Add artifact; target step selector. | Template component import commands, local override metadata. | SB19 |
| US-024 | As a user, I can import/export process definitions and preserve source metadata and warnings. | Agent tools and import metadata integration tests. | Exchange envelope, migration/upcast, validation report. | SB20 |
| US-025 | As a user, I can view Git-style status, diffs, commits, merges, and conflicts for template/configuration changes. | Required by architecture; not complete in current UI. | Generic Git UI component library and Git wrapper projections. | SB04, SB20 |
| US-026 | As a user, I can create a launch plan for a process with a launch name and operating mode. | `ProcessWorkspaceRunsLaunchSection.razor`; launch planning tests. | Launch plan aggregate, role demand model, command receipts. | SB21 |
| US-027 | As a user, I can match delivery-team candidates to process roles and see gaps or provisioning needs. | Launch role candidate matrix, HR match button, launch tests. | Role resolution service, candidate projection, gap/provisioning states. | SB21 |
| US-028 | As a user, I can submit a launch plan for approval, approve/request changes/reject, provision resources, and execute when ready. | Launch approval/provisioning UI and integration tests. | Approval workflow, provisioning command, launch execution gate. | SB21 |
| US-029 | As a user, I can start or execute a process run only through governed launch paths, not arbitrary direct run creation. | Lifecycle section states direct creation is blocked; launch services. | Application command policy and runtime start adapter. | SB21, SB22 |
| US-030 | As a user, I can browse run history and filter by text, state, operating mode, updated time, and tags. | `ProcessWorkspaceRunsLifecycleSection.razor`. | Run history projection with query filters. | SB22 |
| US-031 | As a user, I can select a run and see status, manager, subprocess depth, attempts, pending approvals, missing artifacts, dead letters, diagnostics, and recommended recovery. | Runs tab selected run summary; runs screenshot. | Run detail projection, incident projection, recovery projection. | SB22, SB24 |
| US-032 | As a user, I can stop a blocked run when policy allows. | Lifecycle blocked run stop button. | Authorized run control command and state machine transition. | SB22 |
| US-033 | As a user, I can inspect active process execution and active agent state. | Active section; runtime observation tests. | Active execution projection, agent activity projection. | SB23 |
| US-034 | As a user, I can inspect runtime step status and use runtime canvas actions such as open subprocess run, start, complete, block, wait approval, refuse, fail, and prepare artifact capture. | Runtime canvas action dialog. | Runtime canvas projection, typed operator commands, state machine validation. | SB23 |
| US-035 | As a user, I can see provider/execution telemetry and detect runtime status mismatches. | Runtime host read/query tests and execution projections. | Runtime read model, telemetry projection, invariant diagnostics. | SB23, SB24 |
| US-036 | As an operator, I can view a Process Control Center with escalations, approvals, dead letters, timeline, diagnostics, and recovery advice. | `ProcessWorkspaceRunsOperatorConsoleSection.razor`. | Operator projection, incident projection, outbox/dead-letter projection. | SB24 |
| US-037 | As an operator, I can assign, resolve, reopen, and request rework on escalations. | Operator console and live escalation action tests. | Escalation command model, manager incident lifecycle. | SB24 |
| US-038 | As an operator, I can approve, reject, or request changes for pending approvals. | Approval Console. | Approval aggregate and projection. | SB24 |
| US-039 | As an operator, I can send a manager directive and request targeted rework for an eligible step. | Manager Direction and Rework Console. | Manager message/command model, bounded rework workflow. | SB24 |
| US-040 | As a user, I can review artifact obligations, record artifacts, inspect an artifact matrix, and see work briefs, decision records, and conformance observations. | Artifacts/Evidence sections and artifact projection tests. | Artifact ledger, evidence projection, decision/conformance projections. | SB25 |
| US-041 | As a user, I can resolve runtime assignments to people, AI agents, or workflows with fallback and direct-message permissions. | `ProcessWorkspaceRunsAssignmentsSection.razor`. | Assignment resolution aggregate and executor binding projection. | SB25 |
| US-042 | As a user, I can send direct role-to-role messages and review transcript projections with collaboration thread links. | `ProcessWorkspaceRunsMessagingSection.razor`; messaging tests. | Role messaging service, authorization, transcript projection. | SB25 |
| US-043 | As a user, I can select a manager chat run and communicate with the process manager. | Manager chat tab. | Manager chat projection and command service. | SB25 |
| US-044 | As a user, I can view process graphs and analytics in the workspace. | Graphs and Analytics tabs. | Graph/analytics projections backed by event snapshots. | SB26 |
| US-045 | As a user, I can open Live Processes and see running, blocked, failed, observed, agent, context, tool, time, and cost summary data. | Live dashboard loaded screenshot. | Live snapshot cache and live summary projection. | SB26 |
| US-046 | As a user, I can change live/history time windows and process filters without stale older events appearing in live hour results. | Live dashboard history window; observed current defect. | Projection-query boundary enforces time-window semantics. | SB10, SB26, SB28 |
| US-047 | As a user, I can refresh live data without forcing the runtime to recompute or reload everything from scratch. | Live refresh control and snapshot concept. | Snapshot cache, freshness metadata, force refresh command. | SB10, SB26 |
| US-048 | As a user, I can inspect live activity cards and act on escalation notifications from live mode. | Live activity cards with Details, Request rework, Resolve, close. | Live incident projection and authorized operator commands. | SB24, SB26 |
| US-049 | As a user, I can run project-structure process actions from project nodes and return to process workspace/run context. | Project structure process assignment dialog and E2E tests. | Project-structure integration adapter and scoped links. | SB27 |
| US-050 | As an agent/tool user, I can save, publish, delete, export, import, and inspect process definitions/templates through process agent tools. | `ProcessAgentRuntimeToolProvider`; integration tests. | Application tool facade over the same command/query contracts. | SB27 |
| US-051 | As an agent/tool user, I can list baseline scenarios and live run profiles for seeded process execution. | Agent tool provider; seed catalogs. | Template pack/index projection and seeded scenario services. | SB12, SB27 |
| US-052 | As a process manager, I can communicate with subprocess managers and propagate child process outcomes and artifact references to the parent. | Subprocess integration tests; subprocess UI actions. | Durable parent/child manager messages, child artifact references, run hierarchy projection. | SB09, SB23, SB28 |
| US-053 | As a process manager, I can recover/resupply missing artifacts from completed or earlier steps without discarding completed step results. | Artifact status/projection/recovery tests; recovery option contract tests. | Artifact ledger, recovery policy, manager incident lifecycle. | SB08, SB09, SB24, SB25 |
| US-054 | As a process manager, I can detect dispatcher/outbox failures, dead letters, stale leases, and retry/recovery states. | Outbox, automation dispatch, observation cache tests. | Durable outbox, claims, leases, dead-letter projection, manager incident routing. | SB07, SB08, SB24 |
| US-055 | As a governance owner, I can enforce allowed operations, access summaries, sensitive data handling, and unauthorized mutation checks. | Operation contract tests, access summary methods, security/governance architecture. | Policy engine, restricted diagnostics, Git unauthorized mutation audit. | SB10, SB11, SB20, SB28 |
| US-056 | As a launch planner, I can compare role candidates by both suitability score and deterministic readiness, including missing required tools, rights, capabilities, approvals, bindings, provisioning tasks, execution blockers, and user-safe resolution guidance. | `ProcessesService.Launch.CandidateDiscovery.cs`, `ProcessesService.Launch.Staffing.cs`, `ProcessWorkspaceRunsLaunchSection.razor`, `ProcessLaunchPlanningIntegrationTests.cs`; current gap recorded in `analysis/08-current-role-candidate-selection-gap.md`. | Role execution requirement set, candidate suitability score breakdown, candidate readiness assessment, typed readiness findings, provisioning/approval task planning, launch UI readiness projection. | SB21, SB24, SB27, SB28 |

## Current Template Capability Map

The current template manifest contains a broad process catalog that the rewrite must migrate or intentionally replace:

| Template group | Current examples | Story coverage implication |
| --- | --- | --- |
| .NET and runtime proof | `dotnet-solution-setup`, `dotnet-feature-function-implementation`, `dotnet-development-slice`, `dotnet-runtime-command-writeback`, `dotnet-ui-screenshot-writeback` | Drivers and strategies must support layered software-development tasks without placing .NET terms in the generic core. |
| Blazor delivery and repair | `blazor-app-delivery`, `blazor-app-repair-fix`, `blazor-backend-feature`, `blazor-frontend-feature`, `blazor-fullstack-feature` | Current domain drivers are examples, not final architecture. They must be refactored behind driver capability descriptors. |
| Governance and delivery | `branching-code-review`, `hotfix-rollout`, `architecture-decision-governance`, `release-readiness-and-deployment`, `oss-intake-supply-chain-governance`, `ai-assisted-change-delivery` | Branching, approvals, artifact trust, Git auditing, and escalation behavior must be first-class architecture. |
| Business and incident examples | `customer-onboarding`, `business-plan-development`, `incident-response` | Generic core must remain domain-neutral and support non-software processes through drivers/templates. |
| Screenshot/layout workflows | `app-page-screenshot`, `app-pages-screenshot-set`, `app-layout-image-generation` | Workflows and external tools must be execution strategies/adapters, not runtime concepts. |

## Live UI Findings That Must Influence Rewrite

- The Live Processes page already exposes the right user concept: active/live summaries plus a history window selector.
- The current implementation shows older events under the `Live 1h` window. This confirms REQ-029 and US-046: time filtering must happen at the projection/query boundary, not by loosely filtering already mixed view data.
- The workspace tabs are a good UX anchor. The rewrite should keep the workspace mental model while splitting implementation into projection-backed query services and typed command services.
- The current template dialog contains useful preview and selective import behavior. The rewrite should preserve the user experience but make JSON canonical and turn Markdown/Mermaid into generated/exported projections.
- The run workspace has too many concerns for one implementation phase. Launch, lifecycle, active execution, operator controls, evidence, assignments, messaging, analytics, and live dashboards need separate subbundles and separate validation evidence.

## Reuse Classification For Story Coverage

| Current area | Reuse decision for rewrite |
| --- | --- |
| Process workspace visual organization | Adapt UX structure; replace service/data plumbing. |
| Live Processes layout and action model | Adapt UX structure; rebuild over snapshot projections and fix time windows. |
| Canvas concepts | Adapt node/port/status visual ideas; rebuild projection and command model. |
| Template library UX | Adapt browsing/preview/selective import; replace canonical source and versioning model. |
| Current runtime dispatcher | Replace. Do not wrap or preserve as foundation. |
| Current drivers | Review and refactor aggressively. Keep useful domain knowledge only behind new driver/strategy contracts. |
| Current tests | Treat as story evidence and regression intent; rewrite where coupled to old architecture. |
| Current templates | Migrate through deterministic template migration and compatibility reports. |

## Mandatory Coverage Rule

Every future implementation subbundle must name the user stories it owns, the stories it intentionally does not own, the story proof it adds, and the downstream story risks it creates. Final closure cannot pass until every US-001 through US-056 row is covered by source proof, test proof, and browser proof where browser-facing.
