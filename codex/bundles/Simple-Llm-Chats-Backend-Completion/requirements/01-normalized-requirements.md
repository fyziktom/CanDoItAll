# Normalized Requirements

All requirements are `Planned` until their owning subbundle closes with the declared evidence. “Preserve” requirements still require current-head regression proof when their source was invalidated.

## Scope And Baseline

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-001 | Preserve the current ordinary-chat boundary: no agent execution, tools, skills, memory, processes, Projects, workspaces, or provider-native conversation state. | SB01, SB09 |
| BC-002 | Preserve PostgreSQL as canonical and keep the generic file conversation store out of product composition. | SB01, SB09 |
| BC-003 | Record the actual execution start commit, classify drift after `a8e3f87e...`, and replace stale WIP package-mode/test commands with current sibling-source and `tests/Solutions/*.slnx` commands. | SB01 |
| BC-004 | Run `--list-tests` for every new/changed filter, record expected and actual discovery, and fail on zero or unexpected discovery. | SB01-SB10 |
| BC-005 | Make no UI/component/CSS/JavaScript integration changes and claim no browser proof. | All |
| BC-006 | Retain exact `ReadLlmChats`, `ManageLlmChats`, and `ExecuteLlmChats` policies and server-owned `Api` origin. | SB02, SB09 |

## API, Privacy, And Transport Ownership

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-010 | Every empty definition, conversation, operation, and optional definition-filter GUID returns HTTP 400 with stable `llm-chat.invalid-request` Problem Details; no typed-ID constructor exception reaches the global 500 handler. | SB02 |
| BC-011 | Explicit `take=0`, negative, and over-limit values are rejected consistently; omitted values alone use defaults. | SB02 |
| BC-012 | Endpoint metadata declares every implemented 400/404/409/422/503/504 response, including GET conversation's invalid cursor/page-size 400. | SB02 |
| BC-013 | Unknown JSON members return 400 with stable `llm-chat.invalid-request` Problem Details, not status-only binder behavior. | SB02 |
| BC-014 | Read-scoped transcript projection excludes `System` entries in the database query before cursor paging, while provider execution still receives the pinned system instruction. | SB02 |
| BC-015 | Add `GET /api/llm-chats/{definitionId}/editor` under `ManageLlmChats`; it returns the authoritative editable definition/revision including system prompt and ETag, but never credentials, endpoints, local paths, or provider secrets. | SB02 |
| BC-016 | Remove `RequestFingerprint` from public operation responses; the internal durable fingerprint remains unchanged. | SB02 |
| BC-017 | Split definition and conversation endpoint mapping/handlers into distinct internal Web owners while preserving `MapLlmChatsApi`, route templates, endpoint names, policies, DTO schema names, and error semantics. | SB02 |
| BC-018 | Remove unused `LlmChatOperationKind.Cancel` and `.Recover`, preserve `SendTurn = 0`, and treat impossible stored values as explicit storage corruption. | SB02 |
| BC-019 | Correct docs/traceability references to real contracts; do not invent nonexistent `LlmChatDefinitionSummary`. | SB02 |

## Transactional Command Correctness

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-020 | Resolve an already committed same-ID/same-fingerprint operation before executor-availability validation so terminal and nonterminal replays return the same operation without new dispatch/message/audit side effects. | SB03 |
| BC-021 | A genuinely new turn still fails 503 when no dispatcher is registered; same-ID/different-fingerprint still returns the stable conflict. | SB03 |
| BC-022 | Real two-context definition update/status and conversation rename CAS races map to stable HTTP 409 results; raw `DbUpdateConcurrencyException` never crosses the application boundary. | SB03 |
| BC-023 | Conversation creation locks or atomically revalidates definition status/current revision in its transaction so it pins either the committed active revision or fails; it cannot pin a concurrently archived/suspended/stale revision. | SB03 |
| BC-024 | Live cancellation notification is idempotent and no-throw under completion/disposal races and throwing callbacks; the durable cancellation result remains authoritative. | SB03 |

## Execution Supervision And Recovery

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-030 | Once provider execution starts, every executor exit cancels when required and awaits/observes the provider task before releasing registration/scope; heartbeat, lease, profile, and shutdown failures cannot orphan work. | SB04 |
| BC-031 | Heartbeat/control failure after dispatch produces one evidence-consistent failed/cancelled/recovery-required outcome, never false success or unobserved provider failure. | SB04 |
| BC-032 | Add `POST /api/llm-chat-operations/{operationId}/reconcile` under `ManageLlmChats`, with 400/404/409 metadata and stable Problem Details. | SB04 |
| BC-033 | Reconciliation settles `RecoveryRequired` only when durable invocation/transcript evidence proves succeeded, failed, or cancelled; it rejects a live owner and never redispatches ambiguous post-dispatch work. | SB04 |

