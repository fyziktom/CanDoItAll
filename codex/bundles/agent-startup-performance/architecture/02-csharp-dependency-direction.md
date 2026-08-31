# Dependency Direction

Scoped snapshot project edges: Modules.AgentFramework→Core/Models/Persistence/Infrastructure; Persistence→Core/Models/Infrastructure; Core→Models. No project-level cycle in this scope. The snapshot excludes some referenced projects, so real csproj inspection supplements it: Infrastructure→Infrastructure.Abstractions/SharedKernel; Persistence also references provider-history/workflow/capability contracts/runtime and Infrastructure.Abstractions. These existing references remain unchanged.

Target graph = current graph. No new contract project, package or DI registration. No Foundation→AgentFramework/Module/UI reference; no Core/Models→concrete Infrastructure/Module provider implementation; no new cycle. Existing module cycles Infrastructure.Persistence↔ControlPlane and Modules.AgentFramework.Hosting↔module root, and two type cycles, are baseline findings only.

Before/after evidence: scoped CodeAnalytics dependency/cycle comparison plus actual csproj diff; compile affected owners/host through focused test builds. If a public contract or project reference must change, stop/reopen bundle and use dependency-graph audit/impacted-tests analysis; do not silently widen the plan. Full-solution cleanup is not an acceptance criterion.

ProviderManagement is outside the five-project snapshot; direct csproj/source inspection supplements it: it references Core/Models/Providers, Infrastructure/SharedKernel, Security, SharedProviders.Abstractions and ProviderHistory contracts/persistence. No new Module/UI feature dependency is permitted. Rebuild an additional scoped ProviderManagement snapshot at execution for before/after proof rather than treating the current snapshot as complete coverage.
