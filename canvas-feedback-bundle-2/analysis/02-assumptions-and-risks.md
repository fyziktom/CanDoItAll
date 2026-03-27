# Assumptions And Risks

## Assumptions

- The feedback applies to the shared project structure workbench, not a second hidden canvas implementation.
- Moving preview dialogs into the `CanvasWorkbench` overlay slot is acceptable for summary and Mermaid dialogs too, because they share the same layering problem and backdrop styling.
- The bundle can stay in four workstreams because each note maps to one primary implementation area.

## Risks

- shared canvas CSS changes can affect other canvas pages if selectors are too broad
- moving modal markup can affect focus, click-to-close, or scrolling if the backdrop behavior changes
- catalog-definition changes can accidentally change other file create flows if copied carelessly

## Mitigation

- keep selectors scoped to the existing shared classes
- update only the markdown definition, not the generic file definition
- reuse the existing preview dialog markup and handlers rather than inventing a second dialog
