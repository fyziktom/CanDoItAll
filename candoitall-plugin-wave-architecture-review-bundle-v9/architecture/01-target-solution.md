# Target solution
## Keep the universal carrier
The node should remain the universal carrier for:
- identity,
- node kind / subtype,
- parent/graph placement,
- canonical mindmap position (`PositionX`, `PositionY`),
- canonical markers,
- canonical notes/title/status/time semantics,
- canonical lifecycle/history hooks.

## Do not keep transport / external binding truth on the node
The node should **not** persist:
- route,
- media transport details,
- external artifact linkage payload,
- connector-owned external identity payload.

Those belong to binding facets / dedicated tables / read models.

## Plugin platform
The platform should become truly manifest-driven:
- generic config state bag,
- generic field renderer by `ConnectorConfigFieldType`,
- plugin key as authoritative identity,
- compatibility enums demoted or retired.

## Node references
Use an open model such as:
- `ReferenceNamespace`
- `ReferenceRoleKey`
- `TargetKind`
- `TargetId` (string)
- `OrderIndex`
- optional `MetadataJson`

Typed convenience wrappers can exist at the edges, but the persistence contract must be open-world.

## External side effects
Write-side plugins must execute through a generic connector command / outbox boundary with:
- durable records,
- retry/backoff,
- idempotency keys,
- manual replay,
- approval hooks where needed.
