# SB09 proof manifest

- status: Completed
- owned requirements: RQ-023, RQ-024, RQ-025, RQ-029
- implementation commit: `4c71bfa8857d1228e5cb5e23fac44c9746954dfc`
- dependency mode: local sibling source projects
- host: Microsoft Windows NT 10.0.26200.0 x64; .NET SDK 10.0.303
- database: PostgreSQL Testcontainers used by focused Integration tests
- architecture snapshot: `snap-20260815064713-4eb8c3ec`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `bundle://proof/SB09/semantic-invariants.md` | 202 admission, replay/gap, lifetime, terminal, profile, and redaction contract. |
| `bundle://proof/SB09/changed-files.sha256` | Before/after SHA-256 manifest for the implementation commit. |
| `transcripts/01-current-head-gates.md` | Expected-red, affected build, and final focused behavior results. |
| `transcripts/02-negative-and-source-guards.md` | Cursor, disconnect, cancellation, redaction, terminal, and anti-stub assertions. |
| `transcripts/03-architecture-gate.md` | CodeAnalytics and manual ownership/dependency review. |
| `transcripts/04-validator-results.md` | Bundle and subbundle validator closure results. |
| `bundle://CHECKSUMS.sha256` | Bundle artifact checksum inventory. |

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| 202 operation resource | application admission/details plus durable latest sequence | external client follows Location/status/events/cancel links | provider is held after POST returns | exact replay is explicit and does not redispatch |
| durable SSE page | EF event repository returns operation/range/aggregate plus events | Web replay adapter emits typed sequence frames | local signal only bounds wait; SQL remains authoritative | future/retained cursor gaps are explicit and status-linked |
| profile-fenced stream session | runtime lease is captured before preflight SQL | HTTP projection consumes the session token | switch cancels the projection and later reads fail closed | disconnect disposes only read lease, never operation execution |
| terminal envelope | normalized state event maps to one versioned public payload | generic writer selects event name and stops | success/failure/cancel/recovery all terminate | internal terminal metadata and raw provider data are not serialized |

## Architecture note

CodeAnalytics reports zero cycles, diagnostics, or open questions. The warning set remains scoped
complexity heuristics in existing broad Web API files; SB09 adds small named session, mapper, and replay
adapter owners instead of a new partial or façade. The generic writer grows additively because cursor,
heartbeat, anti-buffering, and profile cancellation must remain one transport implementation.

## Downstream trust

SB10 may secure the stable 202/status/events/cancel surface with server-owned origin and named scopes.
It must not move execution into HTTP/SSE or expose query bearer tokens. SB11 must re-prove this behavior
on its focused portability lane before CP2.
