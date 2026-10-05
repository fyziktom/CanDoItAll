# Structure selection and supporting windows

Complete [S19–S27], including the entire Selection Panel. Do not stop after the
three small windows or present the still-native selection form as a completed leaf.

## Actual UI and ownership

| Surface | Preserve |
|---|---|
| Object Index | Search across existing searchable fields, correct labels/icons/status/selected count, TreeView and exact right-click actions; a selected multi-target menu offers only the currently supported batch actions. |
| Signals | Full stackable markers, progress and priority tiles, active-state indications and actual window placement. |
| Canvas Health | Blocked/review/priority/selected-issue counts, spotlight and supported explicit Validate action. |
| No/single/multi selection | Feedback, focus/clear, status/progress/markers/priority, border name/create/clear, node actions, current mode and actual metadata. |
| Single-node detail | Workflow status and accepted output IDs, StorageSummaryCard, attachment metadata and actual host actions, Mermaid entry, advanced facts and the real detail child. |

Use real shared windows, TreeView, buttons, badges, StorageSummaryCard and dialogs.
Do not draw a substitute window just to avoid an asset/dependency issue. The renderer
gets safe view data, not tracked nodes, a Workbench service, raw secret configuration,
provider implementation, or a registry that can execute arbitrary operations.

## Rendered origin is part of every meaningful action

Current Signals passes only an action string and the host resolves live selected
node IDs [S20, S27]. Current Selection Panel also has several target-free callbacks
and optimizes rerender only by RenderKey [S22]. Current Object Index keeps menu target
IDs but can deliver through the current receiver after a replacement [S19]. These
are old integration assumptions to make explicit during extraction, not proof of
an observed unauthorized write.

Capture the displayed project identity/lifetime, view/selection generation, exact
selected IDs or node, opened menu/dialog identity and receiver at render/open time.
The host compares the action with that captured selection and its current native
admission. A-B-A, identical visible labels and reused node IDs do not make old actions
current again. Rerender suppression must account for changed origin/callback semantics,
not only unchanged display text. Preserve performance by avoiding needless whole-graph
cloning or read-on-render; do not “fix” everything by disabling all optimization.

Menu actions must have been offered for that original menu. A queued right-click
selection continuation cannot reopen a retired menu or rebind a batch to new nodes.
Define when a newer selection closes a menu; never silently retarget it. Keep
string tokens that are external/public compatibility contracts behind an explicit
mapping; a typed intent family is preferable inside the new seam.

## Existing writes remain real and governed

Marker semantics are currently additive/toggle: if all selected nodes have a marker,
remove that marker; otherwise add it. Do not replace every marker collection with
one chosen value. Preserve N/A/untracked, started, complete/progress and priority
meanings, project root/system-managed restrictions and partial mutation behavior.

Multi-node mutation, deletion and border/view-state writes use the actual native
owner and original expected project admission. New renderer flags are not authority.
If the reached old UI path lacks capture, add a narrow typed boundary to the existing
owner path; do not rebuild native mutation services. Ordinary operator/Agent/Process
source authority cannot be combined or manufactured from a DTO.

Validate selected may invoke real validation work. Trace its current caller and
owner before choosing the result contract. Never invoke it automatically on mount,
health refresh or a tooltip. Retain accepted run/effect identity before refreshing.
A confirmed write with refresh failure is not a rejected write; unknown admission
must not be blindly repeated.

## Integrations not reimplemented here

Selection may open an existing WB1 task editor, governed attachment preview/local
open, Mermaid viewer, toolbox, Workflow/Process operations or other native dialogs.
Keep those hosts and their authorization, original target and exact DialogReference.
Represent actual integrations in the production composition and sandbox explicitly.
Do not inject the old complete selection renderer through a slot and call that an
extraction. Slots are only for genuine native descendants outside WB2.

Attachment metadata is not file-content authority. No unsigned URL iframe/image
fallback, preview lease reuse or privileged local path in a render-only DTO. Node
advanced details remain display values prepared by the original factory; do not
move metadata parsing/trust policy into the leaf just to preserve a public type name.

## Window state, assets and proof

Persist placement/visibility only through existing native view-state behavior and
its original context. Window dragging/resize is presentation, but its persisted
state is still a write. Do not introduce new per-user storage, global window keys,
or mobile layout policy. Scope CSS that was inherited from the main Structure page
must be inventoried: move/copy only genuinely feature-owned rules; shared styles
remain at their owner, not duplicated implementations.

Prove all actions through actual native integration as well as delayed component
tests. Verify exact changed rows/IDs, unchanged neighbors and no changes after
Cancel or stale callback. Include two windows, same IDs in different project scopes,
revoked current access, a deleted/recreated project, disposed parents and retained
errors. Keep main-canvas pan/zoom/drag and WB1 task paths usable as integration smoke.
