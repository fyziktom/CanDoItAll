# SB01 semantic invariants

| Invariant | Evidence | Result |
|---|---|---|
| Title and conversation timestamps have one writable owner. | `LlmChatConversationRow` owns `Title`, `CreatedAtUtc`, and `UpdatedAtUtc`; those fields are absent from `LlmChatTranscriptRow` and the current EF snapshot. | Pass |
| Transcript lifecycle state has one writable owner. | `LlmChatTranscriptRow` owns transcript revision, entry count, acceleration, and active-turn fields; conversation reads join canonical metadata. | Pass |
| Create is atomic. | A PostgreSQL exception injected after transcript flush rolls back product and transcript rows. | Pass |
| Rename is atomic. | A PostgreSQL exception injected after transcript revision update rolls back both the canonical title and transcript revision. | Pass |
| Legacy divergence is not silently discarded. | The migration raises when legacy title copies differ before dropping the transcript copy. | Pass |
| Schema artifacts agree. | Migration/model/transfer tests pass and EF reports no pending model changes. | Pass |
| The old fake unit of work is unreachable. | `EfLlmConversationStore` receives `AppDbContext`; no factory/context creation remains in the store. | Pass |
