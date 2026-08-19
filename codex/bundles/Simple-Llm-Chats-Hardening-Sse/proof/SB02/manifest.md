# SB02 proof manifest

- implementation commit: `be36fedb2ce329af6021cd2330eb6162d8ef2db4`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral databases managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815011610-d209545b`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Acceptance-level state-machine and transaction proof. |
| `transcripts/01-red-cancellation-transition.md` | Executable negative proof at the pre-SB02 implementation. |
| `transcripts/02-unit-and-build.md` | Focused reducer, orchestration, archive, audit, build, and EF-model evidence. |
| `transcripts/03-postgresql-and-api.md` | Real PostgreSQL rollback/claim and real-host API evidence. |
| `transcripts/04-architecture-gate.md` | CodeAnalytics, dependency, owner, partial, and old-path assertions. |
| `transcripts/05-validator-results.md` | Bundle/subbundle validator results. |

SB02 replaces post-commit turn callbacks with explicit admission/invocation/completion/compensation
phases, a transactional command protocol, durable cancellation generation, and one pure reconciliation
reducer. Provider I/O remains outside database transactions.
