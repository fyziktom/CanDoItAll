# Project Boundary Map

Use this map when deciding whether a responsibility belongs in an existing project or a new project.

## Generic project roles

| Project kind | Owns | Must not own |
|---|---|---|
| Contracts | DTOs, public records, stable protocol shapes | runtime behavior, SDK dependencies, persistence |
| Abstractions | interfaces, small option records, extension contracts | concrete implementations, external SDK usage |
| Core | domain logic, orchestration-independent rules | infrastructure, UI, provider SDKs |
| Application | use cases, application services, policy coordination | SDK-specific clients, UI rendering |
| Runtime | execution loop, lifecycle coordination, event emission | concrete provider catalogs, external SDK details |
| Builder | definition builders, validation before runtime execution | provider invocation, persistence |
| Drivers | domain-specific execution decisions behind contracts | generic process core logic |
| Providers | provider implementations behind abstractions | application orchestration and UI |
| Tools | tool definitions and tool execution modules | runtime monolith registration logic |
| Persistence | storage implementations, EF, filesystem stores | domain policy decisions |
| Projections | read models and UI-facing projections | write-side orchestration |
| UI | components, view models, browser interactions | infrastructure implementation logic |

## CanDoItAll-shaped examples

The current solution style already supports separated groups such as:

- Memory: Abstractions, Application, Http, Mcp, Persistence.
- MAF: Common, Capabilities, Mcp, Skills, Tools, Workflows, WorkflowExecutors.
- Processes: Contracts, Abstractions, Core, Builder, Application, Projections, Persistence, Runtime, Templates, Drivers.
- Modules: UI or module integration projects that should not become dumping grounds for core behavior.

When adding a new memory provider, process driver, tool family, or workflow executor, prefer a new implementation project under the relevant group when the code introduces SDKs, distinct lifecycle, separate tests, or extension growth.

## Dependency direction

Allowed direction usually looks like this:

```text
UI/Module/Composition
  -> Application/Runtime/Builder
  -> Core
  -> Abstractions/Contracts

Infrastructure/Providers/Persistence/Drivers
  -> Abstractions/Contracts/Core
```

The composition root may reference implementations so it can wire them, but core abstractions must not reference implementation projects.

## Cycle prevention rule

If the desired extraction would create a cycle, do not add the reference. Extract a smaller contract project first, move shared DTOs/interfaces there, then make both sides depend on the contract project.
