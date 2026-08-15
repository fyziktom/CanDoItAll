# Architecture decision register

| ID | Decision | Status | Owner |
|---|---|---|---|
| ADR-H01 | One writable canonical conversation/transcript metadata owner | Implemented by SB01 |
| ADR-H02 | Product cross-table invariants use explicit single-context commands | Implemented by SB01 |
| ADR-H03 | Operation transition/recovery uses one pure reducer | Locked by SB02 |
| ADR-H04 | Whole application use case is profile-generation fenced | Implemented by SB03 |
| ADR-H05 | Provider execution is claimed durably and detached from HTTP request | Proven by SB04 at `7389daff6c21a4568895e514debe110434908d67` |
| ADR-H06 | Long transcript/list queries use read models and keyset paging | Proven by SB05 at `e88987c2018adcf9118d49109eb8d4e3d3eb2c12` |
| ADR-H07 | Streaming is additive; completed invocation remains supported | Locked by SB07 |
| ADR-H08 | Retry stops after first accepted delta | Locked by SB07 |
| ADR-H09 | Deltas are journal events, not canonical transcript messages | Locked by SB08 |
| ADR-H10 | Turn admission returns 202; SSE reuses generic writer/profile stream | Locked by SB09 |
| ADR-H11 | API origin is server-owned and scopes are read/manage/execute | Locked by SB10 |
| ADR-H12 | UI/context/chatbot deployment remain later bundles | Locked |

## SB01 implementation record

`LlmChatConversationRow` is the canonical owner of title and conversation timestamps.
`LlmChatTranscriptRow` owns provider snapshot, transcript revision, entry count, acceleration, and
active-turn state. `EfLlmConversationStore` joins the canonical conversation metadata for reads and
participates in the scoped `AppDbContext` transaction for writes. No ambient transaction or nested
context is used.

## SB03 implementation record

Every public LLM Chat application interface is registered through an internal profile-scoped decorator.
`LlmChatProfileScopeRunner` acquires the immutable canonical host identity before invoking application
behavior and keeps it through the authoritative return. `EfLlmChatUnitOfWork` commits only through
`DatabaseProfileLlmChatCommitFence`; the shared runtime state total-orders profile-switch publication
and durable commits. Provider and transcript adapters consume the same operation scope. No service
re-resolves the current profile after admission, and a restarted host cannot be mistaken for the old
host's newly selected profile.

## SB05 implementation record

Application query contracts define explicit definition, conversation, operation, and transcript read
models over the canonical tables. EF adapters own deterministic updated-at/id or sequence keysets and
enforce bounded `Take` operations before materialization. `ILlmConversationTurnStore` is the production
turn boundary; its EF adapter reads state, system entries, and only the newest bounded non-system range.
Web owns opaque cursor transport, not ordering policy. Command repositories no longer expose collection
queries, and no second persistence truth was introduced.
