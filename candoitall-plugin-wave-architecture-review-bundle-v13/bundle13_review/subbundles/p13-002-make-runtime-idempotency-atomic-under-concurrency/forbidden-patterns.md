# Forbidden patterns

- do not keep read-then-insert logic without uniqueness-conflict recovery,
- do not treat race-driven `DbUpdateException` as an acceptable user-facing failure for duplicate work,
- do not solve this by serializing everything through a single in-memory lock.
