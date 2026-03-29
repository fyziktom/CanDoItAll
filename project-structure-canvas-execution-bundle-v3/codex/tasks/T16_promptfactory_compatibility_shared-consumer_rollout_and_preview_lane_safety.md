# T16 — PromptFactory compatibility, shared-consumer rollout, and preview lane safety

## Phase
P3

## Goal
Make sure PromptFactory still works after the shared CanvasWorkbench migration and apply any needed state-commit or overlay fixes there too. Keep preview-boundary support cards functional but clearly separate from the runtime renderer.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T02, T06, T07, T08, T10, T11, T12, T13, T14, T15

## Primary files
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.Modules.Factory/wwwroot/js/promptFactoryInterop.js`
- `tests/CanDoItAll.Tests.Components/PromptFactoryPageTests.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactoryCatalogToolboxTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F05, F13, F30, F33, F39

## Implementation checklist
- Validate PromptFactory against the new shared renderer and commit-only state flow.
- Preserve support-lane preview boundary surfaces.
- Update shared tests and browser artifacts for PromptFactory.
- Fix any shared-canvas regressions before cleanup continues.

## Validation
- PromptFactory canvas still loads, selects nodes, persists UI state correctly, and exposes its toolbox/support lane.
- Preview boundary components still render in the support lane.
- Shared-canvas changes do not regress PromptFactory screenshots or artifact capture.

## Done when
- CanvasLib changes are safe for both ProjectStructurePage and PromptFactoryPage.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