## Durable Audit And SSE Contract

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-040 | Operation status returns a bounded ordered sanitized invocation collection containing ordinal, provider kind, model, requested/effective effort, outcome, safe failure code, usage, and timestamps; exclude provider profile ID/name, correlation ID, credentials, endpoints, prompts, paths, and raw errors. | SB05 |
| BC-041 | Persist and project completed-attempt model, bounded finish-reason text, delivery mode, and usage into `llm.response.completed`; preserve current provider-completed success semantics rather than adding provider-name inference. | SB05 |
| BC-042 | A response/event/byte limit violation uses one typed consumer-abort reason so operation, invocation audit, and SSE terminal evidence agree on the same stable stream-limit failure and preserve known usage. | SB05 |
| BC-043 | Persist a monotonically increasing operation event high-water mark, update it atomically with append, backfill it from existing events, include it in transfer, and never regress it after retention deletes rows. | SB05 |
| BC-044 | Migration, EF model snapshot, operation/event DTO schema, and pending-model state remain synchronized. | SB05, SB07 |

## Replay, Retention, And Transient State

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-050 | `ListAfterAsync` returns operation snapshot, retained range, events, cursor character count, and high-water from one coherent bounded database snapshot; it cannot combine a terminal event with stale operation state. | SB06 |
| BC-051 | Retention selects eligible event rows, not operation IDs; each call deletes at most `CleanupBatchSize` rows, cannot starve newer operations behind already-empty older ones, and never removes active/nonterminal data. | SB06 |
| BC-052 | Cleanup drains due backlog in bounded batches and retries promptly after zero/failed work without hiding a backlog for a full interval. | SB06 |
| BC-053 | Process-local event signal and retention-schedule state have race-safe idle/profile-generation eviction with a deterministic bound; durable polling/journal remains the correctness authority. | SB06 |
| BC-054 | Reconnect after partial/full retention emits correct gap/earliest/current/high-water semantics and terminal close without provider redispatch. | SB06 |

## Capacity, Configuration, And Transfer

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-060 | Bind typed streaming and dispatcher options from configuration, validate them on startup, preserve safe defaults, and reject invalid combinations before serving traffic. | SB07 |
| BC-061 | Maximum chunk bytes never exceeds the persisted event text bound; aggregate character/byte limits do not advertise content larger than the canonical assistant-message limit; shared constants own duplicated bounds. | SB07 |
| BC-062 | Hosted dispatch supports configured bounded worker concurrency while durable database claims/active-turn invariants prevent duplicate or same-conversation concurrent execution. | SB07 |
| BC-063 | Enforce maximum queued age and total operation duration with typed durable outcomes; after ambiguous dispatch, expiration becomes recovery-required rather than automatic redispatch. | SB07 |
| BC-064 | Availability/saturation reporting distinguishes “worker registered” from actual progress/capacity and records actionable safe state without credentials, prompts, or endpoints. | SB07 |
| BC-065 | Database-transfer validation rejects invalid enum/state/relationship graphs, includes all new audit/high-water fields, and enforces explicit aggregate import bounds before materializing untrusted input. | SB07 |

## Provider Failure Redaction

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-070 | Provider streaming preparation/attempt logs never attach raw exception objects or log exception messages/inner exceptions that may contain provider bodies, credentials, endpoints, paths, prompts, or system instructions. | SB08 |
| BC-071 | Failure logs retain actionable allowlisted context: provider kind, safe provider identifier, model, correlation identifier, attempt ordinal, typed failure kind, and partial-output flag. | SB08 |
| BC-072 | Public exceptions and durable audit continue to expose only stable typed failure categories/codes; redaction does not silently swallow cancellation or change retry semantics. | SB08 |

## Profile/SSE, Architecture, And Closure

| ID | Requirement | Owner |
| --- | --- | --- |
| BC-080 | Re-prove current scoped provider resolution and runtime-lease callback/disposal synchronization under profile switches. | SB09 |
| BC-081 | Re-prove profile switch before response start, mid-frame atomic completion, cancellation normalization, and pending-read drain before request-scope release. | SB09 |
| BC-082 | Preserve SSE replay, gap, heartbeat, terminal flush/close, disconnect independence, exact authorization scopes, and single provider dispatch on reconnect. | SB09 |
| BC-083 | Preserve the existing project dependency direction, add no cycle or Web-to-persistence dependency, introduce no new project/interface without a recorded re-entry decision, and use no partial-class split. | SB09 |
| BC-084 | Run CodeAnalytics and architecture guards against the final changed-source union and obtain an independent C# architecture gate decision. | SB09 |
| BC-090 | At one named frozen commit, run current Release restore/build, stable discovery/test filter, documentation, architecture, migration pending-model, and repository checks exactly once for the named cross-cutting invalidation trigger. | SB10 |
| BC-091 | The same application commit and pinned sibling-source commits pass the current Windows x64, Ubuntu x64, and macOS arm64 CI matrix; artifacts identify host/runtime/commit. | SB10 |
| BC-092 | Final status, traceability, proof manifests/transcripts, hashes, docs, code, and CI reach one consistent conclusion with no stale Ready/Locked/Blocked claims. | SB10 |
