# Measured next-step handoff

The useful first candidate is AgentCatalogPanel with actual AgentSelectionCard, TreeView, search/reset controls, status/tooltip/avatar primitives and catalog CSS. Its scenarios are populated/empty/loading catalogs, selected agent/team, filtering, tree expansion, managed action visibility and recorded select/open/chat/team intents. AgentCatalogHost remains the production effect owner; editor/team dialogs are deliberately outside this small sandbox scenario and continue working in production.

| Readiness dimension | Current verdict and evidence | Next owner / concrete work |
|---|---|---|
| Semantic boundary | Proven for catalog; explicit shell/editor read/command/session seams. Actual no-service catalog rendering and typed intent tests | Future UI extraction child reuses these owners and preserves production host adapters |
| Deterministic rendering | Proven for selected catalog and editor scenarios in component tests with real children and explicit external fakes | Turn the named catalog scenarios into deterministic browser-host fixtures |
| Interaction scenarios | Proven in focused component/real adapter tests; production browser acceptance is recorded separately in browser-report.md | Carry selection/request/result/negative scenarios into the extracted host regression gate |
| Lightweight project graph | Deferred. No project/reference extraction occurred; AgentFramework still evaluates 46 direct references | Cohesive catalog UI project/contract ownership; audit actual card/model closure before choosing dependencies |
| Standalone browser sandbox | Deferred. No standalone host is built or claimed | Implement one small host with production assets and real selected subtree, then measure it |
| Production bookmarkability | Deferred. Typed state and session identity prepare ownership; existing route codec/history/DialogService behavior remain | Product/navigation design decides direct-entry errors, push/replace/back/forward, dirty drafts, routed modal lifetime and Workbench/MAUI behavior |

## Actual graph and assets

AgentCatalogPanel.razor and its existing .razor.css retain the card/tree layout. AgentSelectionCard is defined in MAF/Common/CanDoItAll.AgentFramework.Components. Its current project references Models, Core, Voice and Conversations.Components; taking that entire project into a sandbox may keep the graph broad. CatalogSnapshot currently reuses AgentDefinition/AgentTeamDefinition, including their defining Models graph. Do not claim a contracts-only dependency until evaluated.

The Web host loads BaseLib material-symbol fonts/styles, BaseLib output.css, Web Tailwind output.css, app.css, module CSS isolation and Web.styles.css; Blazor and existing shell scripts supply runtime behavior. Inspect those concrete assets and the actual card/tooltip/tree descendants when building the small host. Existing Tailwind generation/CSS isolation must be included in a representative measurement. A host that drops styling or substitutes fake cards does not prove useful developer iteration.

The later editor sandbox additionally needs Workspace StorageCatalogSelectionField/Dialog and IStorageCatalogSelectionSource; ExternalWorkspaceRootSelectionField and infrastructure registry/bindings; Memory.Application/Abstractions/provider drivers; provider/model selectors; AvatarPicker gateway/declarative dialog; capability setup wizard and its real nested rendering. New project/secret metadata projections reduce direct UI DTO coupling, but their adapters still reference Projects/Security implementation services. These are explicit later ownership edges, not reasons to delay the narrower catalog host.

## Timing evidence and comparison

SB01 recorded Windows, SDK 10.0.303, live Components/FileTools SHAs, the full-app evaluated graph and 3,789 watch-list lines. The Debug watch host served the actual page but the backend reported Healthy/WaitingForChanges while isReadyForHotReload remained false; WatchReady timed out. No trustworthy three-repeat warm Razor/C#/CSS latency series was obtained. This is a measurement gap, not a measured improvement. Final Release browser runtime is functional proof and is not substituted for a warm watch benchmark.

The next host child owns a fresh comparable full-app and small-host measurement: cold startup separately; at least three supported warm edits per relevant Razor/C#/CSS category; include Tailwind/JS when used; edit-to-visible range/median and reload/restart/failure classification; temporary edits reverted; same machine/SDK/source mode/assets. Repair or diagnose readiness reporting before attributing latency to component boundaries. No percentage improvement target is invented here.

## Navigation decisions remain open

The meeting pack is proposal evidence, not approved routing. Existing semantic section and session target do not prescribe URLs. New/save/reset identity and host result lifetimes are explicit now, but production routing must still decide resource identity, hybrid path/query compatibility, direct load/not-found/permission state, unsaved-draft lifecycle, history restoration and overlay ownership. Current DialogService closes overlays on LocationChanged. A future routed editor needs an explicit retained/replaced host policy; no global dialog-library change has been made.
