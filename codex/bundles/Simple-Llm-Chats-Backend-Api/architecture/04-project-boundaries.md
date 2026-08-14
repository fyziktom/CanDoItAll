# Project boundaries and dependency direction

## New projects

### `CanDoItAll.Modules.LlmChats`

SDK: `Microsoft.NET.Sdk`

Owns:

- canonical product models and value objects;
- application commands/results;
- repository and unit-of-work ports;
- definition and conversation use cases;
- operation/idempotency/reconciliation orchestration;
- provider-resolution and runtime-fence abstractions;
- current product commands/results and the minimal conversation-origin values consumed by this bundle;
- documented, but not implemented, boundaries for future context, attachment, and deployment work;
- product errors.

Allowed references:

- `CanDoItAll.SharedKernel`;
- `CanDoItAll.AgentFramework.Llm.Abstractions`;
- `CanDoItAll.AgentFramework.Models` only where the existing provider-neutral profile model is needed;
- existing typed provider/model thinking-effort contracts through that same provider-neutral dependency;
- no provider catalog implementation; provider resolution remains behind the module-owned port.

Forbidden references:

- AgentFramework Core;
- AgentFramework MAF;
- Tools, Skills, MCP, Memory;
- Processes;
- Workbench, Projects, CRM/HR;
- ASP.NET Core;
- EF Core;
- product UI projects.

### `CanDoItAll.Modules.LlmChats.Persistence`

SDK: `Microsoft.NET.Sdk`

Owns:

- EF entities and `IEntityTypeConfiguration<T>`;
- PostgreSQL `ILlmConversationStore`;
- product repositories and unit of work;
- database-profile runtime lease/fence implementation;
- operation cancellation registry bridge;
- persistence registration.

Allowed references:

- `CanDoItAll.Modules.LlmChats`;
- `CanDoItAll.Infrastructure`;
- `CanDoItAll.AgentFramework.Providers` for the extracted/read-only provider profile and model-capability contracts;
- `CanDoItAll.AgentFramework.Llm.ProviderRuntime` only for provider-backed invocation composition where required;
- EF Core;
- lightweight LLM abstractions and the ordinary-conversation implementation required by the store/engine.

Forbidden references:

- Web/API;
- Razor/UI;
- MAF, tools, skills, MCP, Memory, Processes;
- Workbench context implementations.

## Existing projects touched

- `Llm.Abstractions`: additive conversation request identities only if their current owner is confirmed.
- `Llm.Conversations`: consume the optional IDs and retain all old behavior.
- `AgentFramework.Providers`: own narrow provider-profile read and model-capability contracts when SB00 confirms they still live in Core.
- `Llm.ProviderRuntime`: own the idempotent `ILlmInvocationPort` registration seam; Workflows and LLM Chats consume that seam rather than owning duplicate registrations.
- `CanDoItAll.Composition`: register module/persistence assembly and services.
- `CanDoItAll.Web`: map HTTP adapters.
- `Migrations.PostgreSql`: append migration and update snapshot.
- tests: focused Unit and Integration classes.
- documentation and solution files.

## Dependency graph

```text
Web
  -> Modules.LlmChats
  -> Composition

Composition
  -> Modules.LlmChats
  -> Modules.LlmChats.Persistence

Modules.LlmChats.Persistence
  -> Modules.LlmChats
  -> Infrastructure
  -> AgentFramework.Providers
  -> Llm.ProviderRuntime
  -> Llm.Conversations / Llm.Abstractions

Modules.LlmChats
  -> Llm.Abstractions
  -> SharedKernel

Llm.Conversations
  -> Llm.Abstractions
  -> Models
```

No reverse edge is permitted.

The existing `AgentReasoningEffortLevel`, `ProviderModelThinkingEffortCapability`, and
`AgentThinkingEffortPolicy` names predate this product, but their behavior is provider/model policy and
does not construct or execute agents. SB00 must record that reuse explicitly. LLM Chats must not add a
parallel effort enum, parse effort strings in domain logic, or infer capabilities from model names.
