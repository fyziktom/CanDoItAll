# Target Solution

## End State

- Web endpoint files bind transport concerns and delegate to cohesive agent/workflow/
  recruiting application services.
- Portable public DTOs contain strings, numbers, GUIDs, `DateTimeOffset`, `JsonElement`,
  and explicit discriminators; no `.NET Type`, EF entity, or server path leaks.
- External identity and idempotency claims are persisted atomically with their owning
  catalog/run mutations inside the current workspace boundary.
- Package archive inspection is isolated from endpoint plumbing and is directly testable.
- Agent interview evidence is a canonical agent-domain record containing typed immutable
  links; CRM-HR may project or reference it but does not own duplicated mutable run prose.
- OpenAPI response metadata is explicit and validated against real serialization.

## Allowed Side Effects

- Add small top-level service/DTO/validator types and persistence fields in the existing
  owning projects.
- Split new endpoint families into dedicated static endpoint files rather than enlarging
  existing 600-800 line files.
- Add a project only if current project references cannot preserve dependency direction.

## Forbidden End State

- Another partial file as the permanent boundary for a broad existing service.
- Endpoint-local persistence or `IServiceProvider` service location.
- In-memory-only idempotency/evidence state.
- Cross-project references from agent/workflow/process core into Web or CRM-HR.
