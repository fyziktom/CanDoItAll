# C# dependency direction

## Required before/after proof

At CP1 and CP2 capture:

- solution project graph;
- cycles;
- new references;
- exact namespaces used by `CanDoItAll.Modules.LlmChats`;
- exact namespaces used by `Llm.Abstractions` and provider contracts;
- source assertions for forbidden tokens.

## No new dependency is justified for these shortcuts

- Referencing `CanDoItAll.Web` to publish SSE from the product.
- Referencing `Modules.AgentFramework` to resolve provider data.
- Referencing Workflows merely to reuse `ILlmInvocationPort` registration.
- Referencing AgentFramework Core to reuse agent activity or execution types.
- Referencing Workbench/Projects for future context placeholders.
- Referencing UI component packages for future chat target models.

## Composition

A composition extension may register:

- product application services;
- persistence implementations;
- dispatcher hosted/scoped services;
- provider streaming adapter;
- event journal publisher;
- API stream adapter.

The composition extension must not implement retry, state transitions, provider parsing, or query logic.

## Source guard expectations

The architecture guard must fail when:

- product project references Web, Components, MAF agent runtime, tools, skills, MCP, memory or processes;
- provider contracts reference Simple Chats;
- product command code resolves `IServiceProvider`;
- an independent `AppDbContext` is created inside an active command;
- new production partial files are added to operation/conversation services.
