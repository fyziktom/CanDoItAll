# Execution invariants

These invariants constrain every implementation phase.

## Invariant 1 — Canonicality first

If a later phase conflicts with the chosen canonical dependency model, the later phase is wrong.

## Invariant 2 — Compatibility is temporary and isolated

Compatibility paths must be easy to locate, audit, and remove later.

## Invariant 3 — No hidden mutation in analysis paths

Validation, read shaping, and review helpers must not mutate domain/editor state.

## Invariant 4 — Aggregate mutation must be safe under conflict

Silent last-write-wins behavior is not acceptable for critical process aggregates.

## Invariant 5 — Differential persistence must preserve durable meaning

Stable logical children should not be recreated with new identities unless the logical entity truly changed identity.

## Invariant 6 — UI is not the domain

Domain rules stay in services, policies, and state holders, not in Razor event handlers or view-only helpers.

## Invariant 7 — Queries do not become a second canonical model

Query-specific projections are allowed. Query-side mutation or shadow truth is not.

## Invariant 8 — Extraction follows ownership

Shared generic helpers go to neutral layers.
Process-template-domain helpers stay process-template-owned.
Do not collapse these two categories.

## Invariant 9 — Review gates are real gates

A review gate is failed if proof is weak, stale, missing, or architecturally inconclusive.

## Invariant 10 — Closure requires proof

No subbundle may move from `Ready` to `Completed` without satisfying its proof contract and progression gate.
