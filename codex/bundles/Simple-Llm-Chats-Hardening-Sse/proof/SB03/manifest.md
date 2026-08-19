# SB03 proof manifest

- implementation commit: `96f054905eecd33e04228e7837ae7850e3eeeeb4`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral database managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815020112-e34a58a8`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Operation-scope and atomic switch/write invariants. |
| `transcripts/01-red-public-use-case.md` | Executable negative proof at the pre-SB03 implementation. |
| `transcripts/02-unit-and-build.md` | Focused scope, fence, lease, composition, and build evidence. |
| `transcripts/03-postgresql-api.md` | Real-host PostgreSQL retained-evidence and stale-host proof. |
| `transcripts/04-architecture-gate.md` | Owner, dependency, bypass, cycle, and partial assertions. |
| `transcripts/05-validator-results.md` | Bundle/subbundle validator results. |

SB03 places one immutable runtime identity around every public LLM Chat use case. It serializes switch
publication with durable LLM Chat commits, rejects stale old-root leases, and preserves provider audit
and usage evidence when a switch prevents finalization.
