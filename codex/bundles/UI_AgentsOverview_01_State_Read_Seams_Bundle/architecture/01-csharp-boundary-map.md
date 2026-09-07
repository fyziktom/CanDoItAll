# Target ownership

Names below are cohesive candidates, not mandatory spelling or type-count goals.

| Owner | Owns | Excludes |
|---|---|---|
| Page | Route/query/workspace; header/global navigation; HR/defaults; selection/access bridge; history demand; one session; thin typed intent dispatch/owned overlays. | Duplicate accepted aggregates/counts, read races, chart/list mapping, generic controller. |
| Page header state | HR/readiness, avatars, bound-resource value and its generation/error. Counts derive from session's accepted Overview.Totals. | A second Overview totals store or usage scope. |
| Per-page Overview session | Accepted overview/usage, independent load/error lanes, read identity, CTS/generation, refresh/retry. Construct/dispose with page. | Route parsing, dialogs, navigation, commands, chart types. |
| Existing workspace query | Header, overview, scope usage reads via existing backend ports. | UI state, notifications, charts, command effects. |
| Pure mapper/presentation | Metric/team/list/chart values and loading/partial/enabled projection, independently owned collections. | Read eligibility, route acknowledgement, application recovery/I/O. |
| Service-free Surface | Real existing stats/charts/lists/teams; snapshot plus typed intents. | Feature DI/query, persistence/navigation/dialogs, mutable semantic selection. |
| Three usage dialogs | Captured accepted selection, owned cancellable lazy query and existing rendering. | Mutating Overview/workspace scope. |

The page remains the existing effect host. A forwarding host/controller is unnecessary merely to dispatch scope/dialog/team intents. It must lose aggregate/read/mapping responsibilities. If nontrivial effects cannot remain a small dispatcher, reopen this record before inventing a service.

Workspace desired scope -> session starts/cancels scoped generation -> result must match generation AND scope -> accepted snapshot -> pure presentation -> Surface. Scope intent updates workspace once; same-route echo must not duplicate read. Accepted scope is an observation stamp, not a competing selection owner.

Replace current ReadShell use with cohesive ReadHeaderAsync, ReadOverviewAsync and ReadUsageAsync operations on the existing interface. Current sole product caller is page; update owning fakes/integration wiring and remove obsolete shell envelope/facade once callers migrate. If re-entry finds another caller, inventory it and allow only a justified temporary adapter with removal before O02 closure.

Explicit refresh independently starts header/overview/usage under current demand. Scope change starts only usage. History entry starts no aggregates and fences in-flight ones; cached accepted summary survives. Retry targets failed lane, never warmup/HR launch. The page session persists across conditional renderers for header coherence.

O01 keeps new source in Module. O03 moves only real pure rendering/contracts/mapper/options and list/helper/CSS closure to existing UI. Session, query and effects stay Module; header/defaults/HR markup stays page. Normal Razor code-behind is allowed; another page partial is not a boundary.
