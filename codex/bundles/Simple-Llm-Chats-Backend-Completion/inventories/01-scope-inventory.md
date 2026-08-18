# Scope Inventory

## Planned Production Owners

| Area | Existing owner | Planned change kind |
| --- | --- | --- |
| Definition/conversation HTTP | `repo://src/App/CanDoItAll.Web/Api/LlmChatsApi.cs` and contracts/mapper/results | Split endpoint ownership; validation, metadata, editor/read security |
| Operation HTTP/SSE | `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs` and operation contracts/mappers | Reconcile route, sanitized invocation DTO, completion evidence, fingerprint removal |
| Definition/conversation application | `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application` | Atomic guarded creation and stable CAS mapping |
| Operation application | same module | Replay admission ordering, no-throw cancellation notification, evidence recovery |
| Executor/dispatcher | same module plus Composition hosted service | Provider task supervision and bounded workers/age/duration |
| Event journal/signal/retention | same module | Durable high-water contract, safe transient eviction, bounded cleanup scheduling |
| PostgreSQL repositories | `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories` | Coherent replay snapshot, event-rooted row batches, CAS translation |
| Persistence schema | entities/configurations plus migrations | Finish reason/delivery/high-water and configured bounds where required |
| Database transfer | persistence transfer documents/services | New field parity, relationship validation, bounded import |
| Shared provider runtime | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime` | Safe structured logging and consistent consumer-abort outcome |
| Documentation | `repo://docs/llm-chats-api.md`, `repo://docs/testing.md`, architecture handoff | Align public contract and current commands |

## Test Owners

| Boundary | Existing test owner |
| --- | --- |
| Domain/application/executor/options/DI | `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatOperationTests.cs`, `LlmChatConversationApplicationServiceTests.cs`, `LlmChatDurableStreamEventTests.cs`, `LlmChatProviderRuntimeTests.cs` |
| Provider runtime logging/attempts | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderBackedLlmStreamingInvocationAdapterTests.cs` |
| API contracts/security | `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiIntegrationTests.cs`, `LlmChatsTurnApiIntegrationTests.cs` |
| PostgreSQL races/replay/retention | `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatPersistenceIntegrationTests.cs`, `LlmChatsApiPostgreSqlIntegrationTests.cs` |
| Generic SSE writer | `repo://tests/Integration/CanDoItAll.Tests.Integration/ApiStreamingTransportTests.cs` |
| Migration/transfer | LLM Chat persistence tests plus focused migration bootstrap/pending-model checks |

## Explicitly Untouched

- All Razor/Blazor/component/CSS/JavaScript/Playwright chat UI surfaces.
- Agent execution, agent tools, skills, MCP, memory, processes, Workbench, Project Structure, and workspace authority.
- File-backed generic conversation registration.
- Public multi-tenant ownership model.
- New project files or project references unless an architecture checkpoint explicitly reopens and approves them; the prepared plan assumes none.
