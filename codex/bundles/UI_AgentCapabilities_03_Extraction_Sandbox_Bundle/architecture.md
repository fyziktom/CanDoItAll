# Extraction architecture

## Graph and smallest correct boundary

Module owns Surface; Surface and AgentDetailsDialog render AgentCapabilityList in broad AgentFramework.Components. Broad Components already references lightweight UI. UiSandbox references UI, which already references Models, Conversations.Components and live BaseLib. Models references the pure Capabilities.Abstractions project; that project currently has no project/package references. No new project is justified.

Target: Module/broad Components -> UI -> existing Models/pure abstractions/reusable UI; UiSandbox -> UI. Never UI -> Module, Core, Persistence, provider runtime, Voice or broad Components. The existing Models abstraction graph is explicit, not zero dependencies. Re-evaluate it before/after movement.

## Move map

| Source | Planned action |
|---|---|
| Module Pages/Components/AgentCapabilitiesSurface.razor and .razor.cs | Move real files to UI/Capabilities, retaining controlled contract and local interaction state |
| Module AgentCapabilitiesState.cs | Move pure snapshot/selection/load/access-draft/preview/intent records; exclude mutable editors, services and Core receipts |
| Application kind/status/state in Services/AgentCapabilityCommands.cs | Keep all declarations in Module: they determine retention, verification, retry and adoption. Map in the effect host to immutable operation presentation data; move only that presentation contract |
| CapabilityCuratorLaunchStatus and launch state in Services/CapabilityCuratorLaunch.cs | Keep application admission, acknowledgement and known chat identity in Module. Map one atomic launch snapshot to pure Curator presentation data |
| Broad Components AgentCapabilityList.razor, .razor.cs and .razor.css | Move real files; update both Surface and AgentDetailsDialog imports, never copy or wrap back to old assembly |
| AgentCapabilitiesPanel.razor.css and display:contents anchor | Transfer renderer-owned rules/anchor with Surface; preserve actual scoped ::deep behavior and remove the obsolete host styling |
| Access effect/scope/selector enums | Reuse dependency-free Capabilities.Abstractions already reachable through Models; explicit pure reference only if repository conventions require it; no duplication or Core convenience reference |
| Private assignment/type filter enums | Move with Surface; remain transient presentation details, not route/domain contracts |

The panel remains the single effect host. Session/read ownership, page callbacks, assignment attempts/recovery, proof publication, dialogs/notifications and Curator admission stay unchanged in responsibility. Module orchestration can consume pure rendering contracts; no domain or persistence implementation belongs in UI. Do not move any application outcome/recovery enum merely because the renderer displays it. Pure presentation contains message/action availability and opaque attempt identity for typed intents; it does not calculate recovery eligibility.

Moving Razor without its scope anchor would lose tree/card scroll limits, header columns and filters. Move the actual styling with a compatible renderer-owned display:contents anchor, verify generated selectors and both consumers. Do not add a competing scroll wrapper or duplicate CSS. Keep the real BaseLib Grid/ActionCard/Stack/Cluster/StatusBadge/Button in the list, its endpoint summary tooltip, and Surface ListDetailShell/TreeView/Avatar/tooltips/controls. Inventory fonts, icons and avatar fallback assets.

## Sandbox and assets

Extend existing query state with typed specimen `catalog` or `capabilities`; absence stays catalog. Preserve current catalog scenario/layout/agentId/teamId and replace-history behavior. Capabilities uses explicit stable scenario values and deterministic IDs; its missing-target fixture fails closed rather than selecting another agent. Never encode enum ordinals. Keep sandbox /agents; production routes are untouched. Local raw filters/draft remain transient unless a separately authorized bookmarkability task changes them.

Use embedded representative fixtures and the actual moved renderer/list. Sample intents only change controlled state/output. Construct pure presentation states without bringing command/receipt/runtime classes into the sandbox.

Fast currently scans UI, UiSandbox and Conversations.Components. Moving renderer/list into UI may need no new root. Audit generated classes first; never add broad Module or old Components roots as a shortcut. Live BaseLib supplies compiled CSS. Preserve Parity/Fast separate outputs, explicit missing-asset failure, compiled/runtime mode agreement, Development runtime probe and direct browser-refresh behavior.

## Rejected alternatives and lifetime

A second UI project/sandbox duplicates an accepted boundary. Referencing broad Components from UI creates a cycle. Copying the list or mocking its child controls invalidates extraction proof. A runtime controller in the sandbox defeats the light graph. Production URL binding is independent. Moving operation helpers because they use presentation records changes ownership and is forbidden.

Extracted Surface owns rendering/local transient interactions; sandbox owns sample selection/scenarios. Production target/panel/circuit lifetimes remain as proven by Capabilities-02. Different imports are not authorization to redesign behavior.

## Executed child-closure correction

The standalone build and a failing public Curator-action test exposed AgentAvatarActionButton as an omitted AppComponents child. Its exact service-free implementation moves to existing Conversations.Components with its public namespace preserved. AppComponents references that existing reusable UI assembly for all current callers; AgentFramework.UI already references it and adds no broad dependency. This avoids pulling Canvas/FileTools into the sandbox. See proof/SB01/avatar-child-review.md and movement.json. This shared assembly/reference change names one final stable checkpoint.
