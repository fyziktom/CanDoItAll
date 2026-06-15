# SB27 Project-Scoped Processes, Project Structure Integration, Agent Tools, And API Compatibility

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Close project-scoped Process integration, project-structure process actions, process agent tool/API compatibility, baseline scenarios, live run profiles, and scoped live/workspace behavior over the rebuilt contracts.

## Covered Inputs

- REQ-006 to REQ-011, REQ-030, REQ-037 to REQ-040, REQ-051, REQ-052.
- US-002 and US-049 through US-051.
- AC-010, AC-021, AC-026, AC-036, AC-039, AC-040.

## Prerequisites

- SB26 live/history dashboard complete.
- SB11 execution adapters and SB12 template history compatibility complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://Templates/Processes/seed-catalog/baseline-scenarios.json`
- `repo://Templates/Processes/seed-catalog/live-run-profiles.json`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Project-scoped Process workspace and live dashboard behavior.
- Project structure process assignment/open-run integration.
- Process agent tool facade over rebuilt application contracts.
- Baseline scenario and live run profile query/import compatibility.
- API compatibility tests for current tool and UI workflows that must remain available.

## Dependency Impact

- SB28 final regression depends on project-scoped and tool/API compatibility proof.

## Validation Depth

- E2E tests for project-scoped workspace and launch.
- Integration tests for agent tools and template seed catalogs.
- API compatibility tests for save/publish/delete/import/export/list/get/start/list-runs flows.
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
3. Rebuild process agent tool facade over application contracts.
4. Expose baseline scenarios and live run profiles through migrated template indexes.
5. Add compatibility tests and Playwright proof.
6. Record story coverage for US-002 and US-049 through US-051.

## Do Not Do

- Do not let agent tools call old runtime/services directly.
- Do not bypass authorization for project-scoped data.
- Do not special-case project routes with duplicated UI logic.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Project-scoped workspace and live routes work through scoped projections.
- [ ] Project-structure process links preserve context.
- [ ] Agent tool/API compatibility tests pass.
- [ ] Seed catalogs are available after migration.
- [ ] Browser proof exists.

## Proof Required

- E2E/integration/API compatibility test output.
- Playwright project-scoped screenshot evidence.
- Story coverage table for US-002 and US-049 through US-051.

## Browser Validation Logging

- Required. Capture project route, project-structure process action, scoped run/definition assertions, screenshots, and console/network summary.

## Progression Gate

- SB28 may start after project-scoped behavior and API/tool compatibility are proven.

## Suggested Agent Prompt

Execute SB27 from `codex/bundles/process-module-architecture-v3/subbundles/27-project-scoped-processes-project-structure-integration-agent-tools-and-api-compatibility`. Close project-scoped UI and process agent tool compatibility over rebuilt contracts.

## Handoff Notes For Next Bundle

Record complete project/API compatibility proof and any residual story risks for SB28.
