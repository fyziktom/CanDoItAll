# Architecture decision register

| ID | Decision | Status | Owner |
|---|---|---|---|
| ADR-H01 | One writable canonical conversation/transcript metadata owner | Implemented by SB01 |
| ADR-H02 | Product cross-table invariants use explicit single-context commands | Implemented by SB01 |
| ADR-H03 | Operation transition/recovery uses one pure reducer | Locked by SB02 |
| ADR-H04 | Whole application use case is profile-generation fenced | Locked by SB03 |
| ADR-H05 | Provider execution is claimed durably and detached from HTTP request | Locked by SB04 |
| ADR-H06 | Long transcript/list queries use read models and keyset paging | Locked by SB05 |
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
