# Risk Register

| Risk | Probability | Impact | Control |
|---|---:|---:|---|
| Shared components become a second universal domain model | Medium | High | Keep presentation keys opaque, models source-neutral, and product mapping in adapters. |
| Streaming UI persists or duplicates partial output | Medium | Critical | Treat deltas as transient projection; refresh canonical transcript only after terminal evidence. |
| Browser/circuit disposal cancels paid provider work | Medium | Critical | Separate follower lifetime from durable operation lifetime; explicit cancel only. |
| Active operation cannot be rediscovered after refresh | High today | High | Add exact ActiveOperationId before UI. |
| Unsafe LLM Markdown link executes in browser | Low/Medium | High | Explicit safe-scheme policy and hostile-link tests. |
| Unified floating host regresses context-aware Agent chat | Medium | High | CP2 before floating work; contributor adapter; targeted Agent context and affinity Playwright proof. |
| Direct in-process UI bypasses API authorization semantics | Medium | High | Typed UI authorization facade aligned to Read/Manage/Execute policies. |
| Proof is silently ignored again | High today | Medium | SB01 .gitignore exception and checksum/proof reconciliation; validator asserts durable paths. |
| Test cost expands into repeated hours-long gates | High | Medium | Impacted-test selection per diff; one Stable gate only in SB12. |
| Context button becomes dead or prompt-text hack | Medium | High | Explicit exclusion; retain unused slot until typed context aggregate exists. |
