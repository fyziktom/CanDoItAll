# 03-process-workspace-graph-tabs

## Status

- `Blocked`

## Objective

- Add lazy-loaded graph tabs to the process workspace: one selected-process view for merged all-runs graphs and one selected-run view scoped to a specific process run.

## Success Criteria

- Selected process details include a graph tab using existing chart semantics.
- The process all-runs graph tab does not fetch historical graph data until the user clicks `Show graphs of all runs of process`.
- All-runs graph range defaults to one month and offers `1 day`, `1 week`, `1 month`, `3 months`, `1 year`, and `all`.
- Selected process-run details include a graph tab that loads only when selected and only for that run.
- UI uses existing project components/styles and remains readable at desktop and narrower widths.

## Covered Inputs

- N006 / R005 / R007 / R008: selected process all-runs graph tab.
- N007 / R006 / R007 / R008: selected run graph tab.
- N008 / R005 / R006 / R008: lazy load only when selected and scoped.
- N009 / R005 / R008: explicit all-runs button, default range, and range options.

## Prerequisites

- SB01 progression gate has passed.
- SB02 progression gate has passed and exposes a usable scoped graph data contract.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.AnalyticsPresenter.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.RunsPresenter.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunDetailsLoader.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceAnalyticsTab.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

## Deliverables

- Process workspace selected-process graph tab.
- Explicit all-runs graph load button with default one-month range.
- Range selector with all requested options.
- Selected-run graph tab with run-scoped lazy loading.
- Component/browser proof for lazy-load behavior and layout.

## Dependency Impact

- This is the final user-visible phase. Weak proof here leaves the accounting and analytics fixes inaccessible from the requested process page.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Inspect current process workspace tab constants and selected-run detail layout.
2. Add selected-process graph tab state with default one-month range and a separate explicit-loaded flag.
3. Wire the all-runs button to SB02 process-scoped graph data.
4. Add selected-run graph tab state that triggers data load on tab selection.
5. Reuse live dashboard chart rendering patterns or extract a small shared graph component if it reduces duplication.
6. Add component tests for no eager all-runs load, explicit all-runs load, range options, and run-scope load.
7. Run browser validation and update proof artifacts and execution report.

## Scope Exceptions

- Do not redesign unrelated process workspace tabs.
- Do not add a new standalone graph route.
- Do not add marketing/help text to explain the feature.

## Do Not Do

- Do not fetch all-runs graph data merely because the process graph tab was activated.
- Do not use raw string scopes when a typed enum or value object fits the existing code.
- Do not introduce a new chart library.

## Acceptance Checklist

- Process graph tab initially shows controls and the explicit all-runs load button without historical graph data fetched.
- Clicking the button loads one-month all-runs graphs by default.
- Changing range reloads or clearly prepares the selected range without silently using a stale scope.
- Run graph tab fetches only the selected run data on activation.
- Browser screenshots show no overlapping controls, clipped labels, or empty chart containers when data exists.

## Proof Required

- Component test transcript under `proof/SB03/transcripts/`.
- Browser screenshots under `proof/SB03/browser/`.
- Browser validation notes in `reviews/01-execution-report.md`.
- Final raw-note closure update for N006-N009.

## Browser Validation Logging

- Target route: `/processes`.
- Required viewports: large desktop first, narrower follow-up if tab labels or chart controls wrap.
- Required actions: select a process, open the process graph tab, confirm explicit load button before data load, click the button, verify graph rendering, select a process run, open run graph tab, verify scoped graph rendering.
- Evidence path: `proof/SB03/browser/process-graphs-desktop.png`.
- Review questions: does accidental tab selection avoid loading all-runs data, are range controls visible, do charts fit, and is run graph scope visibly tied to the selected run?

## Progression Gate

- Final closure cannot proceed until component tests and browser proof demonstrate lazy load behavior and graph rendering, or the execution report documents an environment blocker with equivalent test proof.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
