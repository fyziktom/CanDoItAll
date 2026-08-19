# Acceptance evidence — SB01

- [x] Conversation title and transcript metadata have exactly one canonical writable owner.
- [x] Conversation creation commits product binding and transcript root together or commits neither.
- [x] Conversation rename updates the canonical title once and cannot leave divergent rows.
- [x] No production conversation store creates a second AppDbContext inside an active product command.
- [x] Migration and transfer payloads preserve the repaired canonical model.

## Required semantic proof

- Intended case: create/rename/read/transfer use canonical conversation title/timestamps and transcript-owned revision/turn state.
- Negative/race/crash/failure case: failure after transcript flush rolls back both rows; divergent legacy titles abort migration.
- Why the old implementation would fail this proof: it created and committed an independent `AppDbContext`, leaving orphan/divergent state across the product unit of work.
- Exact source owner: `LlmChatConversationRow`, `LlmChatTranscriptRow`, and scoped-context `EfLlmConversationStore`.
- Exact commands: `proof/SB01/transcripts/01-red-atomicity.md` through `05-validator-results.md`.
- Actual result: red 0/2; final PostgreSQL 7/7; application unit 5/5; Web build 0 warnings/errors; EF model current.
- Evidence artifact: `proof/SB01/manifest.md`.
- Commit SHA: `689f2b5368bf6fdba7fad24dfa6fa4dee9b4abfc`.
