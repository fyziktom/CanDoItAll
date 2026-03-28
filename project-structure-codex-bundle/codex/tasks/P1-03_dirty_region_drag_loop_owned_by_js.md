# P1-03 — Dirty-region drag loop owned by JS

## Goal

Keep drag, pan, guides, and affected links entirely in JS with minimal patch scope.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P1-01

## Primary files

- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/canvasWorkbenchInterop.js`

## Likely impacted feature IDs

- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F10 — Link mode, reconnect, and disconnect workflows
- F28 — Create selection border and clear selection borders
- F29 — Adopt moved nodes into borders after drag
- F31 — Inline note creation and editing from the canvas

## Must preserve

- Snap guides
- Multi-node drag
- Frame drag
- Selection marquee

## Existing tests most likely to be relevant

- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Edit_actions_open_prefilled_canvas_composer_for_supported_nodes`
- `Edit_create_actions_update_existing_nodes_and_refresh_selection_panel`

## Implementation expectations

1. Track dirty node IDs, dirty link IDs, and dirty guide overlays during drag.
2. Patch only the changed scene objects during active interaction.
3. Defer expensive recalculation until drop/commit where possible.

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

- [ ] Active drag updates only moved nodes, affected links, and active guides.
- [ ] Guide rendering stays correct while render cost drops materially.

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
