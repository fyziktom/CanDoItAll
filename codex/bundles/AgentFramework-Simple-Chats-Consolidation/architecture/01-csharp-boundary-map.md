# C# target boundary map

## Target projects

### CanDoItAll.AgentFramework.Llm.SimpleChats.Core

Path: src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Core

Owns:

- LlmChatDefinitionId, ConversationId, OperationId and other strong identifiers;
- definition/revision and conversation aggregates;
- operation states, events, reducers, transitions, invariants;
- validation, fingerprints, cursor/value objects.

Allowed references:

- CanDoItAll.AgentFramework.Llm.Abstractions;
- narrowly required AgentFramework.Models contracts;
- CanDoItAll.SharedKernel.

Forbidden: DI registration, Application use cases, EF, AppDbContext, provider implementation, Runtime, Razor, Web, Agent module.

### CanDoItAll.AgentFramework.Llm.SimpleChats.Application

Path: src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Application

Owns:

- commands, queries, results, application services;
- admission, dispatch, execution-state orchestration, cancellation/recovery policy;
- repository/read/evidence/runtime/profile-fence ports;
- durable event session abstraction and application DI.

Allowed references: Core, provider-neutral LLM contracts, logging/DI abstractions.

Forbidden: EF implementations, AppDbContext, concrete provider adapters, Razor, Web, Agent module.

### CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime

Path: src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime

Owns:

- canonical provider resolver;
- completed/streaming provider invocation adapters;
- audited invocation decorators;
- conversation engine and provider execution orchestration;
- Runtime DI that consumes Application ports.

Allowed references: Core, Application, AgentFramework.Llm.* generic runtime libraries, AgentFramework.Providers, AgentFramework.Usage.

Forbidden: EF, AppDbContext, Razor, Web, Agent module, persistence concrete types.

### CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence

Path: src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence

Owns:

- EF rows/configurations/repositories/read stores;
- database transfer/import/export;
- relational leases, heartbeats, commit fences and database-profile generation/fresh-scope implementations;
- Simple Chat usage-source adapter over invocation records;
- Persistence DI.

Allowed references: Core, Application, AgentFramework.Usage, Infrastructure/EF.

Forbidden: provider runtime construction, Razor, Web, Agent module.

### CanDoItAll.AgentFramework.Llm.SimpleChats.Components

Path: src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Components

Owns:

- reusable workspace body with inner Conversations/Definitions tabs;
- definition catalog/editor and conversation workspace;
- UI gateways, presentation models/mappers, reducer/follower;
- authorization facade contract/adapter seam;
- floating conversation-shell contributor/content;
- cohesive controller collaborators and Components DI.

Allowed references: Core, Application, AppComponents, Conversations.Components, Conversations.Shell, BaseLib.

Forbidden: @page, shell navigation item, EF/Persistence, Runtime, Web, Agent module.

### CanDoItAll.AgentFramework.Usage

Path: src/MAF/Common/CanDoItAll.AgentFramework.Usage

Owns:

- ProviderUsageWorkloadKind and validated ProviderUsageWorkloadSelection semantics;
- normalized source contribution, consumer, provider, model, totals, completeness and freshness contracts;
- IProviderUsageProjectionSource;
- aggregation/query service;
- provider usage normalization/pricing orchestration extracted from Agent execution where genuinely cross-workload.

Allowed references: AgentFramework.Models and low-level BCL/extensions abstractions.

Forbidden: Agent persistence, Simple Chat persistence, Razor, Web, module types.

## Product/composition owners

### CanDoItAll.Modules.AgentFramework

Owns:

- Simple Chats tab and typed top-level route catalog;
- /chats compatibility redirect;
- removal of duplicate Simple Chats shell navigation;
- use of reusable Components and unified Usage query in the Agent page;
- scope-aware dashboard/dialog orchestration.

It does not own Simple Chat Core, Application, Runtime, Persistence, or reusable component internals.

### CanDoItAll.Composition

Owns:

- Application/Runtime/Persistence registrations;
- hosted dispatcher registration;
- both IProviderUsageProjectionSource registrations;
- outer options and lifecycle wiring.

### CanDoItAll.Web

Owns:

- existing HTTP/SSE endpoint adapters and UI authorization adapter;
- namespace/project reference cutover to Core/Application contracts.

## Compatibility policy

- Temporary old-namespace forwarding is allowed only if CP0 finds a non-monorepo/public binary consumer.
- Any temporary facade is delegation-only, guarded against new callers, and deleted in SB10.
- /chats compatibility is a browser route adapter, not an old feature assembly.
- Historical EF migrations are not rewritten.

