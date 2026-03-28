# P3-02 — Optional shared-library consolidation

## Goal

Retire or intentionally isolate the duplicated canvas component trees after the main fixes are complete.

## Why this task exists

This task addresses one or more verified hotspots from `05_PERFORMANCE_HOTSPOTS.md` and is part of the required order in `04_PHASED_EXECUTION_PLAN.md`.

## Dependencies

P2-01

## Primary files

- `src/CanDoItAll.ComponentKit/**`
- `src/CanDoItAll.Components.CanvasLib/**`

## Likely impacted feature IDs

- F33 — PromptFactory still works after shared CanvasWorkbench/CanvasFloatingWindow changes
- F34 — Sandbox canvas page still works after shared canvas changes

## Must preserve

- All runtime consumers using CanvasLib
- Any still-needed ComponentKit consumers

## Existing tests most likely to be relevant

- `Prompt_factory_canvas_surface_loads_and_exposes_shared_chrome`
- `Prompt_library_catalog_is_exhaustively_available_from_prompt_gallery_and_factory_canvas`
- `Prompt_factory_artifacts_capture_toolbox_preview_and_single_add_flow`
- `Workbench_renders_toolbar_hint_and_help_overlay`
- `Workbench_uses_settings_icon_and_marks_settings_overlay_with_toolbar_safe_modifier`

## Implementation expectations

1. Inventory actual consumers, remove dead duplication, and keep one canonical canvas stack.
2. Do not attempt this early in the program.

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

- [ ] There is one clearly canonical shared canvas implementation path or an explicitly documented reason for temporary duality.

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
