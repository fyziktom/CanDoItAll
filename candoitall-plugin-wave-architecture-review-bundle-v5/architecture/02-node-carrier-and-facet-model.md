# Node Carrier and Facet Model

## Recommended Persistence Shape

Transitional target (names illustrative, not mandatory):

- `Workbench_ProjectNodes` -> stable carrier
- `Workbench_ProjectNodeRelations` -> semantic graph edges only
- `Workbench_ProjectNodeLifecycleEvents` -> reclassification / transition history
- `Workbench_ProjectNodeBindings` -> foreign canonical owners and artifact/resource/provider bindings
- `Workbench_ProjectNodeFacets_*` -> typed kind-family payload tables
- `Workbench_ViewStates` -> ephemeral UI state only
- `Workbench_ComposedProjection*` -> optional read-model tables if projection persistence is needed

## Why this fits the product

This model respects the way the product is used:

- users think in mindmap nodes first
- nodes often start as rough notes and later harden into richer structured objects
- spatial placement and markers matter for analysis, not just for drawing

So the answer is **not** “node is only a view”.
The answer is “node is the stable carrier, while rich behavior hangs off the carrier through governed facets and bindings”.

## Minimal Migration Strategy

1. Introduce the carrier/facet/binding tables without changing the public surface DTO.
2. Add read adapters that still feed the existing `ProjectStructureNode` shape.
3. Migrate one family at a time (participant, work item, repository, etc.).
4. After families are migrated, remove old carrier overload fields.

## Guardrails

- No new foreign-owner ids inside Workbench metadata.
- No new plugin payload inside generic metadata without a reviewed plugin-facet contract.
- No new carrier field may be added unless it is explicitly justified as durable node semantics.
