# T06 — CanvasLib directory reorganization skeleton

## Phase
P1

## Goal
Create a maintainable directory structure for CanvasLib so runtime workbench, shared overlays, preview boundary components, and calendar code are clearly separated.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T00

## Primary files
- `src/CanDoItAll.Components.CanvasLib/**`
- `src/CanDoItAll.ComponentKit/**`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`

## Feature IDs that must remain green
F33, F34, F39, F40

## Implementation checklist
- Create the new CanvasLib folder skeleton without breaking public namespace expectations.
- Move or alias runtime, shared, preview, and calendar code into their target folders.
- Document the new structure inside CanvasLib.
- Keep ComponentKit explicitly out of the active runtime refactor path.

## Validation
- All moved files still compile and namespaces remain stable for consumers.
- Preview boundary components used by PromptFactory continue to render.
- The new folder tree clearly separates runtime, preview, shared, and calendar code.

## Done when
- CanvasLib has a target structure that reduces ambiguity and future drift.
- The legacy ComponentKit path is explicitly marked compatibility-only and excluded from the active refactor path.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
