# SB01 project references after implementation

## Evidence source

CodeAnalytics snapshot `snap-20260824213007-c65710b4` reports 12 scoped product projects and 24
direct product `ProjectReference` edges. Test-only references are recorded separately below.

## Direct product references

| From | To | SB01 delta |
| --- | --- | --- |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Models` | unchanged |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.AgentFramework.Providers` | unchanged |
| `CanDoItAll.AgentFramework.Maf` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.AgentFramework.Models` | `CanDoItAll.Infrastructure.Abstractions` | unchanged |
| `CanDoItAll.AgentFramework.Providers` | `CanDoItAll.AgentFramework.Models` | unchanged |
| `CanDoItAll.AgentFramework.Usage` | `CanDoItAll.AgentFramework.Models` | unchanged |
| `CanDoItAll.Infrastructure` | `CanDoItAll.Infrastructure.Abstractions` | unchanged |
| `CanDoItAll.Migrations.PostgreSql` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Maf` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Models` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.AgentFramework.Usage` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Security` | unchanged |
| `CanDoItAll.Modules.AgentFramework` | `CanDoItAll.Modules.Workspace` | unchanged |
| `CanDoItAll.Modules.Security` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.AgentFramework.Models` | unchanged |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.Modules.Workspace` | `CanDoItAll.Modules.Security` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.Infrastructure` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.Migrations.PostgreSql` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.Modules.AgentFramework` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Security` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.Modules.Workspace` | unchanged |
| `CanDoItAll.Web` | `CanDoItAll.SharedProviders.Abstractions` | added and authorized |

`CanDoItAll.SharedProviders.Abstractions` has no outgoing reference. Direct project inspection
also confirms its project file contains no `PackageReference`, `ProjectReference`, or
`FrameworkReference`.

## Test-only references

- `CanDoItAll.Tests.Unit -> CanDoItAll.SharedProviders.Abstractions`
- `CanDoItAll.Tests.Integration -> CanDoItAll.SharedProviders.Abstractions`

The root solution includes Abstractions. No Workspace reference was added early; SB02 owns that
edge. No Http implementation project exists yet; SB04 owns it.
