# P0-03 — Commit-only canvas state persistence and UI-state ownership cleanup

## Goal

Keep pan/zoom/live viewport state in JS during interaction and persist only the final idle/commit snapshot.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P0-01

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs`

## Likely impacted feature IDs

- F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- F06 — Health floating window with counts, spotlight, and validate-selected action
- F07 — Selection window empty state
- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F09 — Single-select detail view with badges, quick signals, node actions, and advanced details
- F30 — Persisted selection, zoom/pan view state, and window state restoration
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- Restored view state on page reopen
- Selection synchronization for the selection window
- Minimap and diagnostics toggles
- Keyboard shortcuts and toolbar zoom controls

## Existing tests most likely to be relevant

- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Blocked_validation_nodes_surface_annotations`
- `Summary_counts_blocked_review_and_priority_nodes`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order`
- `File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata`
- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas`
- `Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`
- `Workbench_renders_toolbar_hint_and_help_overlay`

## Implementation expectations

1. Split high-frequency live state from persisted snapshot state.
2. Ensure `OnStateChanged` does not trigger DB writes during active pan/zoom/drag.
3. For ProjectStructure, define domain node X/Y as the persisted source of truth and treat UI `ManualPositions` as transient/local-only for drag in progress.
4. Keep selected IDs mirrored only as often as the overlay UI actually needs them.

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

- [ ] No `SaveViewStateAsync` during active pan/zoom.
- [ ] No `RefreshCanvasSurface()` triggered by pure viewport movement.
- [ ] ProjectStructure drag no longer persists both domain X/Y and long-lived UI manual positions.

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
