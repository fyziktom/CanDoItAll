# Zero-write read boundary

## Required rule
The active structure read seam is zero-write.

That means the read call stack may:
- query persisted state,
- normalize data in memory,
- map runtime DTOs,
- compose in-memory projection nodes and links.

It may **not**:
- save entity changes,
- delete stale rows,
- issue raw SQL updates/deletes,
- execute `ExecuteDeleteAsync` or `ExecuteUpdateAsync`,
- hide cleanup inside a helper invoked by the read seam.

## Allowed in-memory behavior
The following are acceptable in phase10:
- in-memory marker normalization,
- in-memory binding/reference fallback composition,
- marking tracked entities as `EntityState.Unchanged` when necessary to prevent accidental persistence.

## Not allowed
These patterns do **not** count as closure:
- moving the delete/save logic from `LoadAsync` to a helper and still calling it during reads,
- moving the same cleanup into another method on the read call chain,
- keeping the cleanup in the read seam and renaming it from “retire” to “reconcile” or “ensure”.
