# SB01 proof manifest

- implementation commit: `689f2b5368bf6fdba7fad24dfa6fa4dee9b4abfc`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral databases managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815002601-d665d970`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Acceptance-level semantic proof. |
| `transcripts/01-red-atomicity.md` | Negative proof against the old independent-context path. |
| `transcripts/02-green-postgresql.md` | Final focused PostgreSQL result and diagnostic history. |
| `transcripts/03-unit-and-build.md` | Application ordering, build, and EF model results. |
| `transcripts/04-architecture-gate.md` | CodeAnalytics, source assertions, and architecture decision. |
| `transcripts/05-validator-results.md` | Bundle/subbundle validator results. |

SB01 replaces duplicate writable conversation metadata with explicit field ownership and makes
conversation create/rename participate in one scoped `AppDbContext` transaction.
