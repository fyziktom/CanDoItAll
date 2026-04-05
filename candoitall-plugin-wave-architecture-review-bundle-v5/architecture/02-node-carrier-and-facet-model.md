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

## Executed SB02 Ownership Split

Current execution keeps the existing `ProjectObjectRecord` schema physically in place for migration safety, but canonical ownership is now explicit:

- Carrier-owned in `Workbench_ProjectObjects`:
  - identity and project scope
  - node kind and subtype
  - title, subtitle, notes, status
  - progress mode and percent
  - semantic marker columns and priority
  - parent relation
  - canonical `X/Y`
  - schedule anchors and duration
  - timestamps
- Binding-owned in `Workbench_ProjectNodeBindings`:
  - route
  - external artifact kind and artifact id
  - managed media path, content type, and original file name
  - storage object reference payload
- Reference-owned in `Workbench_ProjectNodeReferences`:
  - meeting participant ids
  - recording/transcript cross-node references
  - transcript provider profile ids
  - participant/work-item/repository/environment/infrastructure foreign-owner ids

`ProjectNodeBindingStorage` is the transitional adapter. It normalizes legacy carrier rows on read, persists the binding/reference split on write, rehydrates the current DTO surface for callers, and rejects any sanitized metadata payload that still tries to retain foreign-owner references.

## Guardrails

- No new foreign-owner ids inside Workbench metadata.
- No new plugin payload inside generic metadata without a reviewed plugin-facet contract.
- No new carrier field may be added unless it is explicitly justified as durable node semantics.
