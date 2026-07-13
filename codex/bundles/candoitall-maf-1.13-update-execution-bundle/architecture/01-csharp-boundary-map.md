# C# Boundary Map

## Target Projects

| Project | Role In Update | Boundary Rule |
|---|---|---|
| `CanDoItAll.AgentFramework.Maf` | Main Microsoft Agent Framework adapter. | May reference MAF packages and provider/tool/workflow abstractions. Must not absorb process-domain behavior. |
| `CanDoItAll.AgentFramework.Workflows.MafAdapter` | Adapter between CanDoItAll workflow abstractions and MAF workflows. | May adapt workflow package APIs. Must not adopt new durable workflow features in phase 1. |
| `CanDoItAll.AgentFramework.Hosting` | A2A hosting preview package decision surface. | May update preview package only if NuGet/restore/build evidence justifies it. |
| `CanDoItAll.AgentFramework.Tooling` | `Microsoft.Extensions.AI.Abstractions` floor warning surface. | Update only if restore/build produces a real dependency-floor problem. |
| `CanDoItAll.Processes.*` | Product process runtime. | Must not reference concrete MAF packages. |
| `CanDoItAll.Modules.Processes` | Process module UI/API/module wiring. | Must not register direct process runtime tools in this update. |

## Target Top-Level Types

No new top-level types are required by preparation. If compile breaks require a new compatibility type, it must be:

- in the same adapter project as the package API it isolates;
- named by responsibility, not by package version alone;
- directly unit-testable without constructing `MafAgentRuntime`;
- referenced by a pattern selection record update;
- covered by source assertions and the architecture drift checkpoint.

## Contracts Vs Implementations

- Contracts remain in Core/Models/Abstractions projects and must remain SDK-free.
- MAF package-specific SDK types stay inside MAF adapter projects.
- Provider SDK details stay behind provider runtime gateway abstractions.
- Workflow package details stay inside `Workflows.MafAdapter`.
- Process runtime contracts remain in `Processes.*`, not in MAF adapter projects.

## Composition Root Responsibilities

- Composition may reference implementation projects to wire services.
- Runtime/core behavior must not use `IServiceProvider` as a shortcut for new compatibility behavior.
- New registration changes require a composition smoke test and source assertions.

## Old Class Responsibilities To Remove Or Leave

Leave in place for this update:

- `MafAgentRuntime` orchestration facade responsibilities.
- `RuntimeCapabilityComposer` capability attachment responsibilities.
- Existing finalizer, approval, context, provider, and session responsibilities.

Remove only if package compatibility forces it:

- Direct calls to removed or renamed MAF APIs, replaced by minimal adapter code.
- Test expectations tied to obsolete MAF type names when behavior remains equivalent.

## Temporary Bridges And Removal Plan

Temporary compatibility bridges are allowed only when:

- the package API changed;
- the bridge is small and typed;
- it does not hide errors with fallback behavior;
- it is tested directly;
- `SB04` records whether it is temporary or final.
