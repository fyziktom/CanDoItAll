# Input Coverage Matrix

| Input | Normalized concern | Destination | Owning subbundle |
| --- | --- | --- | --- |
| Latest commit is an input packet, not architecture | Need executable hardening bundle | `README.md`, `plan/01-phase-plan.md` | All |
| Successful Tetris process run | Preserve working path and use as regression baseline | `templates/process-test-scenarios/tetris-mini-game.json` | SB08 |
| QA recovery required | Strengthen proof/recovery semantics | `requirements/02-hardening-quality-gates.md` | SB02, SB04, SB09 |
| Stale run id `49fd...` in lineage | Current-run proof binding | `requirements/R-004`, `analysis/02-assumptions-and-risks.md` | SB02, SB04, SB09 |
| Port/database profile drift | Runtime profile identity proof | `requirements/R-004`, SB04 proof | SB04 |
| Build output locks | Runtime host lifecycle hardening | `architecture/01-target-refactoring-architecture.md` | SB04 |
| Large process dispatch files | Responsibility extraction with characterization tests | `inventories/01-hotspot-inventory.md` | SB02 |
| Large workflow canvas/live dashboard UI | UI refactor around typed canonical DTOs | `inventories/01-hotspot-inventory.md` | SB07 |
| String ids/JSON paths | Canonical descriptors and scanner | `inventories/02-string-key-json-path-inventory.md` | SB01 |
| Browser proof spread across prompts/tools/runtime | Unified browser proof contract | `requirements/R-010` | SB04, SB06 |
| Office365 external side effects | Dry-run/commit/idempotency/processed marker | `requirements/R-011` | SB05 |
| Unavailable workflow executors | Clear diagnostics and selection prevention | `requirements/R-012` | SB05, SB07 |
| Token billing mismatch | Usage ledger, finalizer/failure/repair/background accounting | `analysis/03-token-cost-accounting-audit.md` | SB03 |
| Agent/skill/template drift | Canonicalization and active skill sync proof | `requirements/R-013`, `R-014` | SB06 |
| Need five domain-distinct app examples | Multi-domain process E2E suite | `templates/process-test-scenarios/` | SB08 |
| Senior QA inspection before zip | Prepared bundle validation and self-review | `reviews/00-bundle-self-review.md` | SB09 |
