# Architecture review gate

Status: Pass.

- Product owners: explicit `ILlmChatDefinitionReadStore`, `ILlmChatConversationReadStore`, and
  `ILlmChatOperationReadStore` contracts plus the MAF `ILlmConversationTurnStore` contract.
- Persistence adapters: `EfLlmChat*ReadStore` own bounded canonical projections;
  `EfLlmConversationTurnStore` owns atomic turn writes and bounded SQL context reads.
- Composition: registers the read/turn adapters against the scoped `AppDbContext`; it does not
  implement query or context policy.
- Web: maps opaque typed cursors and request/response models without owning sort or continuation rules.
- Old path: command repositories no longer expose collection queries; application list loops and
  transcript document `Skip(offset)` are absent; the EF turn store never calls the full-document load.
- Testability: focused Unit tests cover the product owner, while direct PostgreSQL command interception
  covers query owners with 2,000 messages and multiple definitions.
- Partials/references: no production partial expansion and no forbidden project reference.
- Snapshot: CodeAnalytics `snap-20260815034954-c4aa2a0f`, four scoped projects, zero cycles, zero
  diagnostics, zero error findings. The four warning findings are existing large-file heuristics.

ADR-H06 was implemented as prepared: CQRS-style bounded read models over canonical tables with no
second persistence truth.
