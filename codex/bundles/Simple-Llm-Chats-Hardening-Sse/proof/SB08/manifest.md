# SB08 proof manifest

- status: Completed
- owned requirements: RQ-006, RQ-008, RQ-014, RQ-022, RQ-026, RQ-029, RQ-030
- implementation commit: `e543e7bdd3de97e8f52db9d7df182f462b317742`
- dependency mode: local sibling source projects
- host: Microsoft Windows NT 10.0.26200.0 x64; .NET SDK 10.0.303
- database: PostgreSQL Testcontainers used by focused Integration tests
- architecture snapshot: `snap-20260815060048-09276cd1`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `bundle://proof/SB08/semantic-invariants.md` | Durable journal, atomicity, coalescing, partial-output, retention, and transfer contract. |
| `bundle://proof/SB08/changed-files.sha256` | Before/after SHA-256 manifest for the implementation commit. |
| `transcripts/01-current-head-gates.md` | Final Unit, PostgreSQL, migration-model, and build results. |
| `transcripts/02-negative-and-source-guards.md` | Lease, redaction, anti-stub, no-old-path, and payload assertions. |
| `transcripts/03-architecture-gate.md` | CodeAnalytics and manual ownership/dependency review. |
| `transcripts/04-validator-results.md` | Bundle and subbundle validator closure results. |
| `bundle://CHECKSUMS.sha256` | Bundle artifact checksum inventory. |

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| durable operation event | application admission/lease/evidence/pipeline append through `LlmChatOperationEventJournal` | EF sequence paging and later SB09 SSE reader | state/attempt events join the outer transaction; wakeup is post-commit | rollback emits no wake; append-only guard rejects tracked mutation/deletion |
| coalesced text delta | `LlmChatStreamingPipeline` consumes the SB07 neutral stream | `EfLlmChatOperationEventRepository.ListAfterAsync` returns bounded typed pages | execution lease is checked under the operation row lock for every append | provider pause flushes by time; stale/no lease and bound violations fail closed |
| canonical assistant message | only `LlmChatOperationStateMachine.FinalizeSuccessAsync` calls transcript completion | transcript/read models remain the canonical conversation surface | assistant message, success state, usage, and success event share one outer transaction | failed partial stream compensates active turn and leaves no assistant message |
| retained journal transfer | transfer schema v5 loads retained rows | import validates and restores the same normalized rows | cleanup selects only old terminal parents | active/nonterminal rows survive cleanup; detached or malformed rows are rejected |

## Architecture note

CodeAnalytics reports zero cycles, diagnostics, blocking errors, error findings, or open questions.
Its one warning is the pre-existing multifunction `LlmChatConversationEngine` file at 391 lines. SB08
removed its completed-only invocation method and did not add a new responsibility layer or partial
class; extracting unrelated definition validation solely to silence a size heuristic would expand this
subbundle. The warning is recorded, nonblocking, and remains visible for later modular work.

## Downstream trust

SB09 may implement bounded SSE replay from the durable journal and use the profile-keyed signal as a
latency optimization. Database state remains authoritative; SB09 must prove Last-Event-ID gap handling,
disconnect behavior, terminal close, heartbeats, and the shared SSE writer.
