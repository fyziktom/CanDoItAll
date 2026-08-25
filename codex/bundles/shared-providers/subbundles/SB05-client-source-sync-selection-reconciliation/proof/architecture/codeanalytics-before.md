# SB05 CodeAnalytics before implementation

State: `CAPTURED`

SB05 begins from the force-refreshed SB04 closure snapshot
`snap-20260825051057-300644c7`, captured before any SB05 product source edit.

| Fact | Value |
| --- | --- |
| Scoped product projects | 14 |
| Scoped source documents | 752 |
| Modules | 35 |
| Dependency facts | 5,158 |
| Direct product `ProjectReference` edges | 34 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 nested-type |
| Error findings | 0 |

The scoped projects are AgentFramework Maf/Models/Providers/Usage, Composition,
Infrastructure/Abstractions, PostgreSQL migrations, Modules AgentFramework/Security/Workspace,
SharedProviders Abstractions/Http, and Web. The allowed SB05 design requires no new product edge:
Workspace consumes the neutral catalog-client port already in Abstractions, Http implements it,
and outer Composition already references Http and Workspace. Any Workspace-to-Http,
Http-to-Workspace/Security/Web/EF, or Abstractions-to-product edge is a stop condition.
