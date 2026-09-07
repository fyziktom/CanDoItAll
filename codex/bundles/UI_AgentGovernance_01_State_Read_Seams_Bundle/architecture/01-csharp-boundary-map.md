# Intended ownership (not implemented)

| Owner | Target responsibility | Exclusions |
|---|---|---|
| AgentsHomePage, Module | Existing authoritative workspace/route agent selection and context bridge | No second run-list store, no new route key |
| AgentGovernancePanel, Module | Integration host; apply requested target, construct/dispose one session, dispatch typed intent, publish accepted selection/access callbacks | No duplicated list/detail generation or mapper logic |
| AgentGovernanceSession, Module | Per-instance desired/accepted target, catalog/list/detail requests, manual-selection revision, accepted snapshots, safe lane errors, cancellation/disposal | Not scoped DI, no navigation/dialog/notification service bag |
| IAgentGovernanceReads / production adapter, Module | One cohesive catalog/list/detail read boundary over the registered workspace; tokens and safe request/result validation | No query implementation, EF/store/runtime/approval behavior duplication |
| Governance presentation records/mapper, initially Module | Immutable display state; bounded errors; copied collections and pure badge/summary mapping | No full session/persistence payload serialization |
| AgentGovernanceSurface, initially Module | Real controlled list/detail subtree, typed agent/run/retry intents, display busy/error/stale states | No injected services or independent semantic selection |
| Existing UI, only G03 | Move the proven pure surface/records/mapper and real timeline/metrics children | No session/read adapter/page/Core/broad Components |
| Existing sandbox, only G03 | Safe controlled specimen and intent log using those exact components | No database, workspace, execution or provider graph |

The read adapter is justified by three related reads spanning catalog and execution-history contracts plus direct deterministic session tests. Rejected alternatives: retain a broad mutable workspace in a renderer; interface per method; generic Everything controller. If G00 demonstrates that existing typed history reader plus one catalog delegate is materially simpler with the same public test seam, amend the record before implementation instead of adding a redundant adapter. Never maintain both alternatives.

The session owns operation state; the page owns semantic workspace selection. Host callback acknowledgements are request bookkeeping, not a second workspace store. Preserve public panel parameters/callbacks unless real-page RED proves a narrow change necessary. A failed requested identity must not be converted into All agents by a null callback/parent echo; prove the page sequence before choosing whether to suppress that null publication or retain explicit unresolved target state in the host.

G01 introduces actual top-level owners, not extra partial files. G02 removes old markup helpers/read state from the panel as ownership moves. G03 moves rather than copies. No permanent forwarding facade, new UI project, new contract project, broad service bag or extraction wrapper is planned.
