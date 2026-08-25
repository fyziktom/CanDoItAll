# SB00 CodeAnalytics baseline

## Capture

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824190346-9451b9e9` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T19:03:46.9539916+00:00` |
| Cache | Fresh snapshot (`fromCache: false`) |
| Scoped projects | 11 |
| Scoped source documents | 665 |
| Modules | 31 |
| Dependency edges | 4,566 |
| Direct product `ProjectReference` edges | 23 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 type-level |

The inventory loaded every requested project and returned the complete 23-edge direct product
graph. The project-level negative result is therefore usable for this SB00 architecture gate.
Snapshot diagnostics include duplicate embedded/generated type names and expected scoped EF
collector notices for types outside the selected projects; none reports a failed load of one of
the 11 scoped projects. Those diagnostics do not justify broader symbol-level negative claims.

## Scoped projects

1. `CanDoItAll.AgentFramework.Maf`
2. `CanDoItAll.AgentFramework.Models`
3. `CanDoItAll.AgentFramework.Providers`
4. `CanDoItAll.AgentFramework.Usage`
5. `CanDoItAll.Infrastructure`
6. `CanDoItAll.Infrastructure.Abstractions`
7. `CanDoItAll.Migrations.PostgreSql`
8. `CanDoItAll.Modules.AgentFramework`
9. `CanDoItAll.Modules.Security`
10. `CanDoItAll.Modules.Workspace`
11. `CanDoItAll.Web`

## Cycle classification

CodeAnalytics reported three cycles. None is a project-reference cycle, so the result must be
recorded as “no project-level cycle,” not the broader and incorrect “no cycles.”

| Level | Nodes | Classification | SB00 decision |
| --- | --- | --- | --- |
| Module | `CanDoItAll.Infrastructure.Persistence` ↔ `CanDoItAll.Infrastructure.ControlPlane` | Pre-existing bidirectional namespace/module dependency inside `CanDoItAll.Infrastructure`; edge weights are 35 and 21. It does not cross a `.csproj` boundary. | Baseline-only. Shared-provider work does not touch or depend on resolving this cycle. Reopen if a shared-provider type is placed in either module or adds an edge between them. |
| Module | `CanDoItAll.Modules.AgentFramework.Hosting` ↔ `CanDoItAll.Modules.AgentFramework` | Pre-existing bidirectional namespace/module dependency inside `CanDoItAll.Modules.AgentFramework`; both directions have weight 6. It does not cross a `.csproj` boundary. | Baseline-only. Do not use this existing coupling as permission to put shared-provider protocol or HTTP types in the module. Reopen if SB06 changes either namespace or increases the cycle. |
| Type | `ImageGenerationAgentRuntimeToolProvider` ↔ nested `ImageGenerationAgentRuntimeToolProvider.ImageGenerationToolBuilder` | Pre-existing outer/nested-type collaboration in `AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`; both directions have weight 3 and remain in one file and project. | Not a project architecture cycle. Do not copy this nested-owner pattern into the shared-provider implementation. Reopen if image-generation relay work changes either type or increases the cycle. |

## Project-cycle gate

The 23 direct project-reference edges form a directed acyclic graph. In particular:

- `CanDoItAll.AgentFramework.Models` and `CanDoItAll.AgentFramework.Providers` have no reference
  to Workspace, Web, EF modules, or a provider HTTP implementation;
- `CanDoItAll.AgentFramework.Maf` points to Models, Providers, and Infrastructure only;
- Workspace points inward to Models, Infrastructure, and Security;
- `CanDoItAll.Modules.AgentFramework` is the existing outer adapter that may point to Workspace
  and the inner AgentFramework projects;
- Web remains the outer composition/API layer.

This confirms that the preferred shared-provider project shape can be added without reversing an
existing inner dependency.

## No-workaround and partial-class decision

- Do not solve a future cycle with a `Common` project, `object`, `dynamic`, reflection, a static
  service locator, duplicate protocol DTOs, or a Workspace reference to the HTTP implementation.
- Extract only the smallest stable port into `CanDoItAll.SharedProviders.Abstractions`; bind its
  HTTP implementation in Web/Composition.
- SB00 adds no product partial classes. Future shared-provider entities, services, protocol
  records, and adapters must use focused files and types; they must not be appended to
  `WorkspaceModels.cs` or hidden in a new partial-class cluster.
- The existing outer/nested image-tool type cycle is evidence to avoid that shape, not a precedent
  for shared-provider composition.

## Reopen triggers

Rebuild the scoped snapshot and repeat the dependency/cycle query when any of these occurs:

- any scoped `.csproj` changes;
- either preferred SharedProviders project is added, removed, renamed, or collapsed;
- an inner AgentFramework project gains a Workspace, Web, EF, or SharedProviders.Http reference;
- Workspace gains a concrete SharedProviders.Http reference;
- Abstractions needs ASP.NET, EF, Razor, a provider SDK, or an outer module;
- Http needs Workspace entities, Web endpoint types, EF, or UI types;
- any project-level cycle appears;
- one of the three classified intra-project cycles is touched or its edge weight/scope increases;
- the source commit changes before SB01 begins.

