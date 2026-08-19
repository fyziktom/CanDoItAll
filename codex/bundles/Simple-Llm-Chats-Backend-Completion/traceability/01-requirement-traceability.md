# Requirement Traceability

## Raw Input Coverage

| Raw request element | Normalized outcome | Owner | Closure path |
| --- | --- | --- | --- |
| Study the WIP hardening/SSE bundle. | Truthful retained/reopened/pending classification. | Analysis + SB01 | Governed re-entry manifest and CP0 |
| Use the finished backend/API predecessor as context. | Preserve valid product semantics; do not copy weaker historical auth/origin/inline-dispatch behavior. | Analysis + SB01/SB02/SB09 | Current source/host/architecture proof |
| Account for improved test-during-development workflow. | Per-work-unit project builds, exact `--list-tests`, expected/actual discovery, focused execution, invalidation keys, and one final broad gate. | Plan + all SBs | Execution ledger and proof manifests/transcripts |
| Identify what is done and pending. | Root audit verdict and SB00-SB13 classification. | `analysis/01-current-state.md` | SB01 CP0 and final raw-note closure |
| Prepare a successor for unfinished work. | Ten ordered execution-ready work units. | SB01-SB10 | Each progression gate then FINAL |
| Identify bugs/refactors around Simple Chats. | Confirmed defect table and locked local Web extraction. | Analysis/architecture + SB02-SB08 | Behavioral proof and CP3 |
| Backend only; UI later. | No UI source or browser feature proof; future UI remains separate. | BC-005 / all SBs | Changed-file inventory and final scope scan |
| Do not implement yet. | Product/test source untouched during preparation. | Preparation review | Git diff shows bundle-only changes |

## Requirement-To-Work-To-Proof Matrix

| Requirement group | Owning subbundle | Planned proof | Final closure consumer |
| --- | --- | --- | --- |
| BC-001–BC-006 scope, current graph, exact scopes/origin | SB01, SB02, SB09 | CP0 inventory; API/auth host proof; CP3 graph/SSE proof | SB10 |
| BC-010–BC-019 API validation/privacy/editor/ownership | SB02 | 12 exact real-host/PostgreSQL cases, DTO/Problem Details/OpenAPI samples, auth/origin/secret/source guards | SB09, SB10 |
| BC-020–BC-024 replay/CAS/pin/cancellation races | SB03 | 10 deterministic Unit/PostgreSQL/host cases and canonical row/side-effect evidence | CP1, SB09, SB10 |
| BC-030–BC-033 task supervision/recovery | SB04 | 10 task-lifetime/reconcile cases, provider task timelines, no-redispatch proof | CP1, SB09, SB10 |
| BC-040–BC-044 invocation/SSE/high-water/schema | SB05 | 8 restart/retention/API/SSE/migration/transfer cases and pending-model proof | CP2, SB09, SB10 |
| BC-050–BC-054 replay/retention/transient state | SB06 | 10 isolation/row-bound/starvation/eviction/gap cases, SQL and stress evidence | CP2, SB09, SB10 |
| BC-060–BC-065 options/workers/age/duration/transfer | SB07 | 15 positive/negative startup/configuration/concurrency/time/transfer cases, including concurrent source mutation, and migration parity | CP2, SB09, SB10 |
| BC-070–BC-072 provider log redaction | SB08 | 6 structured-log/sentinel/deadline/retry/cancel cases | CP2, SB09, SB10 |
| BC-080–BC-084 profile/SSE/architecture | SB09 | 15 existing exact profile/frame/gap/heartbeat/disconnect/terminal/cancel/auth/origin cases + accepted new union, real PostgreSQL flow, CodeAnalytics/guards/review | SB10 |
| BC-090–BC-092 release closure | SB10 | One broad stable gate, pending-model/docs/guards/secrets, same-commit 3-OS CI, independent verifier | FINAL |

## Explicit Exceptions

| Item | Status | Evidence/owner |
| --- | --- | --- |
| UI/chat component integration | `Not solved — explicitly out of scope` | User constraint; future UI bundle |
| Conversation-create idempotency | `Not solved — explicitly deferred` | Requires deployment-owned caller identity namespace; current no-blind-retry HTTP description remains tested/documented |
| Live provider certification | `Not solved — operational lane` | Deterministic external-boundary substitutes prove backend behavior; no credential requirement |
| Organization/per-user ownership, RAG, moderation, external channels | `Not solved — separate product boundaries` | Architecture boundary/deferred owner records |

## Status Rules

- During execution, every row becomes `Solved`, `Partially solved`, or `Not solved` with portable evidence.
- `Partially solved` cannot close FINAL for any BC requirement; it must reopen its owner or create an explicit blocker.
- A deferred exception may remain `Not solved` only because the raw request does not require it and the current contract states the limitation explicitly.
