# 09-workflow-template-and-ui-authoring-pack

## Objective

Expose the new executor capabilities to users through templates and UI authoring.

## Required work

1. Add workflow templates:
   - local folder summary to Markdown report,
   - file diff report,
   - HTTP download + document extraction,
   - JSON transform + project task creation,
   - approval-gated external action.
2. Update `Templates/Workflows/manifest.yaml` seed version.
3. Preserve managed seed behavior and never overwrite user-managed definitions.
4. Update workflow canvas executor catalog:
   - group by executor family,
   - show availability,
   - show approval requirement,
   - show deterministic preview support.
5. Add component tests for UI visibility and settings cards.

## Acceptance checklist

- A user can create practical folder/file workflows from examples.
- Planned/unavailable executors are clearly marked and cannot be mistaken as runnable.
