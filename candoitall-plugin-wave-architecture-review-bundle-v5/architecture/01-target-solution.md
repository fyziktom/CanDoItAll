# Target Solution

## Architectural Thesis

Node should remain the **universal carrier** of project meaning.

However, the carrier must not remain a giant everything-box.

The stable target is:

- **node carrier** for durable project semantics
- **typed facets** for kind/family-specific behavior and payload
- **explicit bindings** for foreign canonical owners (artifacts, resources, provider profiles, directory parties, plugin connectors, etc.)
- **assembled projections** for cross-module read models
- **lifecycle history** for semantic evolution

## What stays canonical on the node carrier

The following belong to the carrier because they express durable project meaning:

- node identity and project scope
- parent relationship / hierarchy anchor
- canonical node-kind key
- title, subtitle, notes / primary text
- X/Y coordinates because spatial placement in the mindmap is semantically meaningful
- semantic marker sets because markers participate in project meaning and future cross-analysis
- schedule anchors (start/end/duration) where applicable
- minimal status/progress fields if they truly remain cross-kind semantics
- timestamps and authorship/origin metadata

## What should not stay on the carrier

The following should move behind explicit facet/binding contracts:

- external artifact kind/id
- route as a primary truth field
- media file details and storage reference payloads
- provider/resource/account/secret references
- plugin-specific payloads
- rich cross-module ownership state

## Projection Discipline

Workbench should no longer persist a mirrored graph of other modules inside the same canonical node tables.

Preferred model:

- canonical editable nodes are persisted in the carrier/facet/binding model
- read-only module contributors assemble additional projection nodes/edges when building the surface
- if persistence is needed for performance, use clearly named read-model tables that are **not** canonical node storage

## Lifecycle Discipline

Reclassification should preserve node identity while creating explicit transition history.

- same-family change -> mutate facet with history event
- cross-family change -> archive old facet snapshot, create new facet instance, write history event
- note -> task / decision / other richer forms must remain a first-class lifecycle

## Plugin Platform Direction

New integrations should register through plugin/connector manifests and registries, not by expanding enums and switches.

A plugin should describe:

- unique kind/key
- schema version and config schema
- secret requirements
- health/test contract
- agent capability exposure
- optional node-kind/facet hooks
- optional UI/editor descriptors

## Explicit Non-Goals

- no big-bang rewrite of all existing UI DTOs in one wave
- no removal of the mindmap-first workflow
- no demotion of semantic X/Y and markers into mere view-state
