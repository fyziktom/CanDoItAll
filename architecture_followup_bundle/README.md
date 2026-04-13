# Process module post-Codex architecture follow-up bundle

This bundle reopens the Process-module architecture work after the first hardening initiative was marked complete.

## Why this bundle exists

The current repository is materially better than the original baseline, but it is not yet in a state that I would sign off as architecturally closed. The remaining red gaps are not cosmetic:

- core dependency modeling is still only **collection-first**, not truly canonical;
- most Process child/runtime tables still do **not** have database-enforced foreign keys;
- the step-dependency uniqueness invariant is still broken for the common `NULL` branch-outcome case;
- lifecycle invariants such as **single draft**, **single published**, and **safe active published version binding** are still assumed in code but not fully enforced in the schema;
- search/activity side effects still execute **after** the DB transaction commits, without an outbox boundary;
- the checked-in proof artifacts do not currently prove that the claimed full Process integration suite actually ran.

## Mission

Close the remaining architectural red gaps without regressing the improvements that already landed:

- keep optimistic concurrency;
- keep differential graph persistence;
- keep the improved publish/runtime/query decomposition;
- finish the data-model, invariant, and proof hardening that is still missing.

## Layout

- `01-verdict-and-scope.md` — review verdict and reopening rationale
- `02-open-findings.md` — detailed findings with repository evidence
- `03-target-architecture.md` — target direction for the follow-up
- `04-codex-rules.md` — non-negotiable execution rules
- `05-proof-contract.md` — proof required for closure
- `subbundles/` — numbered execution subbundles plus corrective playbooks
- `codex/` — machine-readable task plan and stop rules
- `reviews/` — templates for execution report and gate memo log
- `traceability/` — finding-to-subbundle mapping

## Recommended execution order

1. `01-live-proof-reconciliation-and-gap-reopen`
2. `02-true-canonical-dependency-model-closure`
3. `03-architecture-review-gate-a`
4. `04-process-schema-referential-integrity-hardening`
5. `05-null-safe-dependency-uniqueness-and-db-invariants`
6. `06-architecture-review-gate-b`
7. `07-definition-lifecycle-invariant-hardening`
8. `08-transactional-side-effects-and-outbox-alignment`
9. `09-architecture-review-gate-c`
10. `10-service-seam-and-ui-orchestration-follow-up`
11. `11-final-proof-and-closure`

## Closure bar

Do not close this bundle while any red finding from `02-open-findings.md` remains open.
