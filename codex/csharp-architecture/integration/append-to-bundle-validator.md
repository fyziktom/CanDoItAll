# Appendix for `candoitall-bundle-validator`

Add these checks to the prepared-stage and completed-stage validator expectations for C# architecture-heavy bundles.

## Prepared-stage checks

If the bundle touches C# architecture, verify:

- `architecture/00-csharp-current-state-inventory.md` exists.
- `architecture/01-csharp-boundary-map.md` exists.
- `architecture/02-csharp-dependency-direction.md` exists.
- `architecture/03-csharp-pattern-selection-records.md` exists.
- `architecture/04-csharp-testability-plan.md` exists.
- `plan/architecture-checkpoints.md` exists.
- `reviews/csharp-architecture-gate.md` exists.
- Every architecture-relevant subbundle has C# architecture sections.
- Critical foundation subbundles exist before dependent feature subbundles.
- Partial-class policy is explicitly stated.
- Testability proof is planned.

## Completed-stage checks

Verify:

- architecture gate result is recorded
- changed project references have before/after proof
- old large class shrink or thin-facade proof is recorded
- no new partial class was added without policy justification
- extracted behavior has isolated unit tests
- composition smoke exists when registration changed
- pattern selection records match implementation
- unresolved bridges have follow-up subbundles
