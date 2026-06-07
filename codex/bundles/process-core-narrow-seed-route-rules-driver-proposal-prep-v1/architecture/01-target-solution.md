# Narrow Core Seed Cutline

## Proposed new project

`src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`

Allowed dependency:

- `CanDoItAll.Processes.Contracts`

Potentially allowed only if unavoidable and justified:

- `CanDoItAll.SharedKernel` for generic result/value helpers, but prefer no reference in the first seed.

Forbidden:

- Modules
- Infrastructure
- AgentFramework
- EF
- Workspace
- Storage
- UI
- Plugins
- MAF
- driver namespaces

## First production move

Move or duplicate-then-switch only the pure route family:

- `ProcessDispatchRouteStage`
- `ProcessDispatchRoutePipeline.StageOrder`
- route stage descriptor/read-model
- route eligibility decision helpers currently used by process dispatch rules
- route-order assertion only if it has no module-local dependency

Recommended target namespace:

`CanDoItAll.Processes.Core.Routing`

## Module compatibility

The Processes module should consume Core via local adapter/wrapper only where necessary.

No runtime orchestration class moves to Core.
