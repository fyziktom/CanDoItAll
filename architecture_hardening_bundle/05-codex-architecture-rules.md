# Codex architecture rules

These rules are not optional. Every implementation subbundle must preserve them.

## Rule 1 — One source of truth per concept

Do not allow dependency meaning, publication state, runtime transition meaning, or query summaries to be derived from multiple canonical representations.

## Rule 2 — Validation must be pure

Validation methods must not mutate editor or aggregate state. Normalization must be explicit and intentionally named.

## Rule 3 — Compatibility code must be quarantined

Legacy fields or adapters may exist temporarily, but only behind a small, named compatibility boundary. Do not let compatibility fallback logic leak across the module.

## Rule 4 — Public façade may stay, internals must split

Keep `ProcessesService` as a façade if that minimizes caller churn, but move behavior into smaller internal services. Do not simply create more partial files for the same god service.

## Rule 5 — Transactions are explicit

Save, publish, and critical runtime transitions must have explicit transaction boundaries. Do not rely on incidental EF batching.

## Rule 6 — Concurrency must be provider-agnostic

Do not use a SQL Server-specific rowversion approach. Use an application-managed concurrency token or equivalent strategy that works with the current providers.

## Rule 7 — Differential persistence is required

A no-op or small edit must not destroy and recreate the full child graph. Stable logical children must keep their identity.

## Rule 8 — Query code is projection-only

Do not let query services become a second mutation surface. Query decomposition should improve shape and performance, not create shadow state.

## Rule 9 — Shared extraction must be intentional

Only extract to `SharedKernel` or infrastructure when the semantics are genuinely shared. Domain-specific logic can stay module-local behind a single reusable helper.

## Rule 10 — No new monoliths

Do not replace the current oversized service or workspace with a different oversized coordinator. The split must be responsibility-based and testable.

## Rule 11 — Both providers matter

Any persistence change that requires migrations or snapshot updates must be done coherently for both SQLite and PostgreSQL.

## Rule 12 — Proof is part of the work

A subbundle is not complete when code compiles locally. It is complete only when its proof contract and progression gate are satisfied.
