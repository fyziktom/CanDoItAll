# SB01 project references before implementation

## Evidence source

CodeAnalytics snapshot `snap-20260824204913-6a7763ae` returned 11 scoped product projects and
all 23 direct product `ProjectReference` edges below. Supporting test references are deliberately
excluded from the production dependency graph.

## Current direct product references

| From | To |
| --- | --- |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Models` |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Providers` |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.AgentFramework.Models` | `CanDoItAll.Infrastructure.Abstractions` |
| `CanDoItAll.AgentFramework.Providers` | `CanDoItAll.AgentFramework.Models` |
| `CanDoItAll.AgentFramework.Usage` | `CanDoItAll.AgentFramework.Models` |
| `CanDoItAll.Infrastructure` | `CanDoItAll.Infrastructure.Abstractions` |
| `CanDoItAll.Migrations.PostgreSql` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Maf` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Models` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Usage` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Security` |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Workspace` |
| `CanDoItAll.Modules.Security` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.AgentFramework.Models` |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Modules.Security` |
| `CanDoItAll.Web` | `CanDoItAll.Infrastructure` |
| `CanDoItAll.Web` | `CanDoItAll.Migrations.PostgreSql` |
| `CanDoItAll.Web` | `CanDoItAll.Modules.AgentFramework` |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Security` |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Workspace` |

## SB01-authorized changes

| Project | Change | Constraint |
| --- | --- | --- |
| `CanDoItAll.SharedProviders.Abstractions` | Add under `src/Integration` and the root solution. | `net10.0`; zero `ProjectReference` and package references unless a separately reviewed canonical inward contract proves necessary. |
| `CanDoItAll.Web` | Add one reference to Abstractions. | Web owns header binding and scoped registration only; no shared catalog or relay implementation in SB01. |
| `CanDoItAll.Tests.Unit` | Add a supporting reference to Abstractions. | Used only by the two focused protocol/routing lanes. |
| `CanDoItAll.Tests.Integration` | Add a supporting reference to Abstractions, while its existing Web reference supplies the real host. | Used only by the focused access-context lane. |

No Workspace reference is needed until SB02. `CanDoItAll.SharedProviders.Http` is not created in
SB01. AgentFramework, Infrastructure, Security, Migrations, Composition, UI, and inner provider
projects gain no new shared-provider reference in this subbundle.

## Expected after graph

For the same production scope plus the new project, the expected inventory is 12 projects and 24
direct product edges. Abstractions has no outgoing edge and Web has the only new incoming product
edge:

```text
CanDoItAll.SharedProviders.Abstractions
                    ^
                    |
             CanDoItAll.Web
```

The following are closure blockers:

- Abstractions references ASP.NET Core, EF, Workspace, Web, UI, AgentFramework, or a provider SDK;
- Workspace references an SB01 implementation or Web type;
- an inner AgentFramework project references Abstractions merely to reuse an outer protocol;
- a `Common` project, reflection, `dynamic`, duplicated DTO, partial class, or service locator is
  introduced to bypass direction;
- the after snapshot reports any project-level cycle or a product edge not listed above.
