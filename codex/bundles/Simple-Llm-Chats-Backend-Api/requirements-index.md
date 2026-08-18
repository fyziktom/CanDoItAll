# Requirements index

| ID | Requirement | Source | Implemented by | Proof |
|---|---|---|---|---|
| R-001 | Simple chats are ordinary LLM conversations, not agents | Architect notes | SB00–SB06 | architecture guards + unit tests |
| R-002 | Reusable name, avatar, system prompt, provider, model, temperature/settings, and typed thinking-effort override | Architect notes + thinking-effort follow-up | SB01–SB04/SB07 | model + revision + provider-capability + API tests |
| R-003 | Durable multi-turn history | Architect notes | SB03–SB05 | PostgreSQL CAS tests |
| R-004 | API-first verification before UI | Current request | SB07–SB09 | real-host API tests |
| R-005 | No UI implementation in this bundle | Current request | all | forbidden-path guard |
| R-006 | Future UI can list agents and simple chats without redefining identity | Architect notes | SB01/SB02 | `LlmChatDefinitionSummary` contract |
| R-007 | Future explicit project/node/subtree context can be added without changing transcript identity | Architect notes | architecture only; concrete source deferred | documented boundary + CP1 review |
| R-008 | Future ordinary enterprise chatbot/channel deployment | Current request | SB01/SB02 + architecture docs | future-readiness review |
| R-009 | Provider and model settings are validated server-side through provider-neutral contracts | Architecture review | SB00/SB04 | provider-resolution and contract-ownership tests |
| R-010 | Definition edits do not silently change existing threads | Architecture review | SB01–SB04 | immutable revision tests |
| R-011 | Cross-profile result cannot commit | Existing runtime contract | SB04/SB05 | switch-before/after-dispatch tests |
| R-012 | Duplicate client retry cannot cause a second paid invocation | Architecture review | SB05/SB08 | idempotency HTTP tests |
| R-013 | Crash after transcript completion can reconcile operation state | Architecture review | SB05 | reconciliation tests |
| R-014 | Cancellation is durable and reaches in-process provider calls | Architecture review | SB05/SB08 | cancellation tests |
| R-015 | Known failed/retried usage is audited outside transcript | Architecture review | SB05 | invocation audit tests |
| R-016 | PostgreSQL is the production store; file store remains isolated | Repository evidence | SB03/SB06 | DI and boundary tests |
| R-017 | Windows/Linux/macOS portability is preserved | Current request/repo | all/SB11 | static guard + CI matrix |
| R-018 | Broad test suites run only at final gate | Current request | all/SB11 | test-policy script + proof manifests |
| R-019 | API never accepts a full live `ProviderProfile` or credentials | Architecture review | SB07/SB08 | DTO/source guard tests |
| R-020 | API exposes optimistic revision and typed stable errors | Architecture review | SB07/SB08 | ETag/ProblemDetails tests |
| R-021 | Archived/suspended definitions provide a kill switch | Enterprise readiness | SB01–SB04/SB07 | lifecycle tests |
| R-022 | External channels remain a later deployment aggregate | Enterprise readiness | architecture only | CP1/CP2 review |
| R-023 | Profile-switch and transcript commit have a proven synchronization boundary | Runtime correctness | SB00/SB04/SB05 | deterministic race tests |
| R-024 | Database profile transfer preserves LLM Chat data and referential integrity | Existing product lifecycle | SB03/SB06/SB09 | focused transfer round-trip test |
| R-025 | Provider-backed invocation registration is provider-runtime-owned and does not depend on workflow activation | Architecture review | SB00/SB04/SB06 | contract-ownership and composition tests |
| R-026 | Thinking-effort availability and validation are provider/model-specific, reuse the canonical provider capability truth, support provider default versus explicit `None`, and reach invocation/audit | Thinking-effort follow-up | SB00/SB01/SB02/SB04/SB05/SB07/SB09 | capability projection + revision fingerprint + dispatch/audit + HTTP tests |
