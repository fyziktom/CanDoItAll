# P0-06 — Runtime surface cleanup and support/demo separation

## Goal

Slim the runtime page and clearly separate production authoring UI from support/demo cards.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

None

## Primary files

- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Likely impacted feature IDs

- F01 — Toolbar toggles for Blocks, Health, and Inspector/Selection surfaces
- F06 — Health floating window with counts, spotlight, and validate-selected action
- F26 — Outline list selection sync below the canvas
- F27 — Graph health support card below the canvas

## Must preserve

- Outline access if product still wants it
- Graph health visibility if product still wants it
- All dialogs and floating windows

## Existing tests most likely to be relevant

- `Renders_selection_window_and_toolbar_toggles_without_stage_inspector_column`
- `Health_window_toggle_restores_the_default_offset_that_keeps_the_toolbox_clear`
- `Project_structure_feedback_fixes_are_validated_in_browser`
- `Blocked_validation_nodes_surface_annotations`
- `Summary_counts_blocked_review_and_priority_nodes`
- `Renders_shared_structure_workbench_and_updates_inspector_from_outline_selection`
- `Project_structure_artifacts_capture_required_canvas_evidence`

## Implementation expectations

1. Move CanvasBoundaryCard demo sections behind a debug/sandbox flag or remove them from runtime.
2. Decide whether Outline and Graph Health belong in runtime, in a collapsible support area, or in diagnostics-only mode.
3. Keep runtime markup focused on authoring, overlays, and essential support surfaces.

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

- [ ] ProjectStructure runtime page no longer renders always-on demo cards.
- [ ] User-facing runtime behavior is clearer and lighter.
- [ ] Any moved support functionality remains reachable where intended.

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
