# PC1 architecture and consequence map

## Preferred cut

```text
ProcessesPage / ProjectProcessesPage / LiveProcessesPage (native module)
  -> per-view native presentation session and effect adapters
     -> CanDoItAll.Processes.UI (same actual renderer in both hosts)
        -> existing suitable Process projections/value contracts
        -> a small cohesive UI view/intent contract where needed
        -> existing shared rendering libraries

CanDoItAll.Processes.UiSandbox
  -> CanDoItAll.Processes.UI
  -> deterministic sessions/scenarios and narrow browser/file/chat adapters
```

Suggested paths are `src/UI/CanDoItAll.Processes.UI`, `src/Sandboxes/CanDoItAll.Processes.UiSandbox` and `tests/Components/CanDoItAll.Processes.UI.Tests`. Reuse an equivalent current project if it exists by execution time. A separate Presentation or UI-contract project is optional and justified only by a real dependency/testing boundary. Do not require one class per command or one project per box.

## Reuse before moving contracts

Read the public values and evaluated transitive closure of existing `src/Processes/CanDoItAll.Processes.{Contracts,Abstractions,Core,Projections}`. Their inspected direct references contain no Runtime/Persistence edge [P11/P19–P21]. A pure Core transitive reference is not forbidden solely by its name; the guard should reject actual heavy implementation/authority leakage, not every domain assembly. Existing projection reuse is preferred when it remains bounded, safe and useful.

Keep Application/Runtime/Persistence/native module services outside the render leaf. If a specific view type has an unsuitable public dependency, extract only its stable closure or map a meaningful display value. Do not copy all projection DTOs, move the complete Projections assembly, make domain Contracts depend on Blazor, or change native serialized payloads to satisfy an arbitrary clean-looking graph.

Preserve routes and query strings: `/processes`, `/projects/{ProjectId:guid}/processes`, `/processes/live`, `/projects/{ProjectId:guid}/processes/live`; existing `processId`, `runId`, `launchPlanId`, `q`, `definitionKey`, `processStarted` and `mockScenario` have host-specific meanings [P12–P14]. Preserve current unavailable-target behavior, back/forward and launch navigation. Do not silently fall back from a requested missing run to another run.

## Owners and lifetime

| Fact/effect | Owner retained or introduced at the native seam |
| --- | --- |
| Desired scope, route selection, database/project admission | Native route/view owner using current native admission |
| Accepted projection/read lanes | Per-view session with scope/opening/generation and bounded native queries |
| Definition/role/step/raw draft and validation | Stable editor lifetime above real tab unmounts; explicit discard/replace transitions |
| Definition/canvas/template commands | Existing native projection/application owner through a typed adapter |
| Launch caller intent, prepared authority, accepted run, continuation/link delivery | Existing launch owners and preparation store; view only presents state/intents |
| Explicit run cancellation/operator command | Existing operator/native client owner; preserve receipt and no-replay semantics |
| File roots, authorization, content lifetime, desktop/download authority | Existing file coordinator/scope/session/action owners |
| Chat orchestration, streaming, voice ownership and safe context publication | Existing agent/conversation/voice owners with narrow presentation adapters |
| Focus, local disclosure, canvas window geometry, presentation JS | Renderer where truly presentation-local and correctly disposed |

Sessions carrying mutable view state must be **per component/view instance**, not an application singleton or accidentally shared Blazor circuit-scoped draft. Use existing factories/lifetimes or an explicit owned instance. Two simultaneously mounted workspaces/dialogs must not share a draft, read generation, busy gate or cleanup target. Do not replace this with a global lock blocking unrelated users.

## Concrete seam behavior

Separate selected definition/run from the current editor/dialog opening. Retain raw invalid values and validation across Definition/Roles/Steps/Runs/Graphs/Analytics/Exchange/Manager chat transitions where the same editor remains active. For nested row edits use semantic row identity, not the current visual index. Capture submitted values and native version before write; reconcile returned state against that submission while preserving later edits.

Create independent read lanes only where the actual native operations are independent. Preserve current lazy loading, event page size and requested history windows; do not multiply backend reads merely by mounting more child components. The live polling owner remains one cancellable native subscription/loop per appropriate view, not a timer in every leaf. The reviewed live interval is ten seconds [P03]; preserve current configured behavior rather than hard-code a newly aggressive loop.

Preserve ProcessWorkspaceProjectionClient as the native in-process adapter [P10]. Moving it or extracting a narrower interface must not weaken current CaptureAsync/RequireCurrentAsync project binding, canvas-session ownership or cached runtime selection reset. Separate API/control-plane consumers retain their current authority and payloads. Do not force UI round trips through HTTP or bypass native command admission.

Keep manager chat visually complete: actual messages/streaming, attachment/status/action controls, voice presentation and existing useful child views must be in the independent render closure. Use existing conversation renderers. A slot containing the entire old native manager-chat renderer or a decorative fake chat does not close this family. Native orchestration and sensitive context remain outside.

## Compatibility and effects

Update native imports, dynamic/dialog component identities and tests deliberately. Preserve public parameter/route/serialized contracts or provide justified compatibility forwarding/adapters for actual consumers. Code-behind splitting alone is not extraction. Do not retain a hidden reference from UI to the old native component just to keep a dynamic dialog working.

For files use the actual FileBrowser and FileInteraction controls and their supported narrow contracts. Keep re-resolution/no retained live-root cache, read-only content, the current 16 MiB viewer limit, current download/local-action restrictions and independent content-session disposal. Do not expose physical roots or credentials as UI IDs.

For template/graph/Markdown/chat content preserve sanitization and current authorization; fake fixture strings are not authority. Keep protected context freshness and database/project generation separate from the currently visible ID. Publishing a new view snapshot must not automatically query all history or include stale focused dialog data.

## Consequences and risks

| Change | Consumers/gates | Mitigation |
| --- | --- | --- |
| New render library and imports | Native module, Web, sandbox, component/browser projects | Build each; preserve assembly/dynamic identities and same real descendants |
| Public projection/view type move, if necessary | Native client, API serializers, Workbench, tests | Small dependency closure, source consumer rebuild and compatibility proof |
| Draft/session ownership | Server tabs, role/step dialogs, saves, context | Stable raw draft, per-opening lifetime, controlled late completion tests |
| File/voice/JS adapter split | FileTools, shared overlays/conversations, disposal | Owned resources, real child composition, native and browser effect tests |
| CSS/JS/Tailwind relocation | Source/published Web and sandbox | Clean generation, current content URLs, body-lock/focus and static-asset proof |
| More presentation snapshots | Live polling, large history, allocations | Bounded materialized data once per accepted revision; no full clone or I/O every render |
| New tests/project references | Actual Components/Stable solution and CI lists | Register all real entry points; preserve existing test rows and narrow inner loop |

No schema change is expected. A discovered need for one is a scoped architectural prerequisite requiring its own causal/compatibility review, not an automatic extension of PC1.
