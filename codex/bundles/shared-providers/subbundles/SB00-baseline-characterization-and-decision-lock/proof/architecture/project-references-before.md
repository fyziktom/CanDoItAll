# SB00 project references before implementation

## Evidence source

This table is the direct product-project inventory returned by CodeAnalytics snapshot
`snap-20260824190346-9451b9e9` for `CanDoItAll.slnx`. It contains 11 scoped projects and all 23
direct `ProjectReference` edges reported for that scope. Supporting test/benchmark references are
not mixed into this production graph.

## Current direct references

| From | To | Current role |
| --- | --- | --- |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Models` | Runtime consumes provider and agent models. |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Providers` | Runtime consumes provider driver contracts/implementations. |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.Infrastructure` | Existing runtime infrastructure integration. |
| `CanDoItAll.AgentFramework.Models` | `CanDoItAll.Infrastructure.Abstractions` | Inner models consume lower infrastructure contracts. |
| `CanDoItAll.AgentFramework.Providers` | `CanDoItAll.AgentFramework.Models` | Capability drivers consume provider models. |
| `CanDoItAll.AgentFramework.Usage` | `CanDoItAll.AgentFramework.Models` | Usage projection consumes provider/agent identities. |
| `CanDoItAll.Infrastructure` | `CanDoItAll.Infrastructure.Abstractions` | Infrastructure implements lower contracts. |
| `CanDoItAll.Migrations.PostgreSql` | `CanDoItAll.Infrastructure` | Migration assembly consumes the application DbContext/model registry. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Maf` | Outer module composes the MAF runtime. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Models` | Outer module maps Workspace state to runtime models. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Usage` | Outer module projects and queries usage. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Infrastructure` | Module persistence/composition support. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Security` | Runtime secret-resolution integration. |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Workspace` | Existing Workspace-to-AgentFramework projection boundary. |
| `CanDoItAll.Modules.Security` | `CanDoItAll.Infrastructure` | Secret persistence/runtime infrastructure. |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.AgentFramework.Models` | Canonical provider rows map to inner runtime models. |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Infrastructure` | Workspace persistence and application infrastructure. |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Modules.Security` | Workspace stores secret references and uses security services. |
| `CanDoItAll.Web` | `CanDoItAll.Infrastructure` | Host/API infrastructure. |
| `CanDoItAll.Web` | `CanDoItAll.Migrations.PostgreSql` | Host selects the migration assembly. |
| `CanDoItAll.Web` | `CanDoItAll.Modules.AgentFramework` | Host/API composes AgentFramework application behavior. |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Security` | Host/API composes authentication and secret services. |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Workspace` | Host/API composes Workspace application behavior. |

## Before-graph decision

- There is no direct project-reference cycle.
- The inner provider/runtime projects do not reference Workspace or Web.
- Workspace already references AgentFramework.Models, but Models does not reference Workspace.
- AgentFramework module is the existing outer projection owner and may reference both Workspace
  and inner AgentFramework projects.
- Web is the composition root and can bind outward implementations to inward contracts.

## Preferred after graph

The current 23 edges remain unless implementation evidence justifies a separately reviewed
removal. Add the following projects and only these production edges:

| From | To | Planned change | Reason |
| --- | --- | --- | --- |
| `CanDoItAll.SharedProviders.Abstractions` | none preferred; `CanDoItAll.SharedKernel` only if a canonical value/result contract is necessary | Add project | SDK-free, EF-free, ASP.NET-free public protocol and transport ports. |
| `CanDoItAll.SharedProviders.Http` | `CanDoItAll.SharedProviders.Abstractions` | Add project/reference | HTTP clients, bounded OpenAI-compatible mapping, streaming, and upstream adapters point inward to contracts. |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.SharedProviders.Abstractions` | Add reference | Workspace owns publication/source/import persistence and consumes neutral catalog/inference ports. |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | Add reference | Endpoints expose explicit public contracts and register policies. |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Http` | Add reference | Outermost host supplies concrete HTTP implementations and adapter registration. |

```text
SharedProviders.Abstractions
    ^                    ^
    |                    |
Workspace          SharedProviders.Http
    ^                    ^
    |                    |
AgentFramework       Web/Composition
    ^                    |
    |____________________|
             |
            Web
```

No new project reference is planned from `CanDoItAll.Modules.AgentFramework`: it already points
to Workspace and the inner runtime projects and can perform the shared-connector effective-profile
projection there. No new reference is planned from Migrations because module EF configurations are
applied through the existing model-registration mechanism.

## Forbidden after edges

| Forbidden edge | Required response |
| --- | --- |
| AgentFramework.Models/Providers/Maf → Workspace, Web, EF, or SharedProviders.Http | Stop and move the stable contract inward. |
| SharedProviders.Abstractions → Workspace, Web, UI, EF, or provider SDK | Stop; the abstraction is in the wrong layer. |
| SharedProviders.Http → Workspace entity/application types, Web endpoint types, Razor, or EF | Stop; depend on Abstractions and compose outward. |
| Workspace → SharedProviders.Http | Stop; inject an Abstractions port from Web/Composition. |
| Razor component → HttpClient or DbContext | Stop; route through the owning application service. |
| Web endpoint → DbContext or upstream provider SDK | Stop; delegate to Workspace/application services. |

## No-workaround and partial-class decision

No graph workaround is authorized. Do not introduce `Common`, dynamic/reflection bridges, service
location, duplicated DTOs, or reversed references to make a build pass. SB00 requires no product
partial. New implementation types belong in cohesive files and focused classes; do not extend the
large Workspace model/service partial cluster with shared-provider responsibilities.

## Reopen triggers

Regenerate both the before/after reference table and CodeAnalytics cycle evidence when:

- a production `.csproj` or solution inventory changes;
- the two-project SharedProviders shape is collapsed or expanded;
- any planned edge differs from the table above;
- a new package forces ASP.NET, EF, UI, or provider-SDK dependencies into Abstractions;
- Workspace appears to require the concrete Http project;
- AgentFramework inner projects appear to require Workspace/import/publication types;
- a new project-level cycle is reported;
- implementation moves composition outside Web/Composition;
- SB01 or later evidence invalidates the canonical Workspace-owner/runtime-projection decision.
