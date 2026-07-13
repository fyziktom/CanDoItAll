# C# Dependency Direction

## Target direction

```text
CanDoItAll.Web / CanDoItAll.Composition
  -> Modules.AgentFramework / Modules.Memory
  -> AgentFramework.Memory
  -> Memory.Application
  -> Memory.Abstractions

CanDoItAll.Composition
  -> Memory.Http / Memory.Mcp / Memory.Persistence
  -> Memory.Application
  -> Memory.Abstractions

AgentFramework.Maf
  -> AgentFramework.Models / AgentFramework Core abstractions
  -> typed runtime intent consumed by AgentFramework.Memory

AgentFramework.Models
  -> Memory.Abstractions
```

Transport and persistence implementations point toward application/abstractions. The application layer never points outward to MAF, EF, HTTP, MCP, UI, modules, composition, or native Cognitive Memory.

## Current edge assessment

| Edge | Current status | Target action |
| --- | --- | --- |
| `Memory.Application -> Memory.Abstractions` | Allowed. | Preserve. |
| `Memory.Application -> AgentFramework.Core` | Forbidden inward-layer reversal. | Rehome generic source snapshot contracts into a memory-owned abstraction boundary and adapt MAF outward. Remove the project reference. |
| `Memory.Http -> Memory.Application + Memory.Abstractions` | Temporarily allowed for a narrow driver port. | Preserve only the narrow port; move protocol DTOs to Abstractions and avoid application implementation dependencies. |
| `Memory.Mcp -> Memory.Application + Memory.Abstractions + Mcp.Abstractions` | Temporarily allowed for a narrow driver port. | Preserve only the narrow ports. No MAF runtime/module dependency. |
| `Memory.Persistence -> Memory.Application + Memory.Abstractions + Infrastructure` | Allowed outward implementation direction. | Restrict its DI extension to persistence services; invoke application registration separately. |
| `Modules.AgentFramework -> Memory.Application` | Overbroad. | Move runtime integration to `AgentFramework.Memory`; the module depends on the integration project for UI/composition only. |
| `AgentFramework.Models -> SharedKernel/capability abstractions` | Allowed. | Add only `Memory.Abstractions` for typed settings identifiers/contracts; do not add Application or drivers. |
| `Composition -> Modules.CognitiveMemory` | Forbidden final-state native coupling. | Remove project/import/discovery dependency. Register generic providers only from explicit configuration. |
| External `CognitiveMemory.Service -> main Memory.Http` | Forbidden cross-repository implementation dependency. | Map its endpoints in the external service using shared protocol contracts; the provider service never consumes the client driver. |
| External Contracts/MAF/tests -> sibling main projects | Migration debt. | Reduce to a shared protocol abstraction/package and real optional adapters. Record any remaining sibling reference with owner/removal condition. |

## Forbidden edges

Architecture tests must fail on these edges:

1. `CanDoItAll.Memory.Abstractions` to any MAF, module, persistence, transport, composition, or CognitiveMemory project.
2. `CanDoItAll.Memory.Application` to AgentFramework, EF/Infrastructure persistence, HTTP/MCP implementations, Modules, Web/Composition, or CognitiveMemory.
3. `CanDoItAll.AgentFramework.Memory` to Razor modules, Web/Composition, EF, `Memory.Http`, `Memory.Mcp`, or CognitiveMemory.
4. `CanDoItAll.AgentFramework.Models` to `Memory.Application`, any memory driver, module, or persistence project.
5. Any generic memory project to `CanDoItAll.Modules.CognitiveMemory`, Qdrant, or native CognitiveMemory domain/persistence/UI.
6. `CanDoItAll.Composition` or base module assembly discovery to `CanDoItAll.Modules.CognitiveMemory`.
7. HTTP and MCP driver projects to each other.
8. Provider drivers or the external provider to main `AppDbContext` or module EF entities.
9. External Domain/Application/Persistence to main modules, main composition, main persistence, or the main HTTP/MCP client implementations.
10. UI modules to transport invocation implementations or direct secret stores.

## Runtime dependency rules

Compile-time direction alone is insufficient. These runtime rules are part of the gate:

- A provider profile is selected only from an explicit agent binding/request, assignment, or an explicitly allowed default policy. `Deny` fallback cannot select the first registered provider.
- Agent allowlists are carried into the selection policy and enforced inside the registry/application boundary, not trusted to a caller-side precheck.
- The registry never acts as a service locator. It resolves typed provider registrations/catalog entries; callers do not request arbitrary `IServiceProvider` services by string.
- Multi-provider planning occurs in `AgentFramework.Memory`; the application handler invokes one provider per typed request.
- HTTP/MCP transport errors are mapped to typed failures at the adapter/application boundary. They are not swallowed and they do not terminate an agent run as unclassified exceptions.
- Provider credentials are resolved at the transport boundary through a typed credential reference. Secret values never enter manifests, selection tags, UI view models, or logs.
- Legacy `WorkspaceMemoryContextProvider` is attached only under an explicit compatibility policy and never alongside configured generic automatic memory unless duplicate context is intentionally selected and tested.
- Hosted workers are composed in the host and depend on application interfaces. Registering a scoped worker without a host execution path does not count as implementation.

## Cross-repository protocol direction

The stable direction is:

```text
Shared Memory Protocol contracts
  <- main Memory.Application and client drivers
  <- CognitiveMemory.Service endpoint adapter

CognitiveMemory.Service
  -> CognitiveMemory.Application
  -> CognitiveMemory.Domain

CognitiveMemory.Persistence
  -> CognitiveMemory.Application ports + Domain
```

The shared protocol may initially be the main `CanDoItAll.Memory.Abstractions` project while both repositories are developed as siblings. The closure record must state whether this is an intentional source dependency or a published package plan. It must not be obscured by having the service reference the main HTTP driver.

## Composition ownership

The host registers layers explicitly and in order:

1. generic memory application services;
2. persistence implementation if configured;
3. each configured transport driver (`Http`, `Mcp`, test-only `Mock`);
4. AgentFramework memory integration;
5. memory/agent UI modules;
6. hosted workers only for enabled, durable features.

No registration extension in Persistence may silently register every layer. No native provider is registered because an assembly happens to be present. Zero-provider startup is a supported, tested configuration.

## Enforcement commands and evidence

At each checkpoint, capture:

- `dotnet list <project> reference` for all changed boundary projects;
- CodeAnalytics project dependency and cycle analysis scoped to Memory, AgentFramework memory/MAF/models, modules, composition, and external CognitiveMemory projects;
- `rg` guard scans for forbidden project references/namespaces/package names;
- architecture tests that parse project references and source namespaces;
- composition smoke tests for zero-provider, HTTP-only, MCP-only, and two-provider configurations.

No-cycle output is necessary but not sufficient: a forbidden acyclic edge still fails this gate.

