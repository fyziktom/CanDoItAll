# C# dependency direction

## Current graph evidence

- Snapshot: `snap-20260808192349-53bec4ab`.
- Projects: 103.
- Project-reference edges: 608.
- Project-level cycles: 0.
- `CanDoItAll.Infrastructure` directly references only `CanDoItAll.SharedKernel`.
- `CanDoItAll.Modules.Security` references Infrastructure, Security.Abstractions, and SharedKernel.
- `CanDoItAll.AgentFramework.Core` references capability/model/runtime/workflow abstractions plus Git, Memory SourceGateway abstractions, and SharedKernel.
- `CanDoItAll.AgentFramework.Maf` already references Infrastructure; that coupling may be reduced but must not be expanded to make MAF own host policy.
- Composition references module implementations and is the selection boundary.

## Required direction

```text
SharedKernel (pure path values/codecs)
  <- Infrastructure and logical-path consumers

Infrastructure.Abstractions (physical binding port only)
  <- Infrastructure implementation
  <- MAF Models/Core and Processes Application consumers

Security.Abstractions
  <- Modules.Security
  <- runtime consumers

MAF execution contracts
  <- Workbench orchestration
  <- Manager/Plugin adapters

Processes contracts/runtime
  <- Workbench and composition adapters

Components/FileTools contracts
  <- CanDoItAll consumer adapters
```

Arrows mean “may be referenced by.” Composition is allowed to see implementations; lower-level libraries are not allowed to reference Composition, Web, Workbench pages, or Manager.

## Change rules

1. A01 may add inward references to SharedKernel for pure path values. The architecture
   gate also approves the new dependency-free `CanDoItAll.Infrastructure.Abstractions`
   port because no existing neutral contract can carry host-bound alias resolution
   without either making SharedKernel a physical authority or coupling MAF to the
   Infrastructure implementation.
2. A02-A04 may add leaf adapters behind an existing narrow contract; they may not add a generic platform aggregation service.
3. A05 owns composition changes and must keep provider selection outside domain/core projects.
4. Temporary Components/FileTools project references are introduced only after core C4 re-anchoring in B00, with explicit paths and no package fallback hidden behind `Exists` conditions.
5. Every new project edge requires an after-change CodeAnalytics snapshot and zero project cycles.

## A01 permitted new edges

- `CanDoItAll.Infrastructure -> CanDoItAll.Infrastructure.Abstractions`.
- `CanDoItAll.AgentFramework.Models -> CanDoItAll.Infrastructure.Abstractions`.
- `CanDoItAll.AgentFramework.Core -> CanDoItAll.Infrastructure.Abstractions`.
- `CanDoItAll.Processes.Application -> CanDoItAll.Infrastructure.Abstractions`.
- composition/hosting projects may reference `CanDoItAll.Infrastructure` to select the
  physical implementation; Models, Core, and Processes Application may not.

No project may add `Models/Core/Processes.Application -> CanDoItAll.Infrastructure`.
The after-change snapshot and cycle result replace the baseline evidence above when C1a
is finally reviewed.

## A01 after-change evidence

- CodeAnalytics snapshot: `snap-20260809031028-a2e9718e`.
- Scope: the eight changed boundary projects; no blocking diagnostics and no
  project-level cycle.
- Deterministic full graph: `104` projects, `619` direct references, `104` projects
  processed topologically, and `0` project cycles.
- `CanDoItAll.Infrastructure.Abstractions` has `0` direct project references.
- `CanDoItAll.AgentFramework.Core` and `CanDoItAll.AgentFramework.Models` reference
  `CanDoItAll.Infrastructure.Abstractions`; neither references the Infrastructure
  implementation.
- Evidence: `artifacts/unix-portability/A01/A01-project-reference-graph-final.json`.

The scoped analyzer also reports an existing module cycle between the Infrastructure
ControlPlane and Persistence namespaces and an existing type cycle inside
AgentFramework.Core. These are not project-reference cycles and do not change the
approved A01 dependency direction. They remain architecture review inputs for the
owning later work.
