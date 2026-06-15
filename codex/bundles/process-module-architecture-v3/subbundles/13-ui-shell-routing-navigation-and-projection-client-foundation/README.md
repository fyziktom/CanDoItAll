# SB13 UI Shell, Routing, Navigation, And Projection Client Foundation

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild the Process UI shell over projection/application services, preserving the global and project workspace routes, tab shell, command strip, projection refresh behavior, and contextual agent entry points without direct runtime or persistence coupling.

## Covered Inputs

- REQ-030, REQ-051, REQ-052.
- US-001 and US-020.
- AC-021, AC-035, AC-039, AC-040.

## Prerequisites

- SB10 monitoring projection contracts complete.
- SB12 template/runtime history compatibility decisions complete.
- UI projection DTOs expose workspace shell state, definition catalog summaries, freshness metadata, and authorization flags.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://codex/bundles/process-module-architecture-v3/analysis/06-current-implementation-user-story-map.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/18-user-story-coverage-model.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Projection-first Process UI shell for `/processes` and project-scoped routes.
- Shared projection client/service layer for Process UI components.
- Navigation/tab shell that preserves current workspace mental model.
- Agent context entry point wired through projection/application contracts.
- Component tests and Playwright evidence for shell load/navigation.

## Dependency Impact

- SB14 through SB27 depend on this UI shell and projection client foundation.
- SB28 depends on the story proof emitted here.

## Validation Depth

- Component tests for route parameters, tab shell, command strip, loading/error states, and projection freshness display.
- Playwright MCP proof for `/processes` at desktop and one narrower viewport.
- Dependency scan proving UI shell does not reference runtime internals, EF runtime entities, or old observation services.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Implementation Steps

1. Define Process UI projection client interfaces and typed command receipt handling.
2. Rebuild `/processes` and project-scoped route shell around those interfaces.
3. Render command strip, tab navigation, loading/empty/error states, and projection freshness.
4. Wire contextual agent entry through authorized application commands.
5. Add component tests and Playwright smoke proof.
6. Record story coverage for US-001 and US-020.

## Do Not Do

- Do not query DbContext or runtime state from components.
- Do not call dispatcher/runtime internals.
- Do not implement definition list, editors, launch, run history, or live dashboard behavior in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Shell renders global and project-scoped routes through projection services.
- [ ] Tabs and command strip preserve the current UX direction.
- [ ] Agent entry point uses authorized application contracts.
- [ ] UI dependency scan passes.
- [ ] Playwright proof and screenshots are recorded.

## Proof Required

- Component test output.
- Playwright route/action/assertion/screenshot evidence.
- UI dependency scan output.
- Story coverage table for US-001 and US-020.

## Browser Validation Logging

- Required. Record route, viewport, actions, assertions, screenshot path, accessibility snapshot path when useful, console issues, network issues, and result.

## Progression Gate

- SB14 may start only after the shell renders from projection services and dependency scans prove no runtime/persistence coupling.

## Suggested Agent Prompt

Execute SB13 from `codex/bundles/process-module-architecture-v3/subbundles/13-ui-shell-routing-navigation-and-projection-client-foundation`. Rebuild only the Process UI shell and projection client foundation. Preserve shell UX, prove projection-only data access, and record US-001/US-020 coverage.

## Handoff Notes For Next Bundle

Record projection DTO gaps, shell screenshots, command receipt behavior, and any route issues that SB14 must handle.
