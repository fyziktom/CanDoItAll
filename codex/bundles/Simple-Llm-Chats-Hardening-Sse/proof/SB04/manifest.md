# SB04 proof manifest

- implementation commit: `7389daff6c21a4568895e514debe110434908d67`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200 x64; .NET SDK 10.0.303
- database: local PostgreSQL ephemeral database managed by `PostgresTestDatabaseLease`
- architecture snapshot: `snap-20260815030209-a236038a`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Durable owner, cancellation, request-lifetime, and fail-closed recovery invariants. |
| `transcripts/01-red-request-lifetime.md` | Executable negative proof at the pre-SB04 implementation. |
| `transcripts/02-unit-and-build.md` | Focused fake-time owner tests, model check, and compile evidence. |
| `transcripts/03-postgresql-api.md` | Two-provider PostgreSQL and request-disconnect proof. |
| `transcripts/04-architecture-gate.md` | Owner, dependency, bypass, cycle, and partial assertions. |
| `transcripts/05-validator-results.md` | Bundle/subbundle validator results. |

SB04 detaches paid provider work from HTTP lifetime, gives every execution one durable owner/epoch,
uses bounded heartbeats for cross-instance cancellation, and never automatically redispatches after
provider-start evidence.
