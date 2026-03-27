# 09. Risk Register

## R1. Bridge auto-retry can duplicate non-idempotent work

Severity:

- Critical

Why it matters:

- duplicate app starts, builds, or atomic commits would corrupt runtime state and confuse Codex

Mitigation:

- request idempotency keys for non-idempotent calls
- retry only when delivery ambiguity is resolved

## R2. Dynamic candidate ports may surprise existing manual workflows

Severity:

- High

Why it matters:

- current operators may expect fixed localhost ports

Mitigation:

- define bundle 1 atomicity around logical runtime identity, not fixed external ports
- document that stable public relay/proxy is a later concern if needed
- preserve existing fixed-port watch flow as the default fast lane

## R3. Resource-scope locking can introduce deadlocks

Severity:

- High

Why it matters:

- a richer lock graph is safer than one global lock only if acquisition order is consistent

Mitigation:

- canonical lock ordering
- test coverage for nested scope acquisition
- fail-fast conflict behavior instead of indefinite waits where practical

## R4. Slot cleanup can delete a still-needed runtime

Severity:

- High

Why it matters:

- active or rollback-capable slots are part of runtime safety

Mitigation:

- never delete the active slot
- never delete the most recent previous slot
- require manifest and in-use checks before deletion

## R5. Published candidate preparation may be too slow for everyday edits

Severity:

- Medium

Why it matters:

- if Codex defaults to the atomic lane too often, the developer experience gets worse

Mitigation:

- keep source-watch as the explicit fast path
- use the atomic lane only when the task or watch pressure justifies it

## R6. Settings migration can become confusing

Severity:

- Medium

Why it matters:

- growing configuration without a clear model produces fragile deployments

Mitigation:

- group new settings by lane and slot behavior
- keep defaults conservative
- keep old settings valid through compatibility mapping

## R7. Manager UI can drift away from true backend state

Severity:

- Medium

Why it matters:

- operators may trust the UI more than raw manifests

Mitigation:

- drive UI from the same typed status models returned by tools
- keep transaction and slot data persisted

## R8. Bundle scope can creep into a full local deployment platform

Severity:

- Medium

Why it matters:

- this package is for Codex-safe local MCP workflows, not a general platform rewrite

Mitigation:

- enforce bundle 1 non-goals
- defer stable relay/proxy and broader deployment concerns unless later justified

## R9. Workflow guidance can become noisy enough that Codex starts ignoring it

Severity:

- Medium

Why it matters:

- if every response starts carrying coaching prose, the signal disappears and context efficiency regresses

Mitigation:

- keep guidance in a compact structured block
- emit only on selected low-volume status/control tools
- ban guidance on raw log and event payloads
- enforce a serialized size budget in tests

## R10. Workflow guidance can drift away from real runtime state

Severity:

- High

Why it matters:

- inaccurate steering is worse than no steering because it can push Codex into the wrong lane or validation action

Mitigation:

- derive guidance only from authoritative lane, revision, pressure, and rollback state
- centralize policy logic instead of hand-writing strings per tool
- require unit and integration tests for guidance selection
