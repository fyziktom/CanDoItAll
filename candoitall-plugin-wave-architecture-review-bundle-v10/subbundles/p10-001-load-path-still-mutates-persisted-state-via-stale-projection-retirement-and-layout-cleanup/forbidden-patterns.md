# Forbidden patterns

- `LoadAsync(...)` still calls `RetireLegacyProjectionRowsAsync(...)` or any equivalent repair helper
- `LoadAsync(...)` still contains `SaveChangesAsync(...)`
- `LoadAsync(...)` still removes stale layout rows
- any helper reachable from `LoadAsync(...)` still performs delete/update/save operations
