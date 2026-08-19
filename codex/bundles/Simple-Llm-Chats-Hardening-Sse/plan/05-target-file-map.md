# Target file map

This is a discovery map, not permission to edit every file.

| Area | Existing hotspots | Likely new focused owners |
|---|---|---|
| Canonical conversations | `Application/LlmChatConversationApplicationService.cs`, EF conversation store/rows | transactional conversation command store; query service |
| Operations | `Application/LlmChatOperationApplicationService.cs`, operation entity | reducer; admission/finalization command store; dispatcher |
| Recovery/audit | `LlmConversationService.cs`, `AuditedLlmChatInvocationPort.cs`, evidence service | durable attempt reducer; recovery service |
| Profile | runtime lease/engine | application operation scope factory |
| Claims | cancellation registry | execution lease repository; worker/dispatcher |
| Queries | EF conversation store/repositories | keyset read repositories; bounded context loader |
| Streaming abstractions | `LlmInvocationContracts.cs` | streaming update contracts/port |
| Provider capabilities | provider contracts | streaming driver contract/capability |
| OpenAI/Azure/Ollama | provider driver files | wire stream parsers/adapters |
| Journal | operation rows/configs | event row/repository/coalescer/retention |
| API | LLM chat operation API | 202 DTOs; event endpoint |
| Generic SSE | existing streaming helpers | reuse; only narrow race-free durable handoff additions |
| Migration | PostgreSQL migration project | one new migration and snapshot |
| Tests | Unit/Integration projects | named focused test classes in subbundle docs |

## Files that must not receive Simple Chat feature logic

- `Program.cs`
- agent chat panels/components
- floating agent chat coordinator
- process runtime
- Workbench Project Structure context
- generic SSE serializer beyond transport-level reusable behavior
