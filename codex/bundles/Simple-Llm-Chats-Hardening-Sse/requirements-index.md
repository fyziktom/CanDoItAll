# Requirements index

Status: prepared. Closure fields are filled during execution.

| ID | Requirement | Primary owner | Closure |
|---|---|---|---|
| RQ-001 | Synchronize simple-chats with the latest development baseline before hardening proof. | SB00, SB13 | Pending |
| RQ-002 | Replace stale or commitless proof with evidence tied to the actual implementation head and classify the prior 19 failures. | SB00, SB13 | Pending |
| RQ-003 | Maintain one canonical writable owner for conversation title and transcript metadata. | SB01, SB06 | Pending |
| RQ-004 | Create and rename conversation state atomically without orphan or divergent rows. | SB01, SB06 | Pending |
| RQ-005 | Commit operation claim, pending user message, active turn, and admission evidence atomically. | SB02, SB06 | Pending |
| RQ-006 | Commit assistant finalization or exact failure compensation atomically with operation state and usage evidence. | SB02, SB06, SB08 | Pending |
| RQ-007 | Escalate unresolved compensation to RecoveryRequired; never leave a live active turn behind a terminal failure. | SB02, SB06 | Pending |
| RQ-008 | A durable cancellation request committed before semantic completion must prevent Succeeded. | SB02, SB06, SB08 | Pending |
| RQ-009 | Resolve idempotent replay by operation identity/fingerprint before mutable lifecycle validation. | SB02, SB06 | Pending |
| RQ-010 | Conversation archive must not race an active turn or nonterminal operation. | SB02, SB06 | Pending |
| RQ-011 | Fence every public use case from first read through final commit/return to one database profile identity and generation. | SB03, SB06 | Pending |
| RQ-012 | A profile switch must prevent old-generation writes and produce deterministic retained evidence. | SB03, SB06 | Pending |
| RQ-013 | Use durable cross-instance execution ownership with claim, heartbeat, expiry, and release. | SB04, SB06 | Pending |
| RQ-014 | Support bounded cross-instance cancellation and never infer liveness from an in-memory registry alone. | SB04, SB06, SB08 | Pending |
| RQ-015 | Execute admitted operations independently from the initiating HTTP request through an available dispatcher. | SB04, SB06 | Pending |
| RQ-016 | Never automatically redispatch when durable evidence says a provider dispatch may have started. | SB02, SB04, SB06 | Pending |
| RQ-017 | Use bounded SQL/keyset read models for collection and transcript pagination without N+1 queries. | SB05, SB06 | Pending |
| RQ-018 | Build provider context windows from bounded database reads rather than full transcript materialization. | SB05, SB06 | Pending |
| RQ-019 | Provide an additive provider-neutral incremental LLM invocation port without breaking existing non-streaming callers. | SB07, SB11 | Pending |
| RQ-020 | Implement true incremental streaming for OpenAI, Azure OpenAI, and Ollama, with a deterministic fallback policy. | SB07, SB11 | Pending |
| RQ-021 | Retry a stream only before its first emitted delta and never after partial output is externally visible. | SB07, SB11 | Pending |
| RQ-022 | Persist a bounded per-operation event journal with monotonic sequence and durable replay authority. | SB08, SB11 | Pending |
| RQ-023 | Expose SSE with Last-Event-ID/after replay, gaps, heartbeat, anti-buffering, profile lifetime, and terminal closure. | SB09, SB11 | Pending |
| RQ-024 | SSE/client disconnect must not cancel the durable operation; explicit cancellation remains authoritative. | SB09, SB11 | Pending |
| RQ-025 | Turn start must return 202 Accepted promptly with operation, status, and event links. | SB09, SB11 | Pending |
| RQ-026 | Audit actual provider attempts with deterministic outcomes shared by direct and recovery reducers. | SB02, SB07, SB08, SB11 | Pending |
| RQ-027 | Conversation origin is server-owned and cannot be spoofed by an HTTP client. | SB10, SB11 | Pending |
| RQ-028 | Enforce LLM Chat read/manage/execute API scopes when bearer authorization is enabled. | SB10, SB11 | Pending |
| RQ-029 | Do not expose prompts, system instructions, credentials, raw provider payloads, or raw provider errors through logs/API/SSE. | SB08, SB09, SB10, SB11 | Pending |
| RQ-030 | Keep EF migration, model snapshot, retention, and database-transfer behavior consistent with the hardened schema. | SB01, SB08, SB11 | Pending |
| RQ-031 | Keep implementation portable and prove affected behavior on Linux plus the final Windows/Linux/macOS CI matrix. | SB11, SB13 | Pending |
| RQ-032 | Do not implement UI, shared-component refactoring, floating chat, or Project Structure context in this bundle. | SB12 | Pending |
| RQ-033 | Preserve a clean future LlmChatDeployment boundary for enterprise chatbot channels without dormant deployment fields now. | SB10, SB12 | Pending |
| RQ-034 | Run the expensive stable solution gate and CI matrix once, only at the immutable final head. | SB00, SB13 | Pending |
| RQ-035 | Use filtered affected-scope tests throughout; forbid repeated full Unit/Integration/Solution suites before the final gate. | SB00, SB12, SB13 | Pending |
