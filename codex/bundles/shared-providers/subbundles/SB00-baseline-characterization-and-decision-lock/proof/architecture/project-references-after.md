# SB00 project references after characterization

## Evidence source

CodeAnalytics force-refresh snapshot `snap-20260824195319-b6470538` reports the same 11 product
projects and 23 direct `ProjectReference` edges as the before snapshot. SB00 added two
SDK-included test source files and no project file, package, solution, or production source change.

## Direct references after SB00

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

## Decision

- Added references: none.
- Removed references: none.
- Product project cycles: zero.
- Inner provider/runtime references to Workspace, Web, UI, EF, or SharedProviders.Http: none.
- Preferred downstream graph: unchanged from `project-references-before.md`.

This artifact and the after snapshot jointly close the no-change reference proof. Any product
project or reference change reopens the owning architecture checkpoint.
