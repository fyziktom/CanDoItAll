# WF1 scope and architectural destination

## One logical family, several coherent implementation stages

WF1 completes the existing Workflow canvas authoring and remaining page-owned dialog presentation.
It is larger than the preceding one-panel cuts. Its production route physically lives in
Modules.AgentFramework, but it is the next logical Workflow family rather than another technical-agent
tab. The full scope below is required in this run.

| Surface | Required output |
|---|---|
| Main canvas | Real CanvasWorkbench, stage, viewport, selection, move/edit/create/link actions, local graph state and toolbar |
| Floating windows | Toolbox/search/groups, selection list/details, component library and their actual window/overlay behavior |
| Definition and node inspectors | Existing fields and all supported node/route variants; real raw configuration, validation and action footers |
| Executor settings | Actual schema loop, image-generation selector presentation and approved custom-renderer composition |
| Prompt/component authoring | Existing Prompt picker and compatibility flow, new component/binding behavior and exact persistence results |
| Preview | Canvas and page input dialogs, project/node context, simulation controls, execution/result/progress presentation |
| Templates | Existing catalog in its actual dialog wrapper and read-only native canvas preview with Add-to-drafts action |
| Run/event overlays | Bounded safe summaries, events, artifact metadata, identifiers and separate owned disclosure lifetime |
| Composition | Existing five tabs, query routes, curator/context and profile-aware owner wiring remain functional |

## Existing work must remain small

`src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.UI` already owns service-free shell, catalog,
history, template list, overview and analytics. Its csproj has only ASP.NET, BaseLib and Charts. S12–S13.
Do not add the entire Canvas/Models graph to that existing leaf without a demonstrated boundary reason.
Preferred new locations:

```text
src/UI/CanDoItAll.AgentFramework.WorkflowAuthoring.UI
src/Sandboxes/CanDoItAll.AgentFramework.WorkflowAuthoring.UiSandbox
```

Use the existing shell through native composition; a new scenario host can compose both. A small
presentation/contracts assembly is optional if it solves an actual sharing edge. Reuse exact typed
Models/Workflows abstractions where their real closure and data contract are appropriate; do not ban
all MAF names or clone them all into DTOs. Conversely, a read/write runtime interface is not a neutral
rendering contract merely because it resides in Abstractions.

The module owns effect dispatch, profile and actor binding, native component/catalog operations,
trusted renderer registration, run admission and route significance. The presentation owns local
editing and read results bound to explicit origins. The renderer owns markup, local focus/element
references, typed intents and owned assets. Do not move HTTP clients, DbContexts, secrets resolution,
process launch or provider implementations into UI.

Real children and scoped CSS/static assets move with rendering. A hidden injection in a child, a
broad service bag, a fake canvas or a screenshot imitation fails the boundary even if the parent
looks light. Existing neutral CanvasLib/OverlayLib are the authoritative components, not a new engine.

## Not included

No runtime/backend rewrite, storage migration, new Workflow API protocol, changed Project Structure
admission, new Processes/Workbench UI, expanded node-kind support, live paid inference or mobile
redesign. Existing global settings, backend availability and unsupported feature refusals are
preserved; do not create a new global settings screen because a settings service is injected.
Native import/export remains its owner; preserve its data on round-trip, without inventing new UI
where no current entry exists. A new product-level unresolved authority issue must be mapped, not
silently solved by weakening its owner.

At closure publish a current caller/descendant/asset census identifying every in-scope renderer and
any intentionally retained host. Residual chat/usage/Voice/provider runtime screens remain recorded
for later work, not falsely marked complete by WF1.
