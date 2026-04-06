# Forbidden patterns

- moving cleanup to another method that is still called by `GetStructureAsync(...)`, `TryGetStructureAsync(...)`, `LoadAsync(...)`, or `FindNodeAsync(...)`
- hiding cleanup in `ProjectWorkbenchSchemaInitializer.EnsureAsync(...)`
- cleanup that is not idempotent
- deleting stale rows without explicit tests for the repair seam
