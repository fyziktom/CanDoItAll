# Requirements index

Status: prepared. Closure fields are filled during execution.

| ID | Requirement | Primary owner | Closure |
|---|---|---|---|
| RQ-001 | Synchronize simple-chats with the latest development baseline before hardening proof. | SB00, SB13 | Closed: synchronized at `5522880cbf3101ed54c216ab74cac3b8ff2bade0`; immutable SB13 candidate confirmed at `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9` |
| RQ-002 | Replace stale or commitless proof with evidence tied to the actual implementation head and classify the prior 19 failures. | SB00, SB13 | Closed: 19/19 classified, original commit reconciled, and implementation/proof ancestry confirmed through SB13 candidate `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9` |
| RQ-003 | Maintain one canonical writable owner for conversation title and transcript metadata. | SB01, SB06 | Closed at CP1 `a820b867fcf34cd07a93d201a9ffc492c243e647`; canonical ownership and current-head PostgreSQL union pass |
| RQ-004 | Create and rename conversation state atomically without orphan or divergent rows. | SB01, SB06 | Closed at CP1 with current-head transaction and failure-injection proof |
| RQ-005 | Commit operation claim, pending user message, active turn, and admission evidence atomically. | SB02, SB06 | Closed at CP1 with current-head admission transaction proof |
| RQ-006 | Commit assistant finalization or exact failure compensation atomically with operation state and usage evidence. | SB02, SB06, SB08 | CP1 invariant plus SB08 same-transaction success/failure event integration pass |
| RQ-007 | Escalate unresolved compensation to RecoveryRequired; never leave a live active turn behind a terminal failure. | SB02, SB06 | Closed at CP1 with reducer, rollback, and compensation-exhaustion proof |
| RQ-008 | A durable cancellation request committed before semantic completion must prevent Succeeded. | SB02, SB06, SB08 | CP1 invariant plus SB08 streaming lifecycle/cancellation union pass |
| RQ-009 | Resolve idempotent replay by operation identity/fingerprint before mutable lifecycle validation. | SB02, SB06 | Closed at CP1 with Unit and real-host idempotency proof |
| RQ-010 | Conversation archive must not race an active turn or nonterminal operation. | SB02, SB06 | Closed at CP1 with locked archive-exclusion proof |
| RQ-011 | Fence every public use case from first read through final commit/return to one database profile identity and generation. | SB03, SB06 | Closed at CP1 with corrected read-owner composition and current-head profile proof |
| RQ-012 | A profile switch must prevent old-generation writes and produce deterministic retained evidence. | SB03, SB06 | Closed at CP1 with current-head PostgreSQL retained-evidence proof |
| RQ-013 | Use durable cross-instance execution ownership with claim, heartbeat, expiry, and release. | SB04, SB06 | Closed at CP1 with current-head two-host lease proof |
| RQ-014 | Support bounded cross-instance cancellation and never infer liveness from an in-memory registry alone. | SB04, SB06, SB08 | CP1 durable cancellation plus SB08 lease-checked stream/event integration pass |
| RQ-015 | Execute admitted operations independently from the initiating HTTP request through an available dispatcher. | SB04, SB06 | Closed at CP1; inline engine path removed and request-detachment proof passes |
| RQ-016 | Never automatically redispatch when durable evidence says a provider dispatch may have started. | SB02, SB04, SB06 | Closed at CP1 with fail-closed lease/recovery proof |
| RQ-017 | Use bounded SQL/keyset read models for collection and transcript pagination without N+1 queries. | SB05, SB06 | Closed at CP1 with current-head 2,000-message query-count proof |
| RQ-018 | Build provider context windows from bounded database reads rather than full transcript materialization. | SB05, SB06 | Closed at CP1 with bounded 12-message context over 2,000 canonical entries |
| RQ-019 | Provide an additive provider-neutral incremental LLM invocation port without breaking existing non-streaming callers. | SB07, SB11 | Closed at CP2 `4ec4d2694d980d52936b4679ae676a0624d5c6fb`; incremental and completed-fallback callers pass on Linux |
| RQ-020 | Implement true incremental streaming for OpenAI, Azure OpenAI, and Ollama, with a deterministic fallback policy. | SB07, SB11 | Closed at CP2; fragmented OpenAI/Azure SSE, Ollama NDJSON, and completed fallback pass on Linux |
| RQ-021 | Retry a stream only before its first emitted delta and never after partial output is externally visible. | SB07, SB11 | Closed at CP2; retry-before-delta and no-retry-after-visible-delta plus partial-failure compensation pass |
| RQ-022 | Persist a bounded per-operation event journal with monotonic sequence and durable replay authority. | SB08, SB11 | Closed at CP2; PostgreSQL concurrent sequence, rollback/no-signal, retention, replay, and transfer pass |
| RQ-023 | Expose SSE with Last-Event-ID/after replay, gaps, heartbeat, anti-buffering, profile lifetime, and terminal closure. | SB09, SB11 | Closed at CP2; Linux real-host delta, heartbeat, reconnect, gap, profile, proxy-header, and terminal proof pass |
| RQ-024 | SSE/client disconnect must not cancel the durable operation; explicit cancellation remains authoritative. | SB09, SB11 | Closed at CP2; request/stream disconnect completes without redispatch while explicit cancellation closes durably |
| RQ-025 | Turn start must return 202 Accepted promptly with operation, status, and event links. | SB09, SB11 | Closed at CP2; real host returns 202 with canonical links while the slow provider remains blocked |
| RQ-026 | Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers. | SB02, SB07, SB08, SB11 | Closed at CP2; per-attempt ordinal/outcome/usage and direct/recovery reduction pass on Linux/PostgreSQL |
| RQ-027 | Conversation origin is server-owned and cannot be spoofed by an HTTP client. | SB10, SB11 | Closed at CP2; Linux real-host and PostgreSQL union retain spoof rejection and authoritative Api origin |
| RQ-028 | Enforce LLM Chat read/manage/execute API scopes when bearer authorization is enabled. | SB10, SB11 | Closed at CP2; Linux real-host exact-scope/cross-scope/broad-api matrix passes |
| RQ-029 | Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE. | SB08, SB09, SB10, SB11 | Closed at CP2; Linux provider/API/SSE redaction and source guards pass |
| RQ-030 | Keep EF migration, model snapshot, retention, and database-transfer behavior consistent with the hardened schema. | SB01, SB08, SB11 | Closed at CP2; Linux migration success/fail-closed, retention, transfer, and restart-gap proof pass; SB08 pending-model proof remains current |
| RQ-031 | Keep implementation portable and prove affected behavior on Linux plus the final Windows/Linux/macOS CI matrix. | SB11, SB13 | Blocked at SB13: Linux package-graph behavior passes at CP2, but the exact Spreadsheet 0.1.18 package is absent from nuget.org, so the final three-OS matrix cannot start honestly |
| RQ-032 | Do not implement UI, shared-component refactoring, floating chat, or Project Structure context in this bundle. | SB12 | Closed at SB12 `58265975e868731e25e39d4bf9109f6010d68127`; complete changed-path/source guard passes and future owners are explicit |
| RQ-033 | Preserve a clean future LlmChatDeployment boundary for enterprise chatbot channels without dormant deployment fields now. | SB10, SB12 | Closed at SB12 `58265975e868731e25e39d4bf9109f6010d68127`; deployment-field guard and architecture handoff pass |
| RQ-034 | Run the expensive stable solution gate and CI matrix once, only at the immutable final head. | SB00, SB13 | Blocked at SB13 before execution: package preflight failed; the one restore/build/test/matrix budget remains unused for resumption |
| RQ-035 | Use filtered affected-scope tests throughout; forbid repeated full Unit/Integration/Solution suites before the final gate. | SB00, SB12, SB13 | Closed/Pass for execution discipline: test-policy guard passes, no repeated broad run occurred, and SB13 correctly preserved its unused single-shot budget after preflight failed |
