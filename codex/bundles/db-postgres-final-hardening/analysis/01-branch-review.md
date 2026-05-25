# Current branch review

## What Codex completed

1. Rebased or otherwise updated the branch so it is no longer behind `development`.
2. Removed SQLite from typed runtime/provider/source models.
3. Replaced hot DB switching with restart-first persisted activation.
4. Introduced canonical startup runtime profile.
5. Replaced normal runtime context creation with pooled `IDbContextFactory<AppDbContext>`.
6. Renamed the profile-specific factory to `IProfileAppDbContextFactory`, clarifying that it is for maintenance/profile-specific operations.
7. Added PostgreSQL batch claim SQL with `FOR UPDATE SKIP LOCKED` for key outbox/delivery paths.
8. Added process dispatch durable claim fields and claim checks.
9. Added tests and evidence manifests.

## What is not fully closed

1. Broad validation is still incomplete due local PostgreSQL auth/timeouts.
2. No numeric benchmark exists.
3. Lease finalization can still be stale if a long-running worker loses lease after it loaded an entity.
4. Lease renewal failure is often logged, but not necessarily converted into a mutation stop condition.
5. PostgreSQL parallelism defaults need proof; a code path can have parallel code while still configured to use `1`.
6. Source-of-truth rules are implemented implicitly but not yet encoded as strong invariants across API, UI, workers, transfer, and managed file paths.
