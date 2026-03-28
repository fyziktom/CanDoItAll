# P2-01 — Scene patch protocol and plain-JS modularization

## Goal

Make the JS layer maintainable without TypeScript or a new bundler requirement.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P1-01, P1-02, P1-03

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/*.js`

## Likely impacted feature IDs

- F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- F02 — Toolbox floating window open/minimize/hide/drag/restore
- F03 — Toolbox search filters standard block actions
- F04 — Toolbox accordion group expand/collapse
- F05 — Quick create and grouped create actions from toolbox/context menu/selection flows
- F10 — Link mode, reconnect, and disconnect workflows
- F21 — Export image from selected hierarchy / mindmap capture
- F30 — Persisted selection, zoom/pan view state, and window state restoration
- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- Public `window.CanDoItAll.canvasWorkbench` API
- Current JS entry points used by tests and Blazor

## Existing tests most likely to be relevant

- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Expanded_window_renders_icon_only_actions_with_accessible_labels`
- `Minimized_window_renders_expand_and_hide_icons_without_text_labels`
- `Prompt_flow_context_actions_include_wizard_and_create_tools`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Markdown_create_definition_keeps_text_fields_and_file_upload_enabled`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Project_structure_export_image_capture_generates_i18_artifacts`
- `Persisted_multi_select_state_renders_common_actions_in_selection_window`

## Implementation expectations

1. Keep plain JavaScript only.
2. Refactor the giant interop file into clearly owned internal modules or adjacent JS files with a stable public API.
3. Separate state store, overlay guards, renderer, viewport math, scene patching, diagnostics, and command API.
4. Do not introduce TypeScript or a heavy build chain.

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

- [ ] Public API stays stable or is migrated with tests in the same task.
- [ ] Hot-path JS is easier to reason about and code ownership is explicit.

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
