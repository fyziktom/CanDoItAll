# UI and canvas modeling

The dedicated process designer should continue reusing `CanvasLib` first, but it now needs richer side-panel semantics and a live runtime overlay layer.

## Canvas stays responsible for

- node and edge rendering
- selection, create actions, inline editing, zoom/pan, minimap
- group frames and layout persistence
- inspector or context actions
- runtime overlay rendering once a run is selected

## Process module side panels become responsible for

- process owner / customer / criticality metadata
- role and template selection
- interface contract editing
- step contract editing
- work-brief inspection
- triage/routing explanation
- decision-right and control-tier editing
- exception / variant authoring
- validation warnings and governance warnings
- overlay-to-timeline navigation for live runs

## Visual priorities

Wave 1 can still ship without fancy edge chrome, but the user should already see:

- who owns the process
- whether a step has bad or missing input requirements
- whether a role is unresolved
- whether a process lacks owner/customer/criticality metadata
- and whether an interface is incomplete

Wave 3 should add live execution overlays that let the operator see:

- which steps are active, waiting, blocked, or completed
- who currently owns the baton
- where approval is pending
- where the last handoff happened
- and which wait reason is dominating

## Projection guardrail

Live execution on the canvas is valuable, but the overlay must remain a projection:

- canonical definition semantics stay in process definition data
- canonical mutable run state stays in runtime state + journal
- layout stays in diagram layout data
- live overlay is composed from those sources and may be cached, but does not own them
