# SB27 Project-Scoped Processes, Project Structure Integration, Process APIs, Codex Skill, And API Compatibility

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Close project-scoped Process integration, project-structure process actions, typed Process HTTP APIs, Codex Process API skill coverage, process agent tool/API compatibility, baseline scenarios, live run profiles, final E2E scenario loading workflow, and scoped live/workspace behavior over the rebuilt contracts.

## Covered Inputs

- REQ-006 to REQ-011, REQ-030, REQ-037 to REQ-040, REQ-051, REQ-052, REQ-055.
- US-002 and US-049 through US-051.
- AC-010, AC-021, AC-026, AC-036, AC-039, AC-040, AC-043.

## Prerequisites

- SB26 live/history dashboard complete.
- SB11 execution adapters and SB12 template history compatibility complete.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://Templates/Processes/seed-catalog/live-run-profiles.json`
- `repo://codex/bundles/process-module-architecture-v3/analysis/09-final-e2e-project-structure-source-scenarios.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/21-process-api-codex-skill-and-e2e-source-scenarios.md`
- `repo://codex/bundles/process-module-architecture-v3/validation/07-final-e2e-source-scenario-validation.md`
- `repo://codex/bundles/process-module-architecture-v3/evidence/e2e-source-project-structures/tetrisgame-live-5032-summary.json`
- `repo://codex/bundles/process-module-architecture-v3/evidence/e2e-source-project-structures/final-e2e-scenario-source-packs.json`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Project-scoped Process workspace and live dashboard behavior.
- Project structure process assignment/open-run integration.
- Typed Process HTTP API surface for definitions, templates, launch plans, candidate readiness, runs, steps, assignments, artifacts, manager control, escalations, projections, and scenario loading.
- Codex Process API skill updated for the rebuilt API.
- Process agent tool facade over rebuilt application contracts.
- Baseline scenario and live run profile query/import compatibility.
- Final E2E scenario loading workflow for `TetrisGame`, `RecipePlannerPwa`, `IssueTriageDashboard`, and `InvoiceApprovalPortal`.
- API compatibility tests for current tool and UI workflows that must remain available.

## Dependency Impact

- SB28 final regression depends on project-scoped and tool/API compatibility proof.

## Validation Depth

- E2E tests for project-scoped workspace and launch.
- Integration tests for agent tools and template seed catalogs.
- API compatibility tests for save/publish/delete/import/export/list/get/start/list-runs flows.
- API compatibility tests for scenario loading, candidate readiness, artifact readback, escalation readback, and project-structure process writeback.
- Codex skill route/workflow parity review.
- Playwright proof for project-scoped route and project-structure process link.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Wire project-scoped workspace and live routes to scoped projection/query services.
2. Rebuild project-structure process action integration.
3. Rebuild the typed Process HTTP API surface over application contracts.
4. Update or create the Codex Process API skill for the rebuilt API.
5. Rebuild process agent tool facade over application contracts.
6. Expose baseline scenarios and live run profiles through migrated template indexes.
7. Implement final E2E scenario loading workflow through public APIs.
8. Add compatibility tests, skill parity proof, and Playwright proof.
9. Record story coverage for US-002 and US-049 through US-051.

## Do Not Do

- Do not let agent tools call old runtime/services directly.
- Do not bypass authorization for project-scoped data.
- Do not special-case project routes with duplicated UI logic.
- Do not create Tetris-specific, recipe-specific, issue-dashboard-specific, or invoice-specific Process APIs.
- Do not use direct database edits or hidden test-only stores for scenario loading.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if final E2E scenario loading cannot be performed through public typed APIs.
- Stop if a Codex agent would need undocumented routes or database access to load scenarios.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Project-scoped workspace and live routes work through scoped projections.
- [ ] Project-structure process links preserve context.
- [ ] Typed Process APIs cover definition/template import, launch planning, candidate readiness, execution, run readback, artifact readback, escalation readback, and scenario loading.
- [ ] Codex Process API skill documents the rebuilt routes and scenario loading workflow.
- [ ] Agent tool/API compatibility tests pass.
- [ ] Seed catalogs are available after migration.
- [ ] Browser proof exists.

## Proof Required

- E2E/integration/API compatibility test output.
- Codex Process API skill parity proof.
- Scenario loading API proof for all final E2E source scenarios.
- Playwright project-scoped screenshot evidence.
- Story coverage table for US-002 and US-049 through US-051.

## Browser Validation Logging

- Required. Capture project route, project-structure process action, scenario-loaded process link, scoped run/definition assertions, screenshots, and console/network summary.

## Progression Gate

- SB28 may start after project-scoped behavior, API/tool compatibility, Codex skill parity, and scenario loading workflow are proven.

## Suggested Agent Prompt

Execute SB27 from `codex/bundles/process-module-architecture-v3/subbundles/27-project-scoped-processes-project-structure-integration-agent-tools-and-api-compatibility`. Close project-scoped UI, typed Process APIs, Codex API skill parity, scenario loading workflow, and process agent tool compatibility over rebuilt contracts.

## Handoff Notes For Next Bundle

Record complete project/API compatibility proof, Codex skill parity proof, scenario loading commands/tests, and any residual story risks for SB28.
