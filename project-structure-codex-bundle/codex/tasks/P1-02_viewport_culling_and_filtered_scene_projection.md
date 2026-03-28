# P1-02 — Viewport culling and filtered scene projection

## Goal

Render only what matters for the current viewport and interaction context.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P1-01

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`

## Likely impacted feature IDs

- F05 — Quick create and grouped create actions from toolbox/context menu/selection flows
- F10 — Link mode, reconnect, and disconnect workflows
- F21 — Export image from selected hierarchy / mindmap capture
- F28 — Create selection border and clear selection borders
- F31 — Inline note creation and editing from the canvas
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- Focus node / ensure visible behavior
- Minimap accuracy
- Selection of off-screen nodes
- Links for visible and near-visible nodes

## Existing tests most likely to be relevant

- `Prompt_flow_context_actions_include_wizard_and_create_tools`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Markdown_create_definition_keeps_text_fields_and_file_upload_enabled`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Project_structure_export_image_capture_generates_i18_artifacts`
- `Edit_actions_open_prefilled_canvas_composer_for_supported_nodes`
- `Edit_create_actions_update_existing_nodes_and_refresh_selection_panel`
- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas`
- `Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`
- `Workbench_renders_toolbar_hint_and_help_overlay`

## Implementation expectations

1. Compute viewport bounds and only mount/update visible nodes plus a small overscan margin.
2. Filter links and frames based on visible endpoint membership or viewport intersection.
3. Keep selection and keyboard behavior correct even when not every node is mounted.

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

- [ ] Rendered visible node count is materially smaller than total node count on large graphs.
- [ ] Selection/focus still works when selected nodes move into or out of view.

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
