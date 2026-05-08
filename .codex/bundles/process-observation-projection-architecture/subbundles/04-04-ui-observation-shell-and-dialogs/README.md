# 04-ui-observation-shell-and-dialogs

## Status

- `Ready`

## Objective

- Incrementally move Processes UI observation reads to the new observation service and prepare lazy, typed detail dialogs without building the full future dashboard.

## Success Criteria

- Existing Processes page functionality is preserved.
- Current lazy-loading improvements remain intact.
- Live refresh is centralized/coalesced through observation state rather than page-local fan-out.
- Detail dialogs load typed payloads lazily from descriptors.
- High-count lists use virtualization/windowing where the changed surface can grow large.
- Browser proof confirms the page still works and does not visually regress.

## Covered Inputs

- R-001, R-002, R-004, R-007, R-008, R-010, R-011.
- Blazor guidance in `architecture/03-blazor-observation-guidance.md`.
- Observation contracts from `02`.
- Cache/invalidation behavior from `03`.

## Prerequisites

- `01-current-state-observation-map` is complete.
- `02-observation-contracts-and-boundary` is complete.
- `03-projection-cache-and-invalidation` is complete for the read shapes consumed by this UI phase, or the execution report explicitly approves a non-cached adapter path with no overload risk.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.LiveRefresh.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Loading.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsActiveSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunDetailsLoader.cs`

## Deliverables

- Circuit-scoped observation state/coordinator for dashboard/runs/details refresh.
- UI integration that consumes observation snapshots for the migrated slices.
- Typed dialog descriptor handling and lazy payload loading for selected details.
- Use of `@key` and virtualization/windowing on affected repeated high-count lists.
- Component tests for migrated behavior.
- Browser validation artifacts.
- New observation state/dialog files under `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components` or the selected services boundary.

## Dependency Impact

- `06-validation-performance-and-rollout` depends on this phase to prove the architecture works in the real UI.
- Future dashboard UI depends on this phase to establish state and dialog patterns.
- Weak browser proof here can hide broken existing workflows, bad rendering, or refresh overload.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Confirm current Processes page behavior and any open changes in the worktree.
2. Introduce or wire the observation state coordinator with granular notifications.
3. Replace one narrow read path at a time, starting with active dashboard/run summaries rather than selected-run details.
4. Preserve tab-aware loading and avoid analytics refresh while Analytics is hidden.
5. Add typed dialog descriptor/payload flow for one existing detail surface, then extend only where low risk.
6. Add virtualization/windowing and `@key` to changed repeated surfaces that can grow large.
7. Ensure all background refresh notifications use `InvokeAsync` when updating Blazor state.
8. Add/update component tests for current behavior and migrated observation behavior.
9. Run browser proof on `/processes` with large and narrow viewport checks.
10. Update execution report with commands, screenshots, DOM checks, and residual risks.

## Scope Exceptions

- Do not build the complete new flexible dashboard UI.
- Do not build conversational AI UI.
- Do not migrate every Processes component if a narrower slice proves the architecture.

## Do Not Do

- Do not remove existing behavior without direct replacement proof.
- Do not add Radzen.
- Do not use raw `div`/`span` component rewrites where an existing BaseLib/CanvasLib wrapper should be improved or reused.
- Do not add marketing-style UI or explanatory in-app text.
- Do not let timers or subscriptions leak after component disposal.
- Do not trigger full page rerenders for every small observation update.

## Acceptance Checklist

- Existing Processes tabs still render and function.
- Runs/Analytics lazy loading behavior is preserved.
- Active run summaries do not reload full details for every active run.
- Dialogs load details only when opened.
- High-count lists affected by the change are bounded or virtualized.
- Component tests pass.
- Browser screenshots show no overlap or broken layout on large and narrow viewports.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspace"`
- Targeted integration tests from `01-scope-inventory.md`.
- Browser proof on `/processes`:
  - maximized large-screen pass
  - narrower-width follow-up
  - open Runs tab
  - open at least one details dialog
  - verify no broken layout, overlap, empty high-count region, or stale spinner
  - capture screenshots into the execution report

## Browser Validation Logging

- Target route: `/processes`
- Required viewports: large desktop/maximized and a narrower responsive width.
- Required actions: navigate, wait for page stability, open Runs, exercise refresh-visible area, open a detail dialog, close dialog, verify no console/runtime errors if available.
- Evidence paths: record screenshots such as `evidence/processes-observation-desktop.png` and `evidence/processes-observation-narrow.png`.
- Review questions: Are lists bounded? Are buttons reachable? Do dialogs load only on demand? Are active states and stale/error states visible without covering other content?

## Progression Gate

- Downstream subbundles may continue only when component tests and browser proof pass, and the execution report confirms the migrated UI slice preserves existing behavior and uses the observation boundary instead of direct ad hoc composition.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
