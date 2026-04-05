# Forbidden patterns
- MarkerIcon / MarkerTone / MarkerLabel remain persisted as canonical node state
- ResolveLegacyJson or HydrateLegacyFields remain in active paths
- LoadAsync still normalizes marker state by writing to the DB
