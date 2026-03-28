# P0-05 — Avoid full surface reloads after simple mutations

## Goal

Stop calling the heavyweight structure reload path for every non-structural change.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P0-04

## Primary files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely impacted feature IDs

- F05 — Quick create and grouped create actions from toolbox/context menu/selection flows
- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F10 — Link mode, reconnect, and disconnect workflows
- F16 — Add subproject dialog from selected root project
- F17 — Reconnect parent dialog from selected subproject
- F18 — Summary modal with inline status edits
- F19 — Export summary workbook (XLSX/CSV-backed behavior)
- F20 — Export Gantt from summary
- F21 — Export image from selected hierarchy / mindmap capture
- F24 — Delete confirmation flow
- F28 — Create selection border and clear selection borders
- F29 — Adopt moved nodes into borders after drag
- F30 — Persisted selection, zoom/pan view state, and window state restoration
- F31 — Inline note creation and editing from the canvas

## Must preserve

- Status/progress/marker/priority updates
- Note editing
- Quick action feedback
- Summary status editing
- Delete/create/link flows

## Existing tests most likely to be relevant

- `Prompt_flow_context_actions_include_wizard_and_create_tools`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Markdown_create_definition_keeps_text_fields_and_file_upload_enabled`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Selected_root_project_can_add_subproject_from_the_selection_panel`
- `Selected_subproject_can_reconnect_parent_from_the_selection_panel`
- `Selected_nodes_with_children_open_summary_modal_and_show_export_actions`
- `Project_structure_artifacts_capture_required_canvas_evidence`
- `Project_structure_export_image_capture_generates_i18_artifacts`
- `Selected_nodes_render_advanced_details_and_keep_delete_last_in_action_order`

## Implementation expectations

1. Introduce a distinction between view-only updates, node-property updates, and structural graph updates.
2. Patch local surface state for simple node-property changes instead of calling `ReloadSurfaceAsync()`.
3. Reserve `GetStructureAsync()+SyncGraphAsync()` for true structural invalidation, hierarchy changes, or cases that genuinely require system-managed node recomputation.
4. Optionally add service methods that return updated node DTOs for local patching.

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

- [ ] Status/progress/marker/priority changes no longer force full structure reloads.
- [ ] Inline note edit no longer needs a full reload when only the note node changed.
- [ ] Create/delete/link flows still end in consistent graph state.

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
