# Target Solution

## Runtime Integrity Direction

- Replace string-derived runtime decisions with typed process contracts.
- Keep process governance generic and separate from workflow executor internals.
- Make artifact materialization, lineage, grounding, validation, disposition, retry, and lint gates durable enough to survive restarts and review.

## Detailed Architecture

- Target runtime concepts and the dependency diagram are preserved in `bundle://architecture/01-target-runtime-integrity.md`.
- Risk-to-solution mapping is preserved in `bundle://architecture/02-risk-to-solution-map.md`.
- Generic process boundary constraints are preserved in `bundle://architecture/03-generic-process-boundary.md`.
