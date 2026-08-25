# SB01 CodeAnalytics baseline

## Capture

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824204913-6a7763ae` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T20:49:13.2317269+00:00` |
| Force refresh | `true` |
| Scoped product projects | 11 |
| Scoped source documents | 665 |
| Modules | 31 |
| Dependency edges | 4,566 |
| Direct product `ProjectReference` edges | 23 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 type-level |
| Blocking analyzer errors | No |

The force-refresh used the same eleven-project production scope as the final SB00 snapshot
`snap-20260824195319-b6470538`. Counts and cycles are identical. The captured inventory does not
contain `CanDoItAll.SharedProviders.Abstractions`, so this is the pre-SB01 graph and remains a
valid comparison point even though implementation began after the analyzer loaded the scope.

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

| Level | Nodes | SB01 decision |
| --- | --- | --- |
| Module | `CanDoItAll.Infrastructure.Persistence` <-> `CanDoItAll.Infrastructure.ControlPlane` | Pre-existing intra-project cycle. SB01 must not place shared-provider contracts in either module or change this cycle. |
| Module | `CanDoItAll.Modules.AgentFramework.Hosting` <-> `CanDoItAll.Modules.AgentFramework` | Pre-existing intra-project cycle. SB01 must not add shared-provider protocol ownership to AgentFramework. |
| Type | `ImageGenerationAgentRuntimeToolProvider` <-> nested `ImageGenerationToolBuilder` | Pre-existing nested-type collaboration. It is not a precedent for shared-provider nesting or partial-class growth. |

The snapshot diagnostics are duplicate embedded/generated type-name warnings already classified
by SB00. All requested projects and 665 documents loaded, and the dashboard, inventory, and
dependency queries returned no query warnings. The negative project-cycle result is therefore
usable for the SB01 architecture gate.

## Required after comparison

The closure snapshot must add `CanDoItAll.SharedProviders.Abstractions` to this exact scope and
force refresh after all source and project changes. Expected production graph changes for SB01
are one project with zero inward references and one new product edge from Web to Abstractions.
Any other new product edge, any project cycle, a new analyzer load failure, or a change to a
classified cycle blocks SB01.
