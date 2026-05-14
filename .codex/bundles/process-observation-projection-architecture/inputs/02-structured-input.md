# Structured Input

## Core Objective

- Create an implementation-ready subbundle plan for a typed, read-only process observation layer that can support future live dashboards, lazy detail dialogs, and AI-driven dashboard focus while preserving the current Processes page.

## Success Criteria

- Current process/UI communication is mapped with concrete source references.
- Observation contracts, caching, UI migration, AI intent, and validation are split into dependency-ordered subbundles.
- Cache policy avoids split source of truth and has explicit staleness/error behavior.
- Blazor performance guidance is reflected in UI phases.
- Validation covers mock agents, generic process behavior, independent simple .NET app builds, component tests, and browser proof.

## Hard Constraints

- No production implementation now.
- Preserve current functionality.
- Keep process logic generic.
- Observation UI remains read-only.
- Do not introduce silent fallback mechanisms that hide failures.
- Do not introduce Radzen unless the codebase later adopts it; current evidence does not require it.

## Allowed Side Effects

- Create and update this bundle only.

## Source Artifacts

- User request from 2026-05-08.
- Source files listed in `inputs/01-source-artifacts.md`.
- Microsoft Learn pages listed in `inputs/01-source-artifacts.md`.
- Code analytics snapshot `snap-20260508224200-0d8ff021`.

## Input Coverage Signals

- Live multi-process UI and dialog drill-down are future goals, not immediate UI implementation.
- Services and process core/UI communication must be prepared first.
- `IMemoryCache` is desirable but risky if it becomes an authority.
- Future AI conversation should change dashboard focus and details, not mutate process core.
- Existing lazy loading/performance gains must be preserved.

## Dependency And Sequencing Signals

- Current-state map must precede contracts.
- Contracts must precede cache and UI migration.
- Cache/invalidation must precede high-volume dashboard UI.
- AI intent bridge must wait until observation contracts exist.
- Validation closes the bundle after all implementation subbundles.

## Validation Expectations

- Build and targeted tests for process runtime/read/UI.
- Scale/performance tests for multi-process observation.
- Mock-agent process run validation.
- Independent simple .NET app build scenarios.
- Browser validation for `/processes` when UI changes are implemented.

## Evidence Contract

- Commands, test results, performance timings, browser screenshots, and execution report updates must be captured in subbundle execution.

## UI Validation Strategy

- For UI-changing subbundles, run a large-screen browser pass, screenshot review, and narrower-width follow-up.
- Inspect text overlap, virtualized list behavior, dialog lazy loading, stale/error state, and rerender responsiveness.

## Browser Validation Analytics

- Subbundles 04 and 06 must log route, viewport, Playwright actions, assertions, screenshot paths, and visual findings in `reviews/01-execution-report.md`.

## Working Assumptions

- First implementation targets the current single-instance app shape.
- BaseLib/CanvasLib remain the UI foundation.
- SignalR is optional later infrastructure, not the first required step.

## Primary Risks

- Cache split source of truth.
- Refresh/query amplification under many active processes.
- UI rerender flood.
- Authorization leakage in cache keys.
- AI intent bypassing typed read-only boundaries.
