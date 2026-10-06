# Actual canvas, toolbox and generic editor

## Entry map

`ProjectStructurePage.razor` still owns the actual CanvasWorkbench composition,
three view tabs, toolbar, standard-block toolbox and several native form islands.
Its first-level callbacks include SelectionChanged, NodesMoved,
ContextActionRequested, CreateActionInvoked, NodeEdited, NodeOpened, StateChanged
and ClipboardRequested. Read all handlers and dynamic activation paths before
moving the markup; a lexical Razor-reference scan alone misses imperative composer
calls and registered setup renderers. S12/S21.

`ProjectStructurePage.CreateCatalog.cs` hydrates the semantic catalog with current
node/storage/image-provider choices. It deliberately routes task, secret-reference
and text-asset creation into dedicated owners. Preserve those routes; the new UI
must not infer a generic writable record just because it can render some fields.
Some values are safe reference metadata, others (file bytes, raw paths, credentials)
require their existing restricted treatment. S20.

`TryBuildNodeEditModel` uses the existing generic component and suppresses fields
that cannot be safely edited there. `ProjectStructureNodeEditor.ComposeUpdate`
merges submitted keys into typed metadata/references. Neither belongs in a new
browser-side domain implementation. Preserve unknown metadata and omitted values,
as well as explicit clearing, enum validation and time/decimal precision. S13/S14.

## Origin hazard to test before preserving the old flow

`OpenEditDialogAsync` opens the shared generic composer using a node snapshot.
`TryApplyNodeEditAsync` later resolves `SourceNodeId` against the current page
surface and sends an update using current `ProjectId`. This source pattern lacks
an explicit end-to-end original opening/admission contract in the shown handler.
Do not claim it caused a wrong write without testing surrounding composer retirement.
Prove route switch, ordinary reload, same-ID lifetime replacement, closed/reopened
composer and changed node type. Use the actual native writer. Preserve any existing
correct shared-component retirement and fix only the missing product boundary.

If the result is stale, reject before the wrong owner is invoked. If the native
write already committed, retain its original ID/fields and report a readback warning
instead of a second create. Unknown writes require owner observation, not replay.
Capture the original parent/source node and offered action for creation, including
implicit placement; selecting another node while the dialog is open is not permission
to change its parent.

## Rendering obligations

Render the existing CanvasWorkbench and generic create/edit controls, toolbar,
source labels, disabled explanations, group search, placement and action descriptions.
Keep actual accessibility mirror/keyboard navigation, SVG/canvas element ownership,
context menus and inspector toggles. Do not replace the graph with cards or an image,
and do not mount an old native component inside a supposedly independent leaf.

Trace scoped CSS and runtime asset order. Shared CanvasLib already owns Workbench,
Overlay and generic-canvas runtimes; load its supported asset components once.
No copied JS implementation or second canvas system. Only feature CSS belongs in
the leaf. Do not lift node-domain catalogs into Components merely to avoid a
product reference. Dynamic trusted setup factories stay native behind narrow slots.

## Raw input and draft proof

Test invalid/intermediate dates, decimal values, JSON or typed custom inputs; text
before blur/Enter; correction after an error; event delivery while parent refreshes;
and same-target parameter echoes. Preserve sub-minute time values and legitimate
zero coordinates. Reject invalid values visibly, not by silently retaining and
saving an old valid value under the invalid text.

When the shared composer deliberately closes on submission, verify accepted outcome
reporting and safe recovery at the native owner. Do not silently change global
composer behavior to retain all dialogs for all consumers. A necessary shared seam
must be additive/opt-in and tested against Workflow WF1 and other CanvasLib consumers.
No approval-free capability, process or file action is introduced by authoring metadata.

## Gestures and persistence

For select/multiselect, pan/zoom, node drag, dependency connect/reconnect, reparent,
recomposition and window changes, test both physical browser gestures and owner
readback. Layout is not task schedule. Keep read-only/system-managed flags effective
in direct/group/dependency gestures and at native writes. A painted new position
is not proof of persistence; restore/readback must show the intended result.

Coalesce/debounce only where the current contract permits. Keep ordering of accepted
same-owner layout writes and retire superseded queued work; an old finally cannot
clear the new queue. Do not serialize unrelated native operations through one giant
global lock. No refresh/re-render may duplicate media creation or graph mutation.
