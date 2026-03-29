# T09 — Preview-boundary isolation and legacy path quarantine

## Phase
P1

## Goal
Keep the preview/boundary components that PromptFactory and Sandbox rely on, but move them out of the active runtime path and stop pretending they are the runtime renderer. Also quarantine legacy CanvasLib duplicates.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T06, T07, T08

## Primary files
- `src/CanDoItAll.Components.CanvasLib/Components/**`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/*.js`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `src/CanDoItAll.ComponentKit/**`

## Feature IDs that must remain green
F33, F34, F39, F40

## Implementation checklist
- Relocate preview-boundary components and scripts under a clearly named preview area.
- Remove them from the conceptual runtime path and asset loading path where safe.
- Keep PromptFactory support lane working and update tests accordingly.
- Leave clear compatibility notes for any legacy or preview-only asset that remains public.

## Validation
- PromptFactory preview support lane still works after relocation or namespace cleanup.
- Preview boundary scripts are no longer loaded as if they were part of the runtime scene engine unless actually needed.
- ComponentKit remains clearly marked as legacy/compatibility-only.

## Done when
- Runtime renderer code and preview-boundary demo code are no longer mixed together conceptually.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
