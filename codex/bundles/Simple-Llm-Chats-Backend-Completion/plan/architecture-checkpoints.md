# Architecture Checkpoints

## CP0 — Re-entry Baseline (SB01)

- Re-run project/reference and service-registration inventory at the actual start commit.
- Confirm zero cycles and the current scoped resolver/hosted dispatcher lifetimes.
- Confirm no product/test changes are hidden in the worktree.
- Record the affected-source union and owner reservation.
- Gate: `Pass` only when all drift after `a8e3f87e...` is classified and no old proof is presented as current.

## CP1 — Lifecycle Ownership (after SB04)

- Review transaction ownership, CAS translation, definition revision pin, replay availability ordering, cancellation registration lifetime, provider task supervision, and reconciliation authority.
- Verify provider dispatch remains outside HTTP and outside admission/finalization transactions.
- Verify ambiguous post-dispatch work cannot redispatch automatically.
- Gate: independent reviewer records `Pass`; any false-success/orphan/duplicate side effect is `Fail`.

## CP2 — Durable Evidence And Bounds (after SB07 and SB08)

- Review operation/invocation/event schema, migration snapshot, transfer parity, high-water ownership, coherent replay isolation, retention row bounds, signal eviction, worker concurrency, options binding, and log allowlist.
- Confirm public DTOs are allowlists and system instructions/raw provider errors do not cross read/API/log boundaries.
- Gate: pending-model diff empty and focused PostgreSQL/Web/provider-runtime proof passes.

## CP3 — Final Focused C# Gate (SB09)

- Take a new CodeAnalytics snapshot over every changed production project plus direct consumers.
- Compare dependency direction and cycles with `architecture/02-csharp-dependency-direction.md`.
- Run source/architecture guards for forbidden edges, partials, inline HTTP dispatch, file-store activation, and Web persistence access.
- Review direct testability and anti-stub evidence.
- Re-run the profile/SSE/DI focused union.
- Gate: independent `csharp-architecture-review-gate` result is `Pass` with no unresolved critical/major finding.

## Final Re-entry Rule

Any production/project/build/test/workflow edit after CP3 reopens the affected checkpoint. SB10 may update only proof/status artifacts and documentation that does not change runtime/source-truth claims; otherwise CP3 must run again.
