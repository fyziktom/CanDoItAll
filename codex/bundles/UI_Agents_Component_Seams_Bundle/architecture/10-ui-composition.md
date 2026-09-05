# UI composition decisions and validation

This is a behavior-preserving architecture refactor, not a visual redesign. Apply this contract in SB02–SB07; each phase records the affected surfaces and evidence.

| Concern | Decision | Validation |
|---|---|---|
| Component library | Keep current CanDoItAll Razor components and existing styles; improve composition within those surfaces | Compare existing DOM/visual behavior; no incidental CSS framework or raw markup replacement |
| Content hierarchy and sizing | Retain primary/supporting content, statistics placement, list/editor arrangement and current text-area/dialog sizing | SB01 records the initial viewport and measurements; affected phases compare against that baseline |
| Tabs | Preserve ten labels/order/default; semantic enum maps to existing SelectedIndex API | Public section selection renders real content and keeps draft |
| Catalog | Keep selection, immediate search, expansion and current actions | Click/select/open flows; managed identities; empty/error states |
| Editor actions | Preserve Clear/Save/Delete location, enabled states, save-stays-open and confirmation policies | Interaction tests and large desktop real-host smoke |
| Overlays | Keep current modal/declarative host behavior, nested confirmation/wizard/selection/preview hierarchy | Exercise open/close/escape/focus return and no clipped active actions |
| Scrolling | Preserve the existing intended scroll owner and sticky action visibility | Real page/editor/nested overlay at 1600x1000 or larger |
| Assets | Inventory CSS isolation, theme/icons, JS and static web assets used by the real subtree | Production host and future sandbox dependency/asset checklist |
| Notifications | Exactly the owned outcome channel; no duplicate success/error after moved callbacks | Save/delete/refetch failure scenarios and host checks |

Do not silently fix a pre-existing layout or focus defect as part of seam movement. Record it with baseline evidence and distinguish a regression from an independent issue. Do not add responsive expansion or unquarantine unrelated broad browser flows.

SB01 captures representative current host/overlay screenshots and observable interaction oracles when execution starts. SB07 repeats the owned scenarios against the real application; bUnit snapshots cannot establish overlay stacking, focus, CSS isolation or watch-host behavior.
