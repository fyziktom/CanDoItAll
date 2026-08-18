# SB06 / CP1 proof manifest

- implementation commit: `a820b867fcf34cd07a93d201a9ffc492c243e647`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral databases managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815041852-376a68b7`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | CP1 canonical, lifecycle, profile, lease, and bounded-query invariants. |
| `transcripts/01-current-head-gates.md` | Final filtered Unit/Integration, build, model, and transfer evidence. |
| `transcripts/02-legacy-path-removal.md` | Historical negative and current source-removal proof. |
| `transcripts/03-architecture-gate.md` | Six-project dependency and owner review. |
| `transcripts/04-validator-results.md` | Bundle/subbundle validator results. |
| `reviews/CP1-BACKEND-HARDENING.md` | Row-by-row checkpoint decision. |

CP1 proves the complete non-streaming backend chain and removes the last public inline provider
execution path before provider-neutral streaming contracts are introduced.
