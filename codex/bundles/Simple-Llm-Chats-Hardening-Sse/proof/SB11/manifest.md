# SB11 proof manifest

- status: Completed
- owned requirements: RQ-019 through RQ-031
- implementation commit: `4ec4d2694d980d52936b4679ae676a0624d5c6fb`
- dependency mode: package references (`UseLocalCanDoItAllLibraries=false`)
- host: Ubuntu 24.04.4 LTS x64 container on Linux 6.18.33.2; .NET SDK 10.0.302
- database: PostgreSQL 16 on Docker Desktop host, reached from the Linux proof container
- architecture snapshot: `snap-20260815080824-3b5bd776`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `bundle://proof/SB11/semantic-invariants.md` | Incremental provider, durable journal, SSE, and portability invariants. |
| `bundle://proof/SB11/changed-files.sha256` | Test-proof changes from the SB10 proof head to SB11 implementation. |
| `transcripts/01-linux-package-build.md` | Cold package restore failure, exact dependency preparation, and clean Linux Web build. |
| `transcripts/02-focused-linux-tests.md` | Three-command CP2 focused-test budget and actual counts. |
| `transcripts/03-architecture-gate.md` | Before/after production ownership and dependency evidence. |
| `transcripts/04-validator-results.md` | Bundle/source validator closure. |
| `bundle://CHECKSUMS.sha256` | Bundle artifact checksum inventory. |

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| incremental provider updates | real OpenAI/Azure/Ollama parser fixtures | provider-neutral streaming adapter | retry and attempt audit | fragmented/malformed/partial-failure cases |
| durable operation events | transaction-owned PostgreSQL journal | SSE replay reader | monotonic sequence/retention/transfer | rollback publishes nothing; retained gap |
| asynchronous operation links | real hosted POST returns 202 | status/events/cancel URLs | request and stream disconnect independence | stale/conflicting/cancel/recovery paths |
| public SSE frames | shared writer and LLM event mapper | real HTTP stream | delta, heartbeat, reconnect, terminal close | no redispatch, duplicate delta, or provider secret |

## Architecture note

SB11 changed tests only. CodeAnalytics found four scoped production projects, 609 types, 4,140
members, 53 DI registrations, zero cycles, and zero blocking errors. Ownership and dependency direction
are identical to SB10; no production partial, reference, model, schema, or alternate path was added.

## Downstream trust

CP2 is Ready and SB12 may perform documentation/guard cleanup. SB13 must perform the one final immutable
restore/build/stable-test/CI matrix gate and must not call the package feed green until
`CanDoItAll.FileTools.FileInteraction.Spreadsheet` 0.1.18 is available to the configured CI feed or an
equivalent reviewed dependency-source correction is committed.
