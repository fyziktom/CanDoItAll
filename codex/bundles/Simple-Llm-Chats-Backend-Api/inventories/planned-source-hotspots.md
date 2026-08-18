# Planned source hotspots

The executor must revalidate these paths at SB00.

## Existing generic lightweight path

- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmConversationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmInvocationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowLlmServiceCollectionExtensions.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Providers`
- current `IProviderRuntimeProfileSource` and model-capability resolver owners discovered by SB00

## Database/runtime

- `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`
- `src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs`
- `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations`
- `src/Foundation/CanDoItAll.Migrations.PostgreSql/PostgreSqlAppDbContextFactory.cs`

## Composition and API

- `src/App/CanDoItAll.Composition`
- `src/App/CanDoItAll.Web/Api`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
  — inspect provider registration only; do not place LLM Chat product behavior here.

## Tests

- `tests/Unit/CanDoItAll.Tests.Unit`
- `tests/Integration/CanDoItAll.Tests.Integration`
- `docs/testing.md`

## Forbidden UI hotspots

No production change is allowed under:

- `**/*.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components`
- agent chat/floating chat panel or coordinator files
- shared UI component projects
