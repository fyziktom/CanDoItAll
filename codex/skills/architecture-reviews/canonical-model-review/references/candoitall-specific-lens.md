# CanDoItAll-specific lens

Use these checks for a project-operating-system style architecture.

## Node as universal primitive
Ask whether `Node` is:

1. a strong universal primitive with explicit contracts and extension points, or
2. a generic box that accumulates unrelated semantics

Warning signs:
- node contains many unrelated optional fields
- node type does not meaningfully constrain behavior
- more and more modules extend node by adding ad hoc flags

## NodeType discipline
Check whether node type defines:
- required fields
- allowed relations
- actor semantics
- artifact semantics
- time semantics
- execution semantics

If node type is only a label, the model may be drifting.

## Relation discipline
Check whether:
- parent-child
- dependency
- assignment
- references

are distinct and explicit.

## Projection discipline
Check whether:
- Gantt
- Mermaid
- CRM views
- testing dashboards
- activity feeds

are derived from canonical truth instead of acting as parallel truths.

## Agent and policy separation
Check whether:
- MCP token scopes
- allowed actions
- approval flows
- agent identities

live in policy / authorization instead of leaking into random domain objects.

## Snapshot and archival separation
Check whether:
- IPFS snapshots
- export/import payloads
- backup artifacts

are clearly separated from live canonical truth.

## Runtime tooling separation
Check whether:
- watch helpers
- diagnostics filters
- dev process state

stay out of the core project model.
