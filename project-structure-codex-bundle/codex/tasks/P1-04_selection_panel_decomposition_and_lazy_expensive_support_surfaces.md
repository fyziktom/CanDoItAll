# P1-04 — Selection-panel decomposition and lazy expensive support surfaces

## Goal

Reduce the Razor render tree and compute heavy overlay sections only when needed.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P0-06

## Primary files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.SelectionPanel.cs`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely impacted feature IDs

- F06 — Health floating window with counts, spotlight, and validate-selected action
- F07 — Selection window empty state
- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F09 — Single-select detail view with badges, quick signals, node actions, and advanced details
- F11 — Inline managed attachment preview in selection panel
- F12 — Attachment preview modal for PDF/video/audio/document
- F13 — Quick action dialog on double-click/open with node-specific actions
- F14 — Prompt flow nodes expose wizard navigation in inspector and quick action modal
- F15 — Project/subproject/shared-parent nodes open related structure in a new tab
- F16 — Add subproject dialog from selected root project
- F17 — Reconnect parent dialog from selected subproject
- F18 — Summary modal with inline status edits
- F19 — Export summary workbook (XLSX/CSV-backed behavior)
- F20 — Export Gantt from summary
- F22 — Transcript provider confirmation dialog and transcript actions
- F23 — Mermaid viewer modal with diagram type detection
- F24 — Delete confirmation flow
- F25 — Runtime launch actions (PowerShell/admin) and launch feedback
- F26 — Outline list selection sync below the canvas
- F27 — Graph health support card below the canvas
- F31 — Inline note creation and editing from the canvas
- F32 — Compact path and subtype-specific mapping for repository/file nodes

## Must preserve

- Selection window single-select and multi-select behavior
- All modal dialogs and advanced details
- Runtime launch, transcript, preview, and mermaid actions

## Existing tests most likely to be relevant

- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Blocked_validation_nodes_surface_annotations`
- `Summary_counts_blocked_review_and_priority_nodes`
- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order`
- `File_selection_panel_uses_semantic_badges_and_suppresses_duplicate_type_metadata`
- `Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation`
- `Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it`
- `Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior`
- `Double_clicking_prompt_flow_nodes_opens_quick_action_modal_and_wizard_new_tab_action`
- `Double_clicking_launchable_runtime_nodes_opens_quick_action_modal_and_runs_powershell`

## Implementation expectations

1. Extract single-select and multi-select panels into focused components or render fragments with stable parameters.
2. Lazy-render expensive detail sections and support panels only when visible.
3. Keep the selection window as HTML/Blazor, but minimize page-level recomputation.

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

- [ ] Selection UI remains feature-complete.
- [ ] Unrelated viewport changes do not force large overlay recomputation.

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
