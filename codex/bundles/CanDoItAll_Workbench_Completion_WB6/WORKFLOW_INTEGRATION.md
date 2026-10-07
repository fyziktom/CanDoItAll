# Workflow integration completion

## Full presentation

Move the actual definition picker, selected saved version, parent/project context, subtree
and asset toggles, additional source controls, manual JSON, input preview/warnings, start
backend/simulation options, status and accepted-run links. Preserve page-level layout and
current accessibility contracts; update tests intentionally when a public marker changes.
The Workflow authoring canvas is already extracted and is not redone here.

Current starting points: `ProjectStructurePage.WorkflowNodes.cs`, Workflow sections of
`ProjectStructureCanvasDialogs.razor`, mixed `OverlayStates`, and native
`ProjectStructureWorkflowNodeService`. [S09, S10, S16, S19]

## Identity and state

An opening names the exact project admission, parent occurrence, navigation/actor generation,
original Workflow selection and submitted input. Separate draft from requested preview and
accepted preview; no preview data from A may appear labelled B. Capture the full input settings,
including hidden additional sources/node IDs, rather than rebuilding from one visible control.
Reject stale/not-selectable versions, malformed input and lost source authority without broadening
scope. Same public project/node ID does not recreate the original target.

Explicit Add attaches/creates through the proper native path. For canonical tasks reuse the
existing task-resource attachment with execution/pricing/assignee guards. For ordinary graph
parents reuse WorkflowNodeService.CreateAsync and project leases. Inspect actual native
acceptance; retain all known created nodes, link/attachment receipts and warnings before
surface refresh. No re-create-by-name or fallback to an unguarded create.

Start freezes backend, simulation list, exact saved definition/version and caller intent.
Use the existing Workflow launch/admission service. Its StartAsync can return a recorded
admission despite later execution/projection problems. Do not flatten that into a boolean
success or replace its reserved run with an unrelated latest run. Explicit observation uses
the original intent/run. A new run requires a new deliberate operation.

Save/link, start accepted, running, completed, failed/incomplete and projection unavailable
are distinct presentation states. A Workflow that merely prints text must not count as the
required file/asset mutation. Simulation proves only simulation and is labelled accordingly.

## Required native tests

A-B-A openings; options read and error after close; duplicate handler/Enter; input preview
out-of-order; hidden input fields survive; saved-version drift; canonical task versus ordinary
parent; project recreated; actor/profile retired; native create accepted then observation fails;
start accepted then page closes; a second start uses a new intent; double-click uses one intent;
late old status cannot repaint a new node. Verify real stored IDs and unchanged neighbor data.

Rehearsal: create or use a deliberately small saved Workflow through normal product facilities,
link it through the new Workbench UI, inspect inputs, start without simulation, observe the
same native run complete and inspect its persisted artifact. Include one supported human
response/cancel or incomplete-output negative in deterministic validation without weakening
existing admission. Reopen after a clean app restart; no duplicate run is dispatched.
