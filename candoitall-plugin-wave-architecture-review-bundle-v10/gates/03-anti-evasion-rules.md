# Anti-evasion rules

These patterns do **not** count as closure:

1. Moving the delete/save logic from `LoadAsync(...)` into another helper and still calling it from the read path.
2. Hiding stale projection cleanup in `ProjectWorkbenchSchemaInitializer.EnsureAsync(...)`, `TryGetStructureAsync(...)`, `GetStructureAsync(...)`, or `FindNodeAsync(...)`.
3. Replacing `SaveChangesAsync(...)` with `ExecuteDeleteAsync(...)`, `ExecuteUpdateAsync(...)`, raw SQL, or any other write primitive inside the read seam.
4. Claiming the read path is “effectively read-only” while stale rows are still deleted during reads.
5. Closing the gate with symbol retirement only, without behavior tests.
6. Claiming future plugin readiness while test coverage still exercises only known built-in plugin manifests.
7. Leaving the phase10 gate unable to fail the current repo shape.

Closure means the **behavior** changed, not just the symbol names.
