# C# current-state inventory

Observation details: [source baseline](../inputs/02-observed-source-baseline.md). Line counts are diagnostic snapshots, not quality thresholds.

| Area | Observed responsibility / dependency | Risk |
|---|---|---|
| AgentsHomePage (~935 code-behind lines) | Eight injections, route compatibility, overview/usage/HR/avatar counts, direct EF bound-resource count, selected chat context, host tabs/dialogs | A generic aggregate query or more host handlers can change lazy behavior or grow page coupling |
| AgentCatalogPanel (~705 lines) | Six injections, initial snapshots/repair, selection/context, requested-open tracking, agent/team dialogs and chat | Selection, presentation and effects have competing owners |
| AgentDetailsDialog (~1584 code-behind lines) | Mutable editor and references, seven injections, ten sections, save/reset/delete/capability workflows | Draft lifetime, concurrency, commit/refresh and UI presentation are intertwined |
| Descendants | External roots, storage dialogs, provider refresh, avatar, capability setup and memory profiles/drivers | Parent-only injection inventory misses real I/O and runtime reachability |
| Public contracts | AgentEditorModel has mutable nested data and ExpectedUpdatedAtUtc; Projects/Security list types live with implementations | Record wrappers do not imply immutability or lightweight references |
| Module project | Runtime/persistence/feature modules and sibling-source substitution | Same-project refactor does not shrink the watch graph |
| Tests | 46 primary cases, 10 route cases, omitted two history-host cases and one adjacent workflow navigation case | Counts and fake parent/controller tests alone miss behavioral gaps |

Use [source scope](../inventories/00-source-scope.md), [service inventory](../inventories/01-service-dependency-inventory.md), and [subtree/type inventory](../inventories/04-rendered-subtree-and-contract-closure.md) as the working audit. Refresh before execution; a three-project CodeAnalytics snapshot cannot certify all transitive references.
