# Canonicality invariants

## I1 — One runtime DB per process

A running process must use exactly one canonical database profile resolved at startup or explicit runtime override.

## I2 — Pending activation is not runtime

A profile selected through UI activation is pending-next-start until process restart. It must not be shown as the current runtime profile.

## I3 — Profile-specific contexts are maintenance-only

Profile-specific contexts may be used for schema health, transfer, create-empty, and bootstrap. They must not be used by runtime execution loops.

## I4 — Claim token owns mutation rights

If a long-running process dispatch claim is lost or expired, that worker loses mutation rights for the step.

## I5 — Parallel work is lease-token scoped

Outbox/delivery work may run concurrently only after durable claim. Completion must verify the claim/lease token or use idempotent update semantics.

## I6 — Aggregate rows are protected

Parallel processing must not race envelope, process run, process step, or connector aggregate state. Use partitioning, transactions, concurrency tokens, or atomic SQL updates.

## I7 — Maintenance switching must not fragment truth

Database activation must either require restart or be a deliberately named maintenance operation with explicit UI warnings, lockout behavior, and test proof.
