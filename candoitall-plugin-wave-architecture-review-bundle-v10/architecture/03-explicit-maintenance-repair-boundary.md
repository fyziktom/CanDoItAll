# Explicit maintenance / repair boundary

## What must move out of the read seam
The following responsibilities still exist but must no longer live in `LoadAsync(...)`:
- retiring stale system-managed projection nodes,
- retiring stale system-managed projection links,
- deleting projection layout overrides whose node key no longer exists in the assembled graph.

## Acceptable implementation options
Phase10 allows any of these if the boundary is explicit and tested:
1. a dedicated maintenance/repair service in the Workbench module,
2. a migration/bootstrap step that invokes a dedicated repair helper,
3. an explicit operator/admin repair command.

## Forbidden implementation options
These do **not** count:
- hiding repair inside `ProjectStructureAssemblyService.LoadAsync(...)`,
- hiding repair inside `FindNodeAsync(...)`,
- hiding repair inside `TryGetStructureAsync(...)`,
- hiding repair inside `ProjectWorkbenchSchemaInitializer.EnsureAsync(...)` if that method stays reachable from reads.

## Required repair properties
The explicit repair seam must be:
- idempotent,
- independently testable,
- able to run before or during rollout,
- separate from normal user structure reads.
