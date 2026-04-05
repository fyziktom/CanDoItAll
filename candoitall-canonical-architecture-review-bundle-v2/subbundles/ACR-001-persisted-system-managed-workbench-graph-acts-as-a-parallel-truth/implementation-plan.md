# Implementation plan

## Remediation goal

Introduce one assembled canonical graph for reads and projections, built from upstream canonical owners + workbench-owned custom nodes + canonical actor assignments/facets. If a persisted cache remains, mark it explicitly non-authoritative.

## Ordered steps

- Introduce `CanonicalGraphAssembler` that reads module-native canonical owners plus workbench-native nodes and canonical actor assignments.
- Define `AssembledProjectGraph`, `AssembledNode`, and `AssembledEdge` with an explicit backing/origin marker such as `WorkbenchNative` vs `ExternalProjection`.
- Switch structure, calendar, summary, and Gantt builders to consume assembler output instead of persisted system-managed workbench rows.
- If a cache is still needed, rebuild it from assembler output and mark it non-authoritative and disposable.

## Guardrails

- Do not migrate every upstream module into workbench-owned truth in this phase.
- Do not let a cache or DTO become the new write model.
- Keep actor assignments attached through explicit graph assembly, not through ad hoc metadata rewrites.

## Acceptance criteria

- Structure, calendar, and Gantt can be built from one assembled graph without reading persisted system-managed rows as truth.
- If a cache remains, deleting/rebuilding it does not change canonical outcomes.
- Actor overlays and cross-module party context are derived from canonical owners, not from duplicated synced records.
