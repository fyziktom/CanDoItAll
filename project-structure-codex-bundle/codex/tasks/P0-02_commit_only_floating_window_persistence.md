# P0-02 — Commit-only floating-window persistence

## Goal

Keep floating-window drag/resize local in JS and persist geometry only on commit or idle.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P0-01

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvas-floating-window.js`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `tests/CanDoItAll.Tests.Components/CanvasFloatingWindowTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `tests/CanDoItAll.Tests.Playwright/PromptLibraryVerificationTests.cs`

## Likely impacted feature IDs

- F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- F02 — Toolbox floating window open/minimize/hide/drag/restore
- F06 — Health floating window with counts, spotlight, and validate-selected action
- F30 — Persisted selection, zoom/pan view state, and window state restoration
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes

## Must preserve

- Visible/hidden/minimized/normalized states
- Drag-to-reposition and resize
- PromptFactory floating windows
- Health/toolbox/selection windows remembering final placement

## Existing tests most likely to be relevant

- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Expanded_window_renders_icon_only_actions_with_accessible_labels`
- `Minimized_window_renders_expand_and_hide_icons_without_text_labels`
- `Blocked_validation_nodes_surface_annotations`
- `Summary_counts_blocked_review_and_priority_nodes`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas`
- `Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`

## Implementation expectations

1. Differentiate live geometry updates from committed geometry updates in the JS bridge.
2. Publish only final geometry after pointerup, minimize, restore, normalize, or hide.
3. Avoid `PersistCanvasUiStateAsync` on every geometry pulse.
4. Optionally keep a lightweight local geometry sync for the Blazor markup without server persistence until commit.

## Task-specific validation checklist

### Required tests
- Rerun the directly relevant component tests.
- Rerun the directly relevant Playwright tests.
- If any shared-canvas file changed, rerun PromptFactory browser coverage too.
- Add missing coverage where this task touches a weakly covered feature.

### Required screenshots or browser evidence
- Capture screenshots for every visible state touched by the task.
- Save artifacts in the normal Playwright artifact location used by the repository.
- Compare overlay behavior before/after when this task affects pointer or wheel routing.

### Required performance evidence
- Use renderer or service counters relevant to this task.
- Record at least one before/after note for any hot-path optimization claim.
- A task that claims performance improvement without counters or measurements is incomplete.

## Acceptance gates

- [ ] Zero `SaveViewStateAsync` calls while actively dragging or resizing a floating window.
- [ ] Exactly one persisted state update after drag/resize commit.
- [ ] PromptFactory floating toolbox still drags and restores correctly.

## Explicit failure examples

- a preserved feature disappears,
- PromptFactory breaks after a shared-canvas change,
- screenshots show overlay breakage,
- DB writes still happen in the interaction path that this task was supposed to fix,
- retained-renderer counters do not change on a task that was supposed to reduce full rebuilds.

## Completion note template

When this task is done, report:
- impacted feature IDs,
- files changed,
- tests rerun,
- browser scenarios rerun,
- screenshots/artifacts captured,
- counter or performance evidence,
- any intentionally deferred follow-up.
