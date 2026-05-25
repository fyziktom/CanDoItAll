# Canonicality invariants

## C1 — One canonical DB per process generation

At any point in normal runtime, a process generation must have one canonical database connection/profile.

## C2 — No straddling

No request, process step, workflow run, automation delivery, or background job may start in one active database and finish in another.

## C3 — Profile switching is an operator transition

Changing the active database profile is an operator action. It must either:
- require a restart, or
- enter explicit maintenance mode and drain safely with a visible generation change.

## C4 — Durable work claim beats in-memory lock

In-memory locks may be performance helpers only. The canonical protection must be durable PostgreSQL state:
- claim token,
- lease timestamp,
- claimant identity,
- attempt count,
- unique/idempotency constraint,
- terminal state.

## C5 — At-most-one active execution per process step

A process step may have at most one active canonical automation execution at a time, unless the process model explicitly supports parallel branches. This must be enforced by DB state, not only by `SemaphoreSlim`.

## C6 — At-most-one delivery claim per lease token

An automation/outbox delivery may be claimed by one worker at a time. Stale leases may be rescued only after configured timeout, with monotonic attempt count.

## C7 — Transfer tools are not runtime switching tools

Database transfer/preview actions are admin tools. They may open source and target profile-specific contexts, but they must not alter the canonical runtime DB in-process unless going through the explicit activation/restart policy.

## C8 — InMemory is not a normal persistent data source

InMemory may be used in tests and explicit config overrides. It must not be saved, selected, or transferred as a normal user-managed runtime data source.
