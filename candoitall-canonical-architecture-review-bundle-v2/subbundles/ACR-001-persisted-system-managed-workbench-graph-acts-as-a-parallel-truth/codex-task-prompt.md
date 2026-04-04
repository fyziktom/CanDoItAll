# Codex task prompt — ACR-001

Implement finding `ACR-001` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 2`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

ProjectWorkbenchService still synchronizes Projects, Resources, Factory, Validation, and TestLab canonical entities into persisted ProjectObjectRecord / ProjectObjectLinkRecord rows, and structure/calendar/Gantt reads still flow through that synced copy. The new CRM/HR overlays would now stack on top of a graph that is already a parallel truth.

## Ordered implementation steps

- Introduce `CanonicalGraphAssembler` that reads module-native canonical owners plus workbench-native nodes and canonical actor assignments.
- Define `AssembledProjectGraph`, `AssembledNode`, and `AssembledEdge` with an explicit backing/origin marker such as `WorkbenchNative` vs `ExternalProjection`.
- Switch structure, calendar, summary, and Gantt builders to consume assembler output instead of persisted system-managed workbench rows.
- If a cache is still needed, rebuild it from assembler output and mark it non-authoritative and disposable.

## Guardrails

- Do not migrate every upstream module into workbench-owned truth in this phase.
- Do not let a cache or DTO become the new write model.
- Keep actor assignments attached through explicit graph assembly, not through ad hoc metadata rewrites.

## Done means

- Structure, calendar, and Gantt can be built from one assembled graph without reading persisted system-managed rows as truth.
- If a cache remains, deleting/rebuilding it does not change canonical outcomes.
- Actor overlays and cross-module party context are derived from canonical owners, not from duplicated synced records.
