# P0-04 — Batch node-move persistence

## Goal

Persist multi-node drag as a single mutation and a single save transaction.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P0-03

## Primary files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely impacted feature IDs

- F08 — Multi-select common actions, batch status/progress/marker/priority, focus-first, clear-selection
- F10 — Link mode, reconnect, and disconnect workflows
- F28 — Create selection border and clear selection borders
- F29 — Adopt moved nodes into borders after drag
- F30 — Persisted selection, zoom/pan view state, and window state restoration

## Must preserve

- Single-node drag
- Multi-node drag
- Selection retention after drop
- Border adoption behavior after drop

## Existing tests most likely to be relevant

- `Persisted_multi_select_state_renders_common_actions_in_selection_window`
- `Project_nodes_use_project_specific_actions_instead_of_generic_graph_mutations`
- `Structure_canvas_supports_inline_note_creation_editing_and_context_create_dialogs`
- `Group_context_actions_expose_border_and_shared_status_tools`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`

## Implementation expectations

1. Introduce a batched move service method that accepts all moved positions in one request.
2. Perform one `SaveChangesAsync` call for the move batch.
3. Return enough information for the page to patch local state or reload only once if required.
4. Make border adoption run after the move commit without turning the move path back into N operations.

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

- [ ] Multi-node drag produces one service call and one DB save transaction.
- [ ] Drag commit keeps selected nodes selected.
- [ ] Moved-node border adoption still behaves correctly.

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
