# Forbidden patterns

- do not keep `ToListAsync()` over the whole delivery/outbox candidate set before filtering,
- do not keep lock fields that are written but never used as part of an actual acquisition protocol,
- do not assume a single process will always own the runtime plane.
