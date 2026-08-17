# C# dependency direction

## Intended graph

Modules.AgentFramework -> SimpleChats.Components -> SimpleChats.Application -> SimpleChats.Core

SimpleChats.Runtime -> SimpleChats.Application -> SimpleChats.Core

SimpleChats.Persistence -> SimpleChats.Application -> SimpleChats.Core

SimpleChats.Runtime -> existing Llm.Abstractions / Llm.Conversations / Llm.ProviderRuntime / Providers

SimpleChats.Persistence -> AgentFramework.Usage

AgentFramework.Core -> AgentFramework.Usage

AgentFramework.Usage -> AgentFramework.Models

App.Composition -> Modules.AgentFramework + SimpleChats.Runtime + SimpleChats.Persistence

Web API adapters -> SimpleChats.Application + SimpleChats.Core

PostgreSQL migrations -> SimpleChats.Persistence

## Forbidden edges

- SimpleChats.Core -> Application, Runtime, Persistence, Components, Modules, Web, EF.
- SimpleChats.Application -> Runtime, Persistence, Components, Modules, Web, EF.
- SimpleChats.Runtime -> Persistence or AppDbContext.
- SimpleChats.Persistence -> Runtime, Components, Modules.AgentFramework, Web.
- SimpleChats.Components -> Runtime, Persistence, AgentFramework.Core, Modules.AgentFramework, Web.
- AgentFramework.Usage -> either source implementation.
- AgentFramework.Core -> SimpleChats.Persistence.
- Any new project -> old CanDoItAll.Modules.LlmChats* after SB10.
- Any reflection/service-location edge used to hide a compile-time cycle.

## Migration sequence

1. Add Usage contracts before either producer depends on them.
2. Add Core and Application projects and move contracts/behavior together in SB03.
3. Retarget old Persistence/UI/API callers to Core/Application.
4. Extract Runtime without EF references.
5. Move Persistence and add append-only usage evidence.
6. Implement source adapters and aggregate queries.
7. Move Components, then activate Agent module composition.
8. Remove old projects and namespaces after caller inventory reaches zero.

## Cycle proof

At CP0, CP1, CP2, CP4, and FINAL:

- record direct ProjectReference before/after graph;
- run CodeAnalytics dependency/cycle analysis;
- compare known baseline cycles by identity;
- fail on any new cycle, enlarged baseline cycle, or forbidden edge;
- run a source guard for CanDoItAll.Modules.LlmChats and old project paths.

No “build succeeds” claim substitutes for the dependency proof.

