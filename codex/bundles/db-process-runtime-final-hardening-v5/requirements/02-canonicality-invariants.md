# Canonicality invariants

1. A non-expired lease is canonical ownership unless proven otherwise.
2. No recovery worker may clear a non-expired lease without owner-death evidence.
3. Final state updates for leased work must be conditional on the same lease token and a non-expired lease.
4. Losing a lease turns the worker into an observer; it may not write canonical state transitions.
5. Retried side effects must be idempotent or must not be retried.
6. Pending database activation is not runtime database activation until process restart.
7. Profile-specific contexts are maintenance/admin tools, not normal runtime persistence.
8. Parallelism must partition work so records with the same canonical aggregate are not processed concurrently.
