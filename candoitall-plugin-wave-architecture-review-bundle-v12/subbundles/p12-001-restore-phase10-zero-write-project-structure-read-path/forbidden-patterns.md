# Forbidden patterns

- Do not call `RetireLegacyProjectionRowsAsync(...)` from `LoadAsync(...)`.
- Do not delete stale layouts from `LoadAsync(...)`.
- Do not call `SaveChangesAsync(...)` from the read path.
