# S0 — original Workflow dialog lifetime and a bounded visual repair

## WB6-W1: an old Workflow operation can overwrite or close a successor

Source-derived, not reproduced by this reviewer; located in the pre-existing remainder,
not introduced by WB5. Source: ProjectStructurePage.WorkflowNodes.cs. [S09]

`OpenAddWorkflowDialogAsync` captures the surface, awaits options, then assigns the shared
dialog field without an opening fence. `ExecuteWorkflowAddAsync` captures one dialog, awaits
native creation/attachment, then clears the shared field and reloads current page selection.
Its error branch restores the old dialog unconditionally. The start path checks ProjectId
but does not establish the same dialog/opening or lifetime; ProjectId equality does not
protect A-B-A, a new dialog on the same project, or a replacement project lifetime.
A disabled Button is not a handler-level admission gate.

Reproduce before editing:
1. Use actual Workbench form and actual native owner with a narrow held read/write boundary.
   Open A, start options or add/link, close or replace with B, then release A.
2. Exercise success and known failure. A must not close, reopen, select, mutate B's draft,
   reset B's busy state, or take current-page authority.
3. Repeat same target/new opening, same public project ID/new lifetime, two views, and
   duplicate submit/Enter. Require at most one native operation for one admitted submission.
4. Hold post-accept observation. Retain A's accepted node/link/run/version/intent even when
   the visible view disappears or observation fails. Retry observes that result only.
5. A normal current attempt still succeeds. A known rejection remains correctable; a genuinely
   unknown result cannot be unlocked by an unconditional catch/reset.

Repair at the existing host/owner seam. Reuse native admission/outcome information. Add only
an operation-specific immutable projection/result where information is genuinely missing.
Do not introduce a new durable transaction platform, fake runner, global dialog lock or
blanket prohibition on close. Preserve old accepted operations as such, not as B's operation.

Workflow metadata refresh and input preview also need request/opening fences after awaits.
Do not silently collapse multiple stored input sources to the single source the form displays.
A native options normalizer must not erase raw invalid JSON before the operator can correct it.

## WB6-V1: content-preview title collides with the canvas toolbar

This was observed and explicitly retained by the WB5 implementer, not reproduced here. [S03]
At 1920x1080/DPR1 open the native Content preview in the Structure canvas. Check actual title,
header, controls and content bounds/hit testing with other canvas windows both open and closed.
Fix the smallest appropriate content-host/overlay geometry. Verify normal/maximized views,
long content and two independent overlays. Shared CanvasOverlay behavior must remain correct
for WB3-WB5 and the new execution dialogs. No small/medium viewport campaign.

Acceptance: title and primary controls are legible/clickable, owned body scrolls without
moving controls off-screen, no z-index workaround exposes modal-underlay actions. Preserve
screenshots and geometry; do not remove the heading or assertion merely to make the test pass.

## Other findings

Repair small reproduced regression defects with focused tests. An unrelated large authority,
schema or persistence redesign is not authorized just before a demo. Record its exact source,
reproduction, effects, affected flows and safe operating boundary; keep demo readiness false
for affected mandatory journeys. Preserve a working published candidate while fixing it.
