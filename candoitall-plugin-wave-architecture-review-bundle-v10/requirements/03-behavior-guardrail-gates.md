# Behavior guardrail gates

## Required static checks
The phase10 gate must fail when any of the following are true:

- `LoadAsync(...)` directly contains `SaveChangesAsync`, `RemoveRange`, raw SQL mutation, `ExecuteDeleteAsync`, or `ExecuteUpdateAsync`,
- `LoadAsync(...)` calls a local helper whose body performs persistence mutation,
- the required zero-write and repair tests are missing,
- the required unknown-plugin manifest tests are missing.

## Required behavioral proof
The repo must prove all of the following in tests:
- a stale system-managed node/link survives a normal structure read,
- a stale projection layout survives a normal structure read,
- legacy marker/reference fallback can still be composed during reads without any persistence mutation,
- the explicit repair seam can remove stale projection artifacts when intentionally invoked,
- unknown manifests using all shared field types work without page-specific changes.
