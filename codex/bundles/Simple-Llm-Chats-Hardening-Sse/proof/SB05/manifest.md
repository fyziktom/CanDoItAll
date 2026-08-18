# SB05 proof manifest

- implementation commit: `e88987c2018adcf9118d49109eb8d4e3d3eb2c12`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral database managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815034954-c4aa2a0f`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Bounded SQL, canonical ordering, cursor, and context-window invariants. |
| `transcripts/01-red-unbounded-paths.md` | Executable historical source negative at the pre-SB05 head. |
| `transcripts/02-unit-and-build.md` | Focused Unit, affected compile, and command-budget evidence. |
| `transcripts/03-postgresql-query-count.md` | Direct 2,000-message PostgreSQL command-count proof. |
| `transcripts/04-architecture-gate.md` | Owner, dependency, bypass, cycle, and partial assertions. |
| `transcripts/05-validator-results.md` | Bundle/subbundle validator results. |

SB05 replaces per-item and full-document query paths with explicit canonical read models, deterministic
keyset pagination, and a production turn store that loads only the context it can send.
