# Implementation plan

## Remediation goal

Create separate structure, calendar, timeline/Gantt, and summary builders that consume one assembled canonical graph plus explicit workbench overlays.

## Ordered steps

- Build Gantt/calendar/timeline builders over the assembled graph and schedule facet, not over persisted workbench rows.
- Make schedule ownership explicit so nodes without schedule facets cannot accidentally appear as scheduled items unless projected intentionally.
- Backfill projection equivalence tests proving the new builders match canonical expectations.
- Keep Mermaid/Gantt exports derived and disposable.

## Guardrails

- Do not let calendar or Gantt become hidden write models.
- Do not embed ad hoc actor-resolution logic in each builder.

## Acceptance criteria

- Structure, calendar, and Gantt are all derived from one assembled graph.
- Schedule, dependency, and assignment overlays are consistent across all projection builders.
