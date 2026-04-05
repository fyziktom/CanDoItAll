# Plugin Platform and Assembly Direction

## Connector platform direction

A connector/plugin should declare a manifest with:

- unique plugin key
- version and config schema version
- secrets requirements
- capability set
- health/test contract
- optional Workbench projection contributor
- optional node/facet hooks
- optional agent policy exposure
- optional UI/editor descriptor(s)

## First-party migration rule

Existing built-in provider/resource kinds should become first-party plugins using the same platform.

That prevents a split world where built-in integrations are enum-driven while external ones are manifest-driven.

## Projection contributor direction

Plugins that want to show read-only structure nodes should contribute them through the assembly boundary, not by inserting canonical Workbench rows.

## Mutation boundary direction

For connector flows that must touch multiple modules:

- transaction where possible
- otherwise outbox / saga orchestration with explicit recovery state

Do not rely on growing chains of save-then-compensate behavior once real external connectors start participating.
