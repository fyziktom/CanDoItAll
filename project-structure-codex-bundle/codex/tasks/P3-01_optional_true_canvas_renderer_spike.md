# P3-01 — Optional true-canvas renderer spike

## Goal

Benchmark an actual canvas renderer only after the current architecture is stabilized and measured.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P1-01, P1-02, P1-03, P2-01, P2-02

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/**`
- `tests/CanDoItAll.Tests.Playwright/**`

## Likely impacted feature IDs

- F05 — Quick create and grouped create actions from toolbox/context menu/selection flows
- F10 — Link mode, reconnect, and disconnect workflows
- F11 — Inline managed attachment preview in selection panel
- F12 — Attachment preview modal for PDF/video/audio/document
- F21 — Export image from selected hierarchy / mindmap capture
- F28 — Create selection border and clear selection borders
- F31 — Inline note creation and editing from the canvas
- F32 — Compact path and subtype-specific mapping for repository/file nodes
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- All user-facing features proven earlier
- Image export
- Accessibility overlay strategy

## Existing tests most likely to be relevant

- `Prompt_flow_context_actions_include_wizard_and_create_tools`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Markdown_create_definition_keeps_text_fields_and_file_upload_enabled`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Pdf_attachment_nodes_render_inline_preview_and_open_modal_without_navigation`
- `Audio_attachment_nodes_render_audio_preview_and_local_open_action_when_host_supports_it`
- `Double_clicking_pdf_attachment_nodes_keeps_preview_modal_behavior`
- `Project_structure_export_image_capture_generates_i18_artifacts`
- `Edit_actions_open_prefilled_canvas_composer_for_supported_nodes`
- `Edit_create_actions_update_existing_nodes_and_refresh_selection_panel`
- `File_backed_nodes_map_compact_path_payload_with_promoted_file_name`

## Implementation expectations

1. Build an isolated benchmark scene and a narrow prototype for nodes, links, and hit testing.
2. Compare retained DOM/SVG vs true canvas on the same graph tiers.
3. Document trade-offs: accessibility, text measurement, export path, and overlay integration.

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

- [ ] A go/no-go decision is backed by measured evidence, not intuition.

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
