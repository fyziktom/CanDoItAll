# C# Dependency Direction

## CodeAnalytics Dependency Evidence

- Snapshot: `snap-20260708104406-98263759`
- Dependency query result: no cycles reported (`[]`).
- Scoped inventory shows `CanDoItAll.Modules.Processes` composes process runtime/application/templates plus AgentFramework core/models. Runtime does not reference module integration.

## Current Direction

```text
CanDoItAll.Modules.Processes
  -> AgentFramework.Core/Models
  -> Processes.Application/Runtime/Templates/Projections/Builder/Drivers

Processes.Application
  -> Processes.Runtime
  -> Processes.Templates
  -> Processes.Projections
  -> Processes.Drivers.Abstractions

Processes.Runtime
  -> Processes.Abstractions/Contracts/Core/Builder/Drivers.Abstractions

Processes.Templates
  -> Processes.Abstractions/Contracts/Core
```

## Target Direction

```text
UI/Module composition
  -> Application services and module integration
  -> Runtime state machine / Drivers abstractions / Template contracts
  -> Core / Abstractions / Contracts

Infrastructure providers and AgentFramework adapters
  -> Abstractions / Contracts / Driver contracts
```

## Forbidden References

- `CanDoItAll.Processes.Runtime` must not reference `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Workbench`, or AgentFramework concrete execution services.
- `CanDoItAll.Processes.Contracts` and `.Abstractions` must not reference template loader implementation, project-structure providers, or AgentFramework concrete models unless the contract is deliberately moved to an AgentFramework-facing assembly.
- `CanDoItAll.Processes.Templates` must not depend on runtime mutation or module integration to validate documents.
- MAF core must not depend on process template metadata for branch semantics.

## New Contract Placement Rules

- If a type is required by both application and module integration, place a small contract/interface in an existing abstractions/drivers-abstractions project.
- If a type is only template JSON document shape, keep it in `CanDoItAll.Processes.Templates`.
- If a type is only projection text shape, keep it in application/projections.
- If a type performs infrastructure I/O, keep implementation in `CanDoItAll.Modules.Processes` or the relevant module and expose only a narrow contract upward.

## Build/Test Proof Required

- Before/after CodeAnalytics snapshot after implementation when project references change.
- `dotnet build CanDoItAll.slnx` or narrower solution/project build if full build is blocked by unrelated issues.
- Project-reference diff or CodeAnalytics dependency proof for any changed `.csproj`.
- Architecture gate must explicitly record cycle result and forbidden-reference review.
