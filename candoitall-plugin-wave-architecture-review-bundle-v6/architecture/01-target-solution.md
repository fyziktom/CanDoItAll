# Target Solution

## Architectural Thesis

Node should remain the **universal carrier** of project meaning.

However, the carrier must stop being a giant everything-box.

The stable target is:

- **node carrier** for durable project semantics
- **typed facets** for kind/family-specific behavior and payload
- **explicit bindings** for foreign canonical owners (artifacts, resources, provider profiles, storage items, connector accounts, secrets, and similar references)
- **assembled projections** for foreign-module read-only surfaces
- **lifecycle history** for semantic evolution
- **capability registry** for commands, assignment roles, and allowed relations

## What stays canonical on the node carrier

The following belong to the carrier because they express durable project meaning:

- node identity and project scope
- parent relationship / hierarchy anchor
- canonical node-kind key
- title, subtitle, notes / primary text
- X/Y coordinates because spatial placement in the mindmap is semantically meaningful
- semantic markers because markers participate in project meaning and future cross-analysis
- schedule anchors (start/end/duration) where applicable
- minimal status / priority fields only if they remain cross-kind semantics
- authorship, origin, and timestamps

## What should move out of the carrier

The following should move behind explicit facet or binding contracts:

- external artifact kind/id
- route as canonical truth
- media file details and storage payload
- provider/resource/account/secret references
- plugin-specific payload
- reusable foreign-owner identifiers

## Projection discipline

Workbench must stop persisting a mirrored graph of other modules inside the same canonical node tables.

Preferred model:

- canonical editable nodes are persisted in the carrier/facet/binding model
- read-only module contributors assemble additional surface nodes/edges
- if persistence is needed for performance, use clearly named read-model tables that are **not** canonical node storage

## Lifecycle discipline

Reclassification should preserve node identity while creating explicit transition history.

- same-family change -> version active facet and write history event
- cross-family change -> supersede old facet, create new active facet, write history event
- note -> task / decision / richer operational form must remain a first-class lifecycle

## Explicit non-goals

- no big-bang rewrite of every DTO in one phase
- no removal of the mindmap-first workflow
- no demotion of semantic X/Y and markers into mere view-state
