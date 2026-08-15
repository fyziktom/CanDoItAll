# Acceptance evidence — SB05

For each criterion, provide behavioral/source evidence rather than only a test count.

- [x] Transcript paging executes a bounded SQL query and never materializes the full transcript.
- [x] Conversation and definition listings do not issue one query per item.
- [x] Context-window construction reads only the bounded entries it can send.
- [x] Externally exposed collections use deterministic cursors and enforced page limits.
- [x] Large-transcript tests prove stable memory/query behavior without changing canonical content.

## Required semantic proof

- Intended case: keyset-page definitions, conversations, and 2,000 canonical transcript entries while
  constructing a 12-message provider context with fixed SQL command counts.
- Negative/race/crash/failure case: invalid typed cursors fail validation; operation-detail overflow
  fails predictably; the prior implementation's per-item and in-memory offset paths are absent.
- Why the old implementation would fail this proof: historical source at `c0bc6d0` performs per-item
  definition/conversation reads and transcript `Skip(offset)` after loading a full document.
- Exact source owner: application `ILlmChat*ReadStore` contracts, persistence `EfLlmChat*ReadStore`
  adapters, and `EfLlmConversationTurnStore`.
- Exact command(s): focused Unit filter for conversation/query owners; exact
  `LlmChatBoundedReadModelIntegrationTests.Large_transcript_and_collection_reads_remain_keyset_bounded_with_constant_query_counts`
  PostgreSQL test; current/historical source guards.
- Actual result: Unit 42/42; PostgreSQL 1/1; fixed list/page/context SQL counts; source guards pass.
- Evidence artifact: `proof/SB05/`.
- Commit SHA: `e88987c2018adcf9118d49109eb8d4e3d3eb2c12`.
