# SB10 proof manifest

- status: Completed
- owned requirements: RQ-027, RQ-028, RQ-029, RQ-033
- implementation commit: `ebb8deae5f2deb0a379875fecf853ea8fc423be7`
- dependency mode: local sibling source projects
- host: Microsoft Windows NT 10.0.26200.0 x64; .NET SDK 10.0.303
- database: PostgreSQL Testcontainers for persisted-origin proof; in-memory real hosts for transport-policy proof
- architecture snapshot: `snap-20260815072303-363bd134`

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `bundle://proof/SB10/semantic-invariants.md` | Origin, scope, redaction, stable transport, and future-deployment contract. |
| `bundle://proof/SB10/changed-files.sha256` | Before/after SHA-256 manifest from the SB09 proof head to the implementation head. |
| `transcripts/01-current-head-gates.md` | Expected-red, affected builds, focused API/Unit, and PostgreSQL results. |
| `transcripts/02-negative-and-source-guards.md` | Scope denial, spoofing, query-token, redaction, and anti-stub assertions. |
| `transcripts/03-architecture-gate.md` | CodeAnalytics and manual ownership/dependency review. |
| `transcripts/04-validator-results.md` | Bundle and subbundle validator closure results. |
| `bundle://CHECKSUMS.sha256` | Bundle artifact checksum inventory. |

## Production behavior artifact matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| server-owned origin | HTTP DTO contains title only; Web supplies `Api` | response and PostgreSQL row expose `api` | product command preserves trusted `Application` origin | unmapped `origin: application` is rejected before dispatch |
| exact scope policies | Workspace owns scope constants; Web owns policies | route metadata selects read/manage/execute | authorization metadata is conditional on enabled bearer auth | broad `api` and wrong dedicated scopes return 403 |
| stable external operation contract | mapper emits `candoitall.llm-chat-operation.v1` and canonical links | OpenAPI exposes transport DTOs | same operation ID/body remains retry-safe | same ID/different body returns a redacted conflict |
| sanitized failures | product persists stable failure codes | API/SSE emits typed product fields | unexpected failures retain operation/conversation/type diagnostics | raw exception object, prompt, credential, and provider body are absent |

## Architecture note

CodeAnalytics reports four scoped projects, zero cycles, and no blocking diagnostics. Web remains the
transport/policy owner; LLM Chats product has no project dependency; Persistence references only the
product. The broad API authorization infrastructure was reused without weakening its other policies.
Conversation-create idempotency is explicitly deferred until the future deployment boundary supplies a
tenant/deployment/participant namespace in which a caller key can be collision-safe.

## Downstream trust

SB11 is explicitly authorized to consume this contract and re-prove the PostgreSQL HTTP/SSE surface on
its focused portability lane. It must reopen SB10 if bearer query tokens become accepted, origin becomes
client-controlled, scopes collapse, raw error/prompt content reappears, or deployment fields are added.
