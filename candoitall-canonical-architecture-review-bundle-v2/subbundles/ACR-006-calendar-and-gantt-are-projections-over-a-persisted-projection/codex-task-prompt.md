# Codex task prompt — ACR-006

Implement finding `ACR-006` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 2`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

Calendar and Gantt do not build directly from canonical owners; they depend on workbench structure output that itself depends on SyncGraphAsync. CRM/HR-linked scheduling and ownership overlays would therefore be computed over a projection chain rather than one graph truth.

## Ordered implementation steps

- Build Gantt/calendar/timeline builders over the assembled graph and schedule facet, not over persisted workbench rows.
- Make schedule ownership explicit so nodes without schedule facets cannot accidentally appear as scheduled items unless projected intentionally.
- Backfill projection equivalence tests proving the new builders match canonical expectations.
- Keep Mermaid/Gantt exports derived and disposable.

## Guardrails

- Do not let calendar or Gantt become hidden write models.
- Do not embed ad hoc actor-resolution logic in each builder.

## Done means

- Structure, calendar, and Gantt are all derived from one assembled graph.
- Schedule, dependency, and assignment overlays are consistent across all projection builders.
