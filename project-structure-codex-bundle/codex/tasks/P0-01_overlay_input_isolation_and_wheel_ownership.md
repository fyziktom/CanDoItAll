# P0-01 — Overlay input isolation and wheel ownership

## Goal

Make floating windows, toolbox content, dialogs, popovers, and support overlays fully own their pointer, wheel, focus, and context-menu interactions.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

None

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/Components/CanvasFloatingWindow.razor`
- `tests/CanDoItAll.Tests.Components/CanvasWorkbenchTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Likely impacted feature IDs

- F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- F02 — Toolbox floating window open/minimize/hide/drag/restore
- F03 — Toolbox search filters standard block actions
- F04 — Toolbox accordion group expand/collapse
- F06 — Health floating window with counts, spotlight, and validate-selected action
- F07 — Selection window empty state
- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F09 — Single-select detail view with badges, quick signals, node actions, and advanced details
- F12 — Attachment preview modal for PDF/video/audio/document
- F13 — Quick action dialog on double-click/open with node-specific actions
- F22 — Transcript provider confirmation dialog and transcript actions
- F23 — Mermaid viewer modal with diagram type detection
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- Toolbox toggle and search
- Toolbox accordion expand/collapse
- Context menu on actual nodes and canvas background
- Help/settings overlays
- Floating window drag and hide/minimize
- Selection, pan, and zoom when interacting with the scene itself

## Existing tests most likely to be relevant

- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Expanded_window_renders_icon_only_actions_with_accessible_labels`
- `Minimized_window_renders_expand_and_hide_icons_without_text_labels`
- `Blocked_validation_nodes_surface_annotations`
- `Summary_counts_blocked_review_and_priority_nodes`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order`
- `File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata`
- `Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation`
- `Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior`

## Implementation expectations

1. Expand overlay target detection to include floating windows, toolbar-safe overlays, dialogs, preview modals, and any overlay root carrying a dedicated data attribute.
2. Add wheel ownership guards so overlay scroll regions scroll themselves and only the scene host zooms the canvas.
3. Ensure overlay pointerdown/doubleclick/contextmenu paths do not leak to the scene host.
4. Use tests and browser automation to prove toolbox group expansion and internal scroll are reliable.

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

- [ ] Wheel inside toolbox no longer changes scene zoom.
- [ ] Clicking a toolbox accordion header never starts canvas selection or pan.
- [ ] Right-click inside toolbox/floating-window content never opens the scene context menu.
- [ ] Existing node/canvas context menu behavior still works on the scene.

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
